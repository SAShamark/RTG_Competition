using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(Light))]
    public class LightFlash : MonoBehaviour
    {
        [SerializeField] private float _duration = 1.2f;

        private Light _light;
        private float _peak;
        private float _time;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _peak = _light.intensity;
        }

        private void OnEnable()
        {
            _time = 0f;
            if (_light != null)
            {
                _light.intensity = _peak;
            }
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float k = Mathf.Clamp01(_time / _duration);
            _light.intensity = _peak * (1f - k) * (1f - k) * (1f + Mathf.Sin(_time * 40f) * 0.15f * (1f - k));
        }
    }
}
