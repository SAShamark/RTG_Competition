using UnityEngine;

namespace UI.Widgets
{
    public sealed class UiPulse : MonoBehaviour
    {
        [SerializeField] private float _strength = 0.18f;
        [SerializeField] private float _duration = 0.35f;

        private float _time = -1f;
        private Vector3 _baseScale = Vector3.one;

        private void Awake() => _baseScale = transform.localScale;

        public void Play()
        {
            _time = 0f;
        }

        private void Update()
        {
            if (_time < 0f)
            {
                return;
            }

            _time += Time.unscaledDeltaTime;
            float t = _time / _duration;
            float k = t < 1f ? Mathf.Sin(t * Mathf.PI) * (1f - t) : 0f;
            transform.localScale = _baseScale * (1f + _strength * k);
            if (t >= 1f)
            {
                _time = -1f;
                transform.localScale = _baseScale;
            }
        }
    }
}
