using System;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class WheelSpinLogic
    {
        private const float FullTurn = 360f;
        private const float Rolling = 1f;

        [SerializeField] private Transform _body;
        [SerializeField] private Transform[] _wheels;
        [SerializeField] private float _radius = 0.55f;

        private Quaternion[] _rest;
        private bool[] _isFront;
        private float[] _spin;
        private Vector3 _last;

        public float Steer { get; set; }
        public float RearSpin { get; set; } = Rolling;

        public void Init()
        {
            _last = _body.position;
            _rest = new Quaternion[_wheels.Length];
            _isFront = new bool[_wheels.Length];
            _spin = new float[_wheels.Length];
            for (int i = 0; i < _wheels.Length; i++)
            {
                _rest[i] = _wheels[i].localRotation;
                _isFront[i] = _body.InverseTransformPoint(_wheels[i].position).z > 0f;
            }
        }

        public void Tick()
        {
            Vector3 delta = _body.position - _last;
            _last = _body.position;
            float degrees = Vector3.Dot(delta, _body.forward) / _radius * Mathf.Rad2Deg;
            Quaternion steer = Quaternion.Euler(0f, Steer, 0f);
            for (int i = 0; i < _wheels.Length; i++)
            {
                _spin[i] = Mathf.Repeat(_spin[i] + degrees * (_isFront[i] ? Rolling : RearSpin), FullTurn);
                Quaternion rolled = _rest[i] * Quaternion.Euler(_spin[i], 0f, 0f);
                _wheels[i].localRotation = _isFront[i] ? steer * rolled : rolled;
            }
        }
    }
}
