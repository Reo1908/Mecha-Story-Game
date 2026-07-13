using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Einfache Hitscan-Waffe: schießt in Blickrichtung (Kameramitte),
    /// leicht zum Aim-Assist-Ziel gebogen. Zeigt Tracer, Mündungsblitz und
    /// Treffereffekt. Meldet Treffer über <see cref="OnHit"/> an das HUD.
    /// Schaden, Feuerrate und Reichweite kommen aus der in der Werkstatt
    /// gewählten <see cref="WeaponDef"/>.
    /// </summary>
    public class HitscanWeapon : MonoBehaviour
    {
        public MechaCameraRig CameraRig;
        public AimAssist AimAssist;
        public Transform Muzzle;
        public WeaponDef Weapon;

        WeaponDef Def
        {
            get
            {
                if (Weapon == null)
                    Weapon = WeaponLibrary.GetWeapon(MechaLoadout.GetWeapon());
                return Weapon;
            }
        }

        /// <summary>Wird bei einem Treffer auf ein Ziel ausgelöst. (Trefferpunkt, Ziel zerstört?)</summary>
        public event System.Action<Vector3, bool> OnHit;

        float _cooldown;
        Light _muzzleLight;

        void Start()
        {
            if (Muzzle != null)
            {
                var lightGo = new GameObject("MuzzleLight");
                lightGo.transform.SetParent(Muzzle, false);
                _muzzleLight = lightGo.AddComponent<Light>();
                _muzzleLight.type = LightType.Point;
                _muzzleLight.color = new Color(1f, 0.8f, 0.4f);
                _muzzleLight.range = 14f;
                _muzzleLight.intensity = 0f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _cooldown -= dt;

            if (_muzzleLight != null && _muzzleLight.intensity > 0f)
                _muzzleLight.intensity = Mathf.Max(0f, _muzzleLight.intensity - dt * 80f);

            InputReader input = InputReader.Instance;
            if (input != null && input.FireHeld && _cooldown <= 0f)
            {
                _cooldown = 1f / Def.FireRate;
                Fire();
            }
        }

        void Fire()
        {
            Ray aimRay = CameraRig != null ? CameraRig.GetAimRay() : new Ray(transform.position, transform.forward);

            Vector3 direction = AimAssist != null
                ? AimAssist.GetAssistedShotDirection(aimRay.direction)
                : aimRay.direction;

            Vector3 endPoint = aimRay.origin + direction * Def.Range;
            bool hitSomething = Physics.Raycast(aimRay.origin, direction, out RaycastHit hit, Def.Range);

            TargetDummy target = null;
            if (hitSomething)
            {
                endPoint = hit.point;
                target = hit.collider.GetComponentInParent<TargetDummy>();
            }

            if (target != null)
            {
                bool killed = target.TakeDamage(Def.Damage);
                OnHit?.Invoke(endPoint, killed);
            }

            Vector3 tracerStart = Muzzle != null ? Muzzle.position : aimRay.origin;
            TracerEffect.Spawn(tracerStart, endPoint);

            if (hitSomething)
            {
                Color flashColor = target != null ? new Color(1f, 0.55f, 0.15f) : new Color(0.8f, 0.8f, 0.7f);
                HitFlash.Spawn(endPoint, flashColor, target != null ? 1.4f : 0.7f);
            }

            if (_muzzleLight != null)
                _muzzleLight.intensity = 5f;
        }
    }
}
