using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    [DefaultExecutionOrder(300)]
    public sealed class JawControl : MonoBehaviour
    {
        [SerializeField] private Transform _jaw;
        [SerializeField] private Vector3 _axis = Vector3.right;
        [SerializeField] private float _openAngle = 11f;
        [SerializeField] private float _syllableRate = 5.5f;
        [SerializeField] private float _emphasisRate = 1.7f;
        [SerializeField, Range(0f, 1f)] private float _minEmphasis = 0.35f;
        [SerializeField] private float _sharpness = 28f;

        private Quaternion _closed;
        private float _openness;
        private float _seed;
        private bool _isTalking;

        public void SetTalking(bool isTalking) => _isTalking = isTalking;

        private void Awake()
        {
            _closed = _jaw.localRotation;
            _seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            _openness = Mathf.Lerp(_openness, _isTalking ? Syllable() : 0f, Easing.Damp(_sharpness, Time.deltaTime));
            _jaw.localRotation = _closed * Quaternion.AngleAxis(_openAngle * _openness, _axis);
        }

        // Each syllable is one half-wave of a sine; a slow noise makes some of them wide and some barely a murmur.
        private float Syllable()
        {
            float time = Time.time + _seed;
            float beat = Mathf.Abs(Mathf.Sin(time * _syllableRate * Mathf.PI));
            float emphasis = Mathf.Lerp(_minEmphasis, 1f, Mathf.PerlinNoise(time * _emphasisRate, _seed));
            return beat * emphasis;
        }
    }
}
