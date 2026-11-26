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
        [SerializeField] private Transform _aimAnchor;

        private float _sinceWanted = float.MaxValue;
        private ToolItem _carried;

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

            bool posesTool = isActive && posesBodyHold && tool.HasAimHold;
            _carried = posesTool && tool.IsTwoHanded ? tool : null;
            if (posesTool && !tool.IsTwoHanded)
            {
                Transform item = tool.transform;
                item.localPosition = Vector3.Lerp(tool.BodyHoldPosition, tool.BodyAimPosition, Weight);
                item.localRotation = Quaternion.Slerp(Quaternion.Euler(tool.BodyHoldEuler), Quaternion.Euler(tool.BodyAimEuler), Weight);
            }
        }

        // A tool with a grip for each hand is carried by the chest and both hands reach for it. It is placed after the
        // animator has run, so the hand it hangs from at rest never drags it around while it is raised.
        public void LateTick(PlayerBodyIK body)
        {
            if (_carried == null)
            {
                return;
            }

            Place();
            body.Carry(_carried.RightGrip, _carried.LeftGrip, Weight);
            // The tool hangs off the right hand, which has just moved to its grip, so it is put back in place.
            Place();
        }

        private void Place()
        {
            Transform item = _carried.transform;
            Transform socket = item.parent;
            Vector3 holdPosition = socket.TransformPoint(_carried.BodyHoldPosition);
            Quaternion holdRotation = socket.rotation * Quaternion.Euler(_carried.BodyHoldEuler);
            Vector3 aimPosition = _aimAnchor.TransformPoint(_carried.BodyAimPosition);
            Quaternion aimRotation = _aimAnchor.rotation * Quaternion.Euler(_carried.BodyAimEuler);
            item.SetPositionAndRotation(Vector3.Lerp(holdPosition, aimPosition, Weight), Quaternion.Slerp(holdRotation, aimRotation, Weight));
        }

        public void Reset()
        {
            _sinceWanted = float.MaxValue;
            _carried = null;
            IsAiming = false;
            Weight = 0f;
        }
    }
}
