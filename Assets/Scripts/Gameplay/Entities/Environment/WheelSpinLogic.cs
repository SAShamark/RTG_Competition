using System;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [Serializable]
    public sealed class WheelSpinLogic
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform[] _wheels;
        [SerializeField] private float _radius = 0.55f;

        private Vector3 _last;

        public void Init() => _last = _body.position;

        public void Tick()
        {
            Vector3 delta = _body.position - _last;
            _last = _body.position;
            float degrees = Vector3.Dot(delta, _body.forward) / _radius * Mathf.Rad2Deg;
            foreach (Transform wheel in _wheels)
            {
                wheel.Rotate(Vector3.right, degrees, Space.Self);
            }
        }
    }
}
