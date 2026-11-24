using System;
using Gameplay.Entities.Interactive.Items;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class ExtinguisherStanceLogic
    {
        [SerializeField] private float _raiseSharpness = 9f;
        [SerializeField] private float _lowerSharpness = 6f;
        [SerializeField] private float _linger = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _sprayReadyWeight = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _moveFactor = 0.6f;

        private float _sinceWanted = float.MaxValue;

        public float Weight { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsReadyToSpray => Weight >= _sprayReadyWeight;
        public float MoveFactor => Mathf.Lerp(1f, _moveFactor, Weight);

        public void Tick(bool isActive, bool wantsToSpray, ToolItem tool, bool posesBodyHold, float deltaTime)
        {
            _sinceWanted = isActive && wantsToSpray ? 0f : _sinceWanted + deltaTime;
            IsAiming = isActive && _sinceWanted <= _linger;

            float target = IsAiming ? 1f : 0f;
            float sharpness = IsAiming ? _raiseSharpness : _lowerSharpness;
            Weight = Mathf.Lerp(Weight, target, Easing.Damp(sharpness, deltaTime));

            if (isActive && posesBodyHold && tool.HasAimHold)
            {
                Transform item = tool.transform;
                item.localPosition = Vector3.Lerp(tool.BodyHoldPosition, tool.BodyAimPosition, Weight);
                item.localRotation = Quaternion.Slerp(Quaternion.Euler(tool.BodyHoldEuler), Quaternion.Euler(tool.BodyAimEuler), Weight);
            }
        }

        public void Reset()
        {
            _sinceWanted = float.MaxValue;
            IsAiming = false;
            Weight = 0f;
        }
    }
}
