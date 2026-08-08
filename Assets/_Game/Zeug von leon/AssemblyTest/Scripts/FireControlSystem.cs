using UnityEngine;

namespace MechCombat.FCS
{
    public enum LockState { Searching, Locking, Locked }

    [System.Serializable]
    public struct SteeringMargin
    {
        [Tooltip("Distance from the screen edge (reference-resolution pixels) where the turn command hits +-1.")]
        public float outerDistance;
        [Tooltip("Distance from the screen edge (reference-resolution pixels) where the turn command starts, at 0.")]
        public float innerDistance;
    }

    /// <summary>
    /// Equippable Fire Control System part. Drives reticle -> lockbox -> target lock,
    /// and exposes HorizontalTurnCommand / VerticalLookAngle for the mech's body/look rig,
    /// plus LockedTarget for weapon aim solvers to consume.
    /// Rendering the lockbox itself is left to a UI script - use LockboxScreenPosition/Size.
    /// </summary>
    public class FireControlSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [Tooltip("World-space point line-of-sight checks are cast from. Falls back to the camera if unset.")]
        [SerializeField] private Transform _sensorOrigin;

        [Header("Reference Resolution (keeps sizes uniform across screen sizes)")]
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920, 1080);

        [Header("Lockbox")]
        [SerializeField] private Vector2 _lockboxSize = new Vector2(120f, 120f);
        [SerializeField] private float _lockboxMaxFollowSpeed = 900f; // reference px/sec
        [Tooltip("How much the raw reticle movement each frame can directly drag the lockbox off target.")]
        [SerializeField] private float _reticleDragInfluence = 1f;
        [SerializeField] private LayerMask _lineOfSightBlockingMask;
        [SerializeField] private LayerMask _targetableSearchMask = ~0;
        [SerializeField] private float _targetSearchRadius = 500f;

        [Header("Locking")]
        [SerializeField] private float _lockTime = 1.5f;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _lockingLoopClip;
        [SerializeField] private AudioClip _lockConfirmClip;

        [Header("Reticle Input")]
        [SerializeField] private float _mouseSensitivity = 20f;   // reference px per mouse-delta unit
        [SerializeField] private float _stickSpeed = 1200f;       // reference px/sec at full stick deflection

        [Header("Horizontal Steering Margin")]
        [SerializeField] private SteeringMargin _horizontalMargin = new SteeringMargin { innerDistance = 400f, outerDistance = 60f };

        [Header("Vertical Look")]
        [SerializeField] private float _verticalDeadzone = 40f; // reference px from screen vertical center
        [SerializeField] private float _verticalMaxAngle = 60f; // degrees
        [SerializeField] private float _verticalLookSpeed = 90f; // degrees/sec

        // --- runtime state ---
        private Vector2 _reticleScreenPos;
        private Vector2 _reticleDeltaThisFrame;
        private Vector2 _lockboxScreenPos;
        private LockState _state = LockState.Searching;
        private LockableTarget _candidateTarget;
        private LockableTarget _lockedTarget;
        private float _lockProgress;

        public LockState State => _state;
        public LockableTarget LockedTarget => _lockedTarget;
        public float HorizontalTurnCommand { get; private set; }
        public float VerticalLookAngle { get; private set; }
        public Vector2 LockboxScreenPosition => _lockboxScreenPos;
        public Vector2 LockboxScreenSize => _lockboxSize * ResolutionScale;
        public float LockProgress01 => Mathf.Clamp01(_lockProgress / Mathf.Max(0.001f, _lockTime));

        private float ResolutionScale => Screen.height / _referenceResolution.y;
        private Vector3 SensorOrigin => _sensorOrigin != null ? _sensorOrigin.position : _camera.transform.position;

        void Awake()
        {
            if (_camera == null) _camera = Camera.main;
            _reticleScreenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            _lockboxScreenPos = _reticleScreenPos;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            HandleReticleInput();
            UpdateLockboxTracking(dt);
            UpdateTargeting(dt);
            UpdateHorizontalSteering();
            UpdateVerticalLook(dt);
        }

        // ---------------- Reticle input ----------------

        void HandleReticleInput()
        {
            // NOTE: swap these for your actual input system (new Input System / custom action maps).
            // Mouse delta is a per-frame delta already, so no dt multiply. Stick is a held axis, so it does need dt.
            Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * _mouseSensitivity;
            Vector2 stickAxis = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            Vector2 stickDelta = stickAxis * _stickSpeed * Time.deltaTime;

            _reticleDeltaThisFrame = (mouseDelta + stickDelta) * ResolutionScale;
            _reticleScreenPos += _reticleDeltaThisFrame;
            _reticleScreenPos.x = Mathf.Clamp(_reticleScreenPos.x, 0f, Screen.width);
            _reticleScreenPos.y = Mathf.Clamp(_reticleScreenPos.y, 0f, Screen.height);
        }

        // ---------------- Lockbox movement ----------------

        void UpdateLockboxTracking(float dt)
        {
            Vector2 trackPos = _reticleScreenPos;
            LockableTarget tracked = _state == LockState.Locked ? _lockedTarget : _candidateTarget;

            if ((_state == LockState.Locking || _state == LockState.Locked) && tracked != null)
            {
                Vector3 sp = _camera.WorldToScreenPoint(tracked.transform.position);
                if (sp.z > 0f) trackPos = sp;
            }

            // auto-tracking: box glides toward the target (or reticle, if unlocked) at a capped speed
            _lockboxScreenPos = Vector2.MoveTowards(_lockboxScreenPos, trackPos, _lockboxMaxFollowSpeed * ResolutionScale * dt);

            // manual force: raw reticle movement can additionally drag the box directly,
            // letting a fast mouse flick out-run the auto-tracking pull and break lock
            _lockboxScreenPos += _reticleDeltaThisFrame * _reticleDragInfluence;
        }

        Rect LockboxRect()
        {
            Vector2 size = LockboxScreenSize;
            return new Rect(_lockboxScreenPos.x - size.x * 0.5f, _lockboxScreenPos.y - size.y * 0.5f, size.x, size.y);
        }

        // ---------------- Targeting ----------------

        void UpdateTargeting(float dt)
        {
            LockableTarget best = FindBestCandidate();

            switch (_state)
            {
                case LockState.Searching:
                    if (best != null)
                    {
                        _candidateTarget = best;
                        _lockProgress = 0f;
                        _state = LockState.Locking;
                        PlayLoop();
                    }
                    break;

                case LockState.Locking:
                    if (best == null)
                    {
                        CancelLock();
                        break;
                    }

                    // a strictly higher-priority target entering the box interrupts the current lock attempt
                    if (best != _candidateTarget)
                    {
                        _candidateTarget = best;
                        _lockProgress = 0f;
                    }

                    _lockProgress += dt;
                    if (_lockProgress >= _lockTime)
                    {
                        _lockedTarget = _candidateTarget;
                        _candidateTarget = null;
                        _state = LockState.Locked;
                        StopLoop();
                        PlayConfirm();
                    }
                    break;

                case LockState.Locked:
                    if (_lockedTarget == null || !IsStillValid(_lockedTarget))
                    {
                        _lockedTarget = null;
                        _state = LockState.Searching;
                    }
                    break;
            }
        }

        LockableTarget FindBestCandidate()
        {
            Rect box = LockboxRect();
            Vector2 boxCenter = _lockboxScreenPos;

            Collider[] hits = Physics.OverlapSphere(SensorOrigin, _targetSearchRadius, _targetableSearchMask, QueryTriggerInteraction.Collide);

            LockableTarget best = null;
            int bestPriority = int.MaxValue;
            float bestDist = float.MaxValue;

            foreach (var col in hits)
            {
                var lt = col.GetComponentInParent<LockableTarget>();
                if (lt == null || !lt.Lockable) continue;

                Vector3 sp3 = _camera.WorldToScreenPoint(lt.transform.position);
                if (sp3.z <= 0f) continue;
                Vector2 sp = sp3;
                if (!box.Contains(sp)) continue;

                if (!HasLineOfSight(lt.transform.position)) continue;

                float dist = Vector2.Distance(sp, boxCenter);

                if (lt.LockPriority < bestPriority || (lt.LockPriority == bestPriority && dist < bestDist))
                {
                    best = lt;
                    bestPriority = lt.LockPriority;
                    bestDist = dist;

                    if (bestPriority == 0) break; // priority 0 always wins outright
                }
            }

            return best;
        }

        bool IsStillValid(LockableTarget target)
        {
            if (!target.Lockable) return false;

            Vector3 sp3 = _camera.WorldToScreenPoint(target.transform.position);
            if (sp3.z <= 0f) return false;
            if (!LockboxRect().Contains((Vector2)sp3)) return false;

            return HasLineOfSight(target.transform.position);
        }

        bool HasLineOfSight(Vector3 worldPos)
        {
            return !Physics.Linecast(SensorOrigin, worldPos, _lineOfSightBlockingMask, QueryTriggerInteraction.Ignore);
        }

        void CancelLock()
        {
            _candidateTarget = null;
            _lockProgress = 0f;
            _state = LockState.Searching;
            StopLoop();
        }

        void PlayLoop()
        {
            if (_audioSource == null || _lockingLoopClip == null) return;
            _audioSource.clip = _lockingLoopClip;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        void StopLoop()
        {
            if (_audioSource == null) return;
            if (_audioSource.isPlaying && _audioSource.clip == _lockingLoopClip) _audioSource.Stop();
        }

        void PlayConfirm()
        {
            if (_audioSource == null || _lockConfirmClip == null) return;
            _audioSource.PlayOneShot(_lockConfirmClip);
        }

        // ---------------- Body steering ----------------

        void UpdateHorizontalSteering()
        {
            float leftDist = _lockboxScreenPos.x;
            float rightDist = Screen.width - _lockboxScreenPos.x;
            float inner = _horizontalMargin.innerDistance * ResolutionScale;
            float outer = _horizontalMargin.outerDistance * ResolutionScale;

            float turn = 0f;
            if (leftDist <= inner)
                turn = -Mathf.InverseLerp(inner, outer, leftDist);
            else if (rightDist <= inner)
                turn = Mathf.InverseLerp(inner, outer, rightDist);

            HorizontalTurnCommand = Mathf.Clamp(turn, -1f, 1f);
        }

        void UpdateVerticalLook(float dt)
        {
            float centerY = Screen.height / 2f;
            float offset = _lockboxScreenPos.y - centerY;
            float deadzone = _verticalDeadzone * ResolutionScale;

            float target = 0f;
            if (Mathf.Abs(offset) > deadzone)
            {
                float beyond = Mathf.Abs(offset) - deadzone;
                float maxRange = Mathf.Max(centerY - deadzone, 0.001f);
                float t = Mathf.Clamp01(beyond / maxRange);
                target = Mathf.Sign(offset) * t * _verticalMaxAngle;
            }

            VerticalLookAngle = Mathf.MoveTowards(VerticalLookAngle, target, _verticalLookSpeed * dt);
        }
    }
}
