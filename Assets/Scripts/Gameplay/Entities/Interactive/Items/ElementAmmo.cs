using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class ElementAmmo
    {
        [SerializeField] private ElementType _type;
        [SerializeField] private GameObject _screenIcon;
        [SerializeField] private Material _glow;
        [SerializeField] private ElementProjectile _projectile;
        [SerializeField] private ParticleSystem _muzzleFlash;

        public Material Glow => _glow;
        public ElementProjectile Projectile => _projectile;

        public void PlayMuzzleFlash() => _muzzleFlash.Play(true);

        public void SetShown(bool isShown) => _screenIcon.SetActive(isShown);
    }
}
