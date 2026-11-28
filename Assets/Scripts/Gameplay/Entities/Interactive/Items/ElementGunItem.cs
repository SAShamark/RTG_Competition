using Services;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ElementGunItem : MonoBehaviour
    {
        [SerializeField] private Transform _muzzle;

        [Header("Trigger")]
        [SerializeField] private Transform _trigger;
        [SerializeField, Min(0f)] private float _triggerAngle = 20f;
        [SerializeField, Min(0.01f)] private float _triggerDuration = 0.18f;

        [Header("Element")]
        [SerializeField] private ElementAmmo[] _ammo;
        [SerializeField, Min(0)] private int _startIndex = 2;
        [SerializeField] private Renderer _body;
        [SerializeField, Min(0)] private int _glowMaterialIndex = 3;

        private Quaternion _triggerRest;
        private float _triggerTime;
        private int _index;

        public Transform Muzzle => _muzzle;
        public ElementProjectile Projectile => _ammo[_index].Projectile;

        public void SelectNext() => Select((_index + 1) % _ammo.Length);

        public void PlayShot()
        {
            _triggerTime = 0f;
            _ammo[_index].PlayMuzzleFlash();
        }

        private void Awake()
        {
            _triggerRest = _trigger.localRotation;
            _triggerTime = _triggerDuration;
            Select(_startIndex);
        }

        private void Update()
        {
            if (_triggerTime >= _triggerDuration)
            {
                return;
            }

            _triggerTime = Mathf.Min(_triggerTime + Time.deltaTime, _triggerDuration);
            float pull = Easing.Parabola(_triggerTime / _triggerDuration);
            _trigger.localRotation = _triggerRest * Quaternion.Euler(pull * _triggerAngle, 0f, 0f);
        }

        private void Select(int index)
        {
            _index = index;
            for (int i = 0; i < _ammo.Length; i++)
            {
                _ammo[i].SetShown(i == index);
            }

            Material[] materials = _body.sharedMaterials;
            materials[_glowMaterialIndex] = _ammo[index].Glow;
            _body.sharedMaterials = materials;
        }
    }
}
