using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay.Effects.WaterStream
{
    [Serializable]
    public sealed class StreamDroplets
    {
        [SerializeField] private ParticleSystem _system;
        [SerializeField] private float _rate = 160f;
        [SerializeField] private float _spread = 1.6f;
        [SerializeField] private float _fullSpreadDistance = 14f;
        [SerializeField] private Vector2 _size = new(0.03f, 0.12f);
        [SerializeField] private Vector2 _speedFactor = new(0.7f, 1f);

        private float _debt;

        public void Init() => _system.Play(false);

        public void Emit(IWaterStreamCurve curve, float cut, float reach, float deltaTime)
        {
            var droplet = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            _debt += _rate * deltaTime;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                float distance = Mathf.Lerp(cut, reach, Random.value);
                float loose = Mathf.Clamp01(distance / _fullSpreadDistance);
                curve.Sample(distance, out Vector3 point, out Vector3 velocity);
                droplet.position = point;
                droplet.startSize = Mathf.Lerp(_size.x, _size.y, loose * Random.value);
                velocity += Random.insideUnitSphere * (_spread * (loose + Random.value * loose));
                droplet.velocity = velocity * Random.Range(_speedFactor.x, _speedFactor.y);
                _system.Emit(droplet, 1);
            }
        }
    }
}
