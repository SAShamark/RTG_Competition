using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay.Effects.WaterStream
{
    [Serializable]
    public sealed class WaterImpactFx
    {
        [SerializeField] private float _radius = 0.2f;

        [Header("Splash")]
        [SerializeField] private ParticleSystem _splash;
        [SerializeField] private float _splashRate = 140f;
        [SerializeField] private Vector2 _splashSpeed = new(2f, 6f);
        [SerializeField, Range(0f, 1f)] private float _splashLift = 0.45f;

        [Header("Mist")]
        [SerializeField] private ParticleSystem _mist;
        [SerializeField] private float _mistRate = 30f;
        [SerializeField] private Vector2 _mistSpeed = new(0.4f, 1.4f);
        [SerializeField, Range(0f, 1f)] private float _mistLift = 0.3f;

        [Header("Spray fan (optional)")]
        [SerializeField] private ParticleSystem _spray;
        [SerializeField] private float _sprayRate = 45f;
        [SerializeField] private Vector2 _spraySpeed = new(4f, 9f);
        [SerializeField, Range(0f, 1f)] private float _sprayLift = 0.2f;

        [Header("Steam")]
        [SerializeField] private ParticleSystem _steam;
        [SerializeField] private float _steamRate = 24f;
        [SerializeField] private Vector2 _steamSpeed = new(0.6f, 1.8f);
        [SerializeField, Range(0f, 1f)] private float _steamDrift = 0.35f;

        [Header("Foam ring (optional)")]
        [SerializeField] private Transform _ring;
        [SerializeField] private float _ringLift = 0.02f;

        private float _splashDebt;
        private float _mistDebt;
        private float _steamDebt;
        private float _sprayDebt;

        public void Init()
        {
            if (_spray != null)
            {
                _spray.Play(false);
            }

            _splash.Play(false);
            _mist.Play(false);
            _steam.Play(false);
            Hide();
        }

        public void Hide()
        {
            if (_ring != null)
            {
                _ring.gameObject.SetActive(false);
            }
        }

        public void Tick(Vector3 point, Vector3 normal, bool isLanded, float deltaTime)
        {
            if (!isLanded)
            {
                Hide();
                return;
            }

            Emit(_splash, ref _splashDebt, _splashRate, _splashSpeed, _splashLift, point, normal, deltaTime);
            Emit(_mist, ref _mistDebt, _mistRate, _mistSpeed, _mistLift, point, normal, deltaTime);
            if (_spray != null)
            {
                Emit(_spray, ref _sprayDebt, _sprayRate, _spraySpeed, _sprayLift, point, normal, deltaTime);
            }

            if (_ring != null)
            {
                _ring.gameObject.SetActive(true);
                _ring.SetPositionAndRotation(point + normal * _ringLift, Quaternion.LookRotation(-normal));
            }
        }

        public void EmitSteam(Vector3 point, float deltaTime)
        {
            var puff = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            _steamDebt += _steamRate * deltaTime;
            while (_steamDebt >= 1f)
            {
                _steamDebt -= 1f;
                Vector3 direction = (Vector3.up + Random.insideUnitSphere * _steamDrift).normalized;
                puff.position = point + Random.insideUnitSphere * _radius;
                puff.velocity = direction * Random.Range(_steamSpeed.x, _steamSpeed.y);
                _steam.Emit(puff, 1);
            }
        }

        // Water that hits a surface sheets out along it instead of bouncing straight back.
        private void Emit(ParticleSystem system, ref float debt, float rate, Vector2 speed, float lift, Vector3 point, Vector3 normal, float deltaTime)
        {
            var burst = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            debt += rate * deltaTime;
            while (debt >= 1f)
            {
                debt -= 1f;
                Vector3 along = Vector3.ProjectOnPlane(Random.onUnitSphere, normal).normalized;
                Vector3 direction = (along + normal * lift).normalized;
                burst.position = point + direction * Random.Range(0f, _radius);
                burst.velocity = direction * Random.Range(speed.x, speed.y);
                system.Emit(burst, 1);
            }
        }
    }
}
