using Services;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class FrostCrystal : MonoBehaviour
    {
        [SerializeField] private Vector2 _scaleRange = new(0.7f, 1.3f);
        [SerializeField, Min(0.01f)] private float _growDuration = 0.3f;
        [SerializeField, Min(0f)] private float _overshoot = 1.6f;

        private float _scale;
        private float _time;
        private float _lifetime;
        private float _meltDuration;

        public void Grow(float lifetime, float meltDuration)
        {
            _lifetime = lifetime;
            _meltDuration = meltDuration;
            _scale = Random.Range(_scaleRange.x, _scaleRange.y);
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float grown = Easing.OutBack(Mathf.Clamp01(_time / _growDuration), _overshoot);
            float kept = Mathf.Clamp01((_lifetime + _meltDuration - _time) / _meltDuration);
            transform.localScale = Vector3.one * (_scale * grown * kept);
        }
    }
}
