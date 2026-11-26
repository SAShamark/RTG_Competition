using System;
using Unity.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay.Effects.WaterStream
{
    [Serializable]
    public sealed class StreamParticleLayer
    {
        private const uint SeedMask = 0xFFFF;
        private const float SeedRange = 65535f;

        [SerializeField] private ParticleSystem _system;
        [SerializeField] private float _rate = 900f;
        [SerializeField] private Vector2 _size = new(0.03f, 0.08f);
        [SerializeField, Range(0f, 0.2f)] private float _spread = 0.02f;
        [SerializeField] private Vector2 _speedFactor = new(0.96f, 1f);
        [SerializeField] private float _gravityScale = 1f;
        [SerializeField] private Vector2 _dragScale = new(0.95f, 1.15f);

        private NativeArray<ParticleSystem.Particle> _particles;
        private float _debt;

        public void Init()
        {
            _particles = new NativeArray<ParticleSystem.Particle>(_system.main.maxParticles, Allocator.Persistent);
            _system.Play(false);
        }

        public void Dispose() => _particles.Dispose();

        public void Emit(Vector3 origin, Vector3 velocity, float deltaTime)
        {
            var drop = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            float speed = velocity.magnitude;
            _debt += _rate * deltaTime;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                Vector3 launch = (velocity + Random.insideUnitSphere * (speed * _spread)) * Random.Range(_speedFactor.x, _speedFactor.y);
                drop.velocity = launch;
                // The system moves a new drop by v·Δt before it is first drawn, so drops start up to one step behind
                // the nozzle and are seen spread from the nozzle onwards: p = origin − v·Δt·u, u ∈ [0, 1]
                drop.position = origin - launch * (Random.value * deltaTime);
                drop.startSize = Random.Range(_size.x, _size.y);
                _system.Emit(drop, 1);
            }
        }

        public void Integrate(float gravity, float drag, float deltaTime)
        {
            int count = _system.GetParticles(_particles);
            float fall = gravity * _gravityScale * deltaTime;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = _particles[i];
                float share = (particle.randomSeed & SeedMask) / SeedRange;
                float slowdown = drag * Mathf.Lerp(_dragScale.x, _dragScale.y, share) * deltaTime;

                // Same step as the gameplay trajectory, so the water lands where the jet is traced: v ← (v + g·Δt)·(1 − k·Δt)
                Vector3 velocity = particle.velocity;
                velocity.y -= fall;
                particle.velocity = velocity * Mathf.Max(0f, 1f - slowdown);
                _particles[i] = particle;
            }

            _system.SetParticles(_particles, count);
        }
    }
}
