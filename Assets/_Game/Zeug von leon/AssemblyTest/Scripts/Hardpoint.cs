using UnityEngine;
using MechCombat;

namespace MechCombat.FCS
{
    public enum HardpointSlot { LeftArm, RightArm, LeftBackMount, RightBackMount }

    /// <summary>
    /// An equippable arm/mount slot. Builds a two-part hierarchy at runtime:
    /// a static base (does not rotate) and a rotating model (gets an ArmAimController).
    /// The rotating instance is parented directly to this Hardpoint's transform (not the base),
    /// so its local rotation stays relative to the hardpoint's own parent frame - see ArmAimController.
    /// </summary>
    public class Hardpoint : MonoBehaviour
    {
        [Header("Slot")]
        public HardpointSlot Slot;

        [Header("Prefabs")]
        [Tooltip("Non-moving base part (e.g. shoulder housing).")]
        [SerializeField] private GameObject _basePrefab;
        [Tooltip("The rotating part carrying the weapon visuals; will receive an ArmAimController.")]
        [SerializeField] private GameObject _rotatingModelPrefab;
        [SerializeField] private Vector3 _rotatingModelOffset;

        [Header("Weapon")]
        [SerializeField] private WeaponController _equippedWeapon;

        private GameObject _baseInstance;
        private GameObject _rotatingInstance;
        private ArmAimController _armAimController;

        public WeaponController EquippedWeapon => _equippedWeapon;
        public ArmAimController AimController => _armAimController;
        public Transform RotatingRoot => _rotatingInstance != null ? _rotatingInstance.transform : null;

        void Awake()
        {
            BuildHierarchy();
        }

        void BuildHierarchy()
        {
            if (_basePrefab != null)
            {
                _baseInstance = Instantiate(_basePrefab, transform);
                _baseInstance.transform.localPosition = Vector3.zero;
                _baseInstance.transform.localRotation = Quaternion.identity;
            }

            if (_rotatingModelPrefab != null)
            {
                _rotatingInstance = Instantiate(_rotatingModelPrefab, transform);
                _rotatingInstance.transform.localPosition = _rotatingModelOffset;
                _rotatingInstance.transform.localRotation = Quaternion.identity;

                _armAimController = _rotatingInstance.GetComponent<ArmAimController>();
                if (_armAimController == null)
                    _armAimController = _rotatingInstance.AddComponent<ArmAimController>();
            }
        }

        public void EquipWeapon(WeaponController weapon)
        {
            _equippedWeapon = weapon;
        }

        /// <summary>
        /// Ballistics origin: the equipped weapon's own transform position.
        /// </summary>
        public Vector3 GetMuzzlePosition()
        {
            if (_equippedWeapon != null)
                return _equippedWeapon.transform.position;
            return RotatingRoot != null ? RotatingRoot.position : transform.position;
        }
    }
}
