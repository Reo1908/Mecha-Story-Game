using System.Collections.Generic;
using UnityEngine;

namespace MechaGame
{
    /// <summary>
    /// Hitscan-Bewaffnung des Mechas: feuert alle montierten Waffen (links/rechts,
    /// jede mit eigener Feuerrate aus ihrer <see cref="WeaponSpec"/>) in
    /// Blickrichtung, leicht zum Aim-Assist-Ziel gebogen. Zeigt Tracer,
    /// Mündungsblitz und Treffereffekt. Meldet Treffer über <see cref="OnHit"/>
    /// an das HUD.
    /// </summary>
    public class HitscanWeapon : MonoBehaviour
    {
        public MechaCameraRig CameraRig;
        public AimAssist AimAssist;

        class Barrel
        {
            public WeaponSpec Spec;
            public Transform Muzzle;
            public Light MuzzleLight;
            public float Cooldown;
        }

        readonly List<Barrel> _barrels = new List<Barrel>();

        /// <summary>Wird bei einem Treffer auf ein Ziel ausgelöst. (Trefferpunkt, Ziel zerstört?)</summary>
        public event System.Action<Vector3, bool> OnHit;

        /// <summary>Verdrahtet die in der Werkstatt gewählten Waffen (null = Seite unbewaffnet).</summary>
        public void SetWeapons(WeaponSpec left, Transform muzzleLeft, WeaponSpec right, Transform muzzleRight)
        {
            _barrels.Clear();
            AddBarrel(left, muzzleLeft);
            AddBarrel(right, muzzleRight);
        }

        void AddBarrel(WeaponSpec spec, Transform muzzle)
        {
            if (spec == null)
                return;

            var barrel = new Barrel { Spec = spec, Muzzle = muzzle };
            if (muzzle != null)
            {
                var lightGo = new GameObject("MuzzleLight");
                lightGo.transform.SetParent(muzzle, false);
                barrel.MuzzleLight = lightGo.AddComponent<Light>();
                barrel.MuzzleLight.type = LightType.Point;
                barrel.MuzzleLight.color = new Color(1f, 0.8f, 0.4f);
                barrel.MuzzleLight.range = 14f;
                barrel.MuzzleLight.intensity = 0f;
            }
            _barrels.Add(barrel);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            InputReader input = InputReader.Instance;
            bool firing = input != null && input.FireHeld;

            foreach (Barrel barrel in _barrels)
            {
                barrel.Cooldown -= dt;
                if (barrel.MuzzleLight != null && barrel.MuzzleLight.intensity > 0f)
                    barrel.MuzzleLight.intensity = Mathf.Max(0f, barrel.MuzzleLight.intensity - dt * 80f);

                if (firing && barrel.Cooldown <= 0f)
                {
                    barrel.Cooldown = 1f / Mathf.Max(0.1f, barrel.Spec.FireRate);
                    Fire(barrel);
                }
            }
        }

        void Fire(Barrel barrel)
        {
            Ray aimRay = CameraRig != null ? CameraRig.GetAimRay() : new Ray(transform.position, transform.forward);

            Vector3 direction = AimAssist != null
                ? AimAssist.GetAssistedShotDirection(aimRay.direction)
                : aimRay.direction;

            Vector3 endPoint = aimRay.origin + direction * barrel.Spec.Range;
            bool hitSomething = Physics.Raycast(aimRay.origin, direction, out RaycastHit hit, barrel.Spec.Range);

            TargetDummy target = null;
            if (hitSomething)
            {
                endPoint = hit.point;
                target = hit.collider.GetComponentInParent<TargetDummy>();
            }

            if (target != null)
            {
                bool killed = target.TakeDamage(barrel.Spec.Damage);
                OnHit?.Invoke(endPoint, killed);
            }

            Vector3 tracerStart = barrel.Muzzle != null ? barrel.Muzzle.position : aimRay.origin;
            TracerEffect.Spawn(tracerStart, endPoint);

            if (hitSomething)
            {
                Color flashColor = target != null ? new Color(1f, 0.55f, 0.15f) : new Color(0.8f, 0.8f, 0.7f);
                HitFlash.Spawn(endPoint, flashColor, target != null ? 1.4f : 0.7f);
            }

            if (barrel.MuzzleLight != null)
                barrel.MuzzleLight.intensity = 5f;
        }
    }
}
