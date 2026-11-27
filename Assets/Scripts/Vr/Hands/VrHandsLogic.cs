using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vr.Hands
{
    [Serializable]
    public sealed class VrHandsLogic
    {
        [SerializeField] private PhysicalHand _left;
        [SerializeField] private PhysicalHand _right;
        [SerializeField] private PhysicalHandSettings _settings;

        public PhysicalHand Left => _left;
        public PhysicalHand Right => _right;

        public PhysicalHand Get(bool isLeft) => isLeft ? _left : _right;

        public void Initialize(Transform leftController, Transform rightController, IReadOnlyList<Collider> ignored)
        {
            _left.Initialize(_settings, leftController);
            _right.Initialize(_settings, rightController);
            _left.IgnoreCollisions(_right.Colliders);
            _left.IgnoreCollisions(ignored);
            _right.IgnoreCollisions(ignored);
        }

        public void FixedTick(float deltaTime)
        {
            _left.FixedTick(deltaTime);
            _right.FixedTick(deltaTime);
        }

        public void Tick(float deltaTime, VrInputLogic input)
        {
            _left.SetVisible(input.IsLeftTracked);
            _right.SetVisible(input.IsRightTracked);
            _left.Tick(deltaTime, input.LeftGrip, input.LeftTrigger);
            _right.Tick(deltaTime, input.RightGrip, input.RightTrigger);
            ApplyHolds();
        }

        public void ApplyHolds()
        {
            _left.ApplyHold();
            _right.ApplyHold();
        }
    }
}
