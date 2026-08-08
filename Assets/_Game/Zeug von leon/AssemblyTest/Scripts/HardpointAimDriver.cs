using UnityEngine;
using MechCombat;

namespace MechCombat.FCS
{
    /// <summary>
    /// Ties FireControlSystem's lock state to each equipped Hardpoint: while a target is
    /// locked, solves a per-weapon lead point (each hardpoint's own muzzle position and its
    /// equipped weapon's actual Projectile ballistics) and drives that hardpoint's
    /// ArmAimController toward it. Otherwise eases all arms back to rest.
    /// Ballistics data (velocity, maxVelocity, drag, gravityMultiplier) is read straight from the
    /// Projectile component on WeaponController.mechanics.projectile - the gun itself only
    /// contributes its own multiplier.velocityMultiplier. No changes to WeaponController needed.
    /// </summary>
    public class HardpointAimDriver : MonoBehaviour
    {
        [SerializeField] private FireControlSystem _fcs;
        [Tooltip("Rigidbody used for the shooter's own velocity in the lead calculation.")]
        [SerializeField] private Rigidbody _shooterRigidbody;
        [SerializeField] private Hardpoint[] _hardpoints;

        void Update()
        {
            float dt = Time.deltaTime;

            if (_fcs == null || _fcs.State != LockState.Locked || _fcs.LockedTarget == null)
            {
                foreach (var hp in _hardpoints)
                {
                    if (hp != null && hp.AimController != null)
                        Relax(hp, dt);
                }
                return;
            }

            LockableTarget target = _fcs.LockedTarget;
            Vector3 targetPos = target.transform.position;
            Vector3 targetVel = target.GetVelocity();
            Vector3 shooterVel = _shooterRigidbody != null ? _shooterRigidbody.velocity : Vector3.zero;

            foreach (var hp in _hardpoints)
            {
                if (hp == null || hp.AimController == null || hp.EquippedWeapon == null) continue;

                if (hp.EquippedWeapon.mechanics.projectile == null) continue; // no projectile assigned on the weapon yet
                Projectile projectile = hp.EquippedWeapon.mechanics.projectile.GetComponent<Projectile>();
                if (projectile == null) continue;

                Vector3 muzzle = hp.GetMuzzlePosition();
                Vector3 aimPoint;

                if (projectile.movement.bulletType == BulletType.Laser)
                {
                    // hitscan: no travel time, so no lead - aim straight at the target
                    aimPoint = BallisticsAimSolver.SolveHitscanAimPoint(targetPos);
                }
                else
                {
                    aimPoint = BallisticsAimSolver.SolveAimPoint(
                        muzzle,
                        shooterVel,
                        targetPos,
                        targetVel,
                        projectile.movement.velocity,
                        projectile.movement.maxVelocity,
                        hp.EquippedWeapon.multiplier.velocityMultiplier,
                        projectile.movement.drag,
                        projectile.damage.gravityMultiplier);
                }

                hp.AimController.AimAt(aimPoint, dt);
            }
        }

        void Relax(Hardpoint hp, float dt) => hp.AimController.ReturnToRest(dt);
    }
}
