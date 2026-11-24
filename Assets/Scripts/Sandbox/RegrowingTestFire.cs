using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Sandbox
{
    [RequireComponent(typeof(FireSource))]
    public sealed class RegrowingTestFire : MonoBehaviour
    {
        [SerializeField] private FireSource _fire;
        [SerializeField] private float _restartDelay = 6f;
        [SerializeField, Range(0f, 1f)] private float _restartIntensity = 0.2f;
        [SerializeField] private float _growDelay = 1.5f;
        [SerializeField] private float _growSpeed = 0.12f;

        private float _sinceOut;
        private float _sinceSprayed;
        private float _lastIntensity;

        private void OnEnable()
        {
            _fire.Extinguished += HandleExtinguished;
            _lastIntensity = _fire.Intensity;
        }

        private void OnDisable()
        {
            _fire.Extinguished -= HandleExtinguished;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (_fire.IsOut)
            {
                _sinceOut += deltaTime;
                if (_sinceOut >= _restartDelay && !_fire.IsSuppressedByPowder)
                {
                    _fire.Ignite(_restartIntensity);
                    _sinceSprayed = 0f;
                }

                _lastIntensity = _fire.Intensity;
                return;
            }

            _sinceSprayed = _fire.Intensity < _lastIntensity ? 0f : _sinceSprayed + deltaTime;
            if (_sinceSprayed >= _growDelay)
            {
                _fire.Grow(_growSpeed * deltaTime);
            }

            _lastIntensity = _fire.Intensity;
        }

        private void HandleExtinguished(FireSource fire)
        {
            _sinceOut = 0f;
        }
    }
}
