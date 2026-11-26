using System;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    [Serializable]
    public sealed class HeadLookLogic
    {
        private const float MinFacingSqr = 0.001f;

        [SerializeField] private float _faceSharpness = 5f;
        [SerializeField] private float _lookLimit = 55f;
        [SerializeField] private float _lookSharpness = 6f;
        [SerializeField] private float _lookRelease = 0.3f;

        private Transform _body;
        private Transform _head;
        private Vector3 _headForward;
        private Vector3 _lookPoint;
        private float _sinceLooked = float.MaxValue;
        private Quaternion _lookTurn = Quaternion.identity;

        public void Init(Transform body, Transform head)
        {
            _body = body;
            _head = head;
            _headForward = head.InverseTransformDirection(body.forward);
        }

        public void Face(Vector3 point, float deltaTime)
        {
            Vector3 planar = Vector3.ProjectOnPlane(point - _body.position, Vector3.up);
            if (planar.sqrMagnitude < MinFacingSqr)
            {
                return;
            }

            _lookPoint = point;
            _sinceLooked = 0f;
            _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(planar), Easing.Damp(_faceSharpness, deltaTime));
        }

        public void Tick(float deltaTime)
        {
            _sinceLooked += deltaTime;
            Quaternion target = Quaternion.identity;
            if (_sinceLooked <= _lookRelease)
            {
                Quaternion full = Quaternion.FromToRotation(_head.TransformDirection(_headForward), _lookPoint - _head.position);
                target = Quaternion.RotateTowards(Quaternion.identity, full, _lookLimit);
            }

            _lookTurn = Quaternion.Slerp(_lookTurn, target, Easing.Damp(_lookSharpness, deltaTime));
            _head.rotation = _lookTurn * _head.rotation;
        }
    }
}
