using Services;
using System;
using UnityEngine;

namespace Gameplay.Entities.Player
{
    public enum HandGesture
    {
        None = 0,
        Push = 1,
        Rattle = 2,
        Grab = 3,
        Pry = 4,
        Chop = 5,
    }

    [Serializable]
    public sealed class HandsAnimationControl
    {
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Vector3 _leftRest = new(-0.24f, -0.285f, 0.05f);
        [SerializeField] private Vector3 _rightRest = new(0.24f, -0.285f, 0.05f);
        [SerializeField] private Vector3 _leftRestEuler = new(-8f, 8f, 25f);
        [SerializeField] private Vector3 _rightRestEuler = new(-8f, -8f, -25f);

        [Header("Motion")]
        [SerializeField] private float _bobFrequency = 1.9f;
        [SerializeField] private Vector2 _bobAmplitude = new(0.012f, 0.018f);
        [SerializeField] private float _swayAmount = 0.0009f;
        [SerializeField] private float _swaySmoothing = 10f;
        [SerializeField] private float _breathAmplitude = 0.004f;

        [Header("Aim (spraying)")]
        [SerializeField] private Vector3 _aimOffset = new(-0.12f, 0.09f, 0.12f);
        [SerializeField] private Vector3 _aimEuler = new(-6f, -10f, 20f);
        [SerializeField] private float _aimSpeed = 8f;

        [Header("Carry (resident in the arms)")]
        [SerializeField] private Vector3 _carryLeftOffset = new(-0.06f, -0.2f, 0.2f);
        [SerializeField] private Vector3 _carryRightOffset = new(0.06f, -0.2f, 0.2f);
        [SerializeField] private Vector3 _carryLeftEuler = new(-5f, 15f, 70f);
        [SerializeField] private Vector3 _carryRightEuler = new(-5f, -15f, -70f);
        [SerializeField] private float _carrySpeed = 5f;
        [SerializeField] private float _longGestureDuration = 0.8f;
        [SerializeField] private float _gestureDuration = 0.6f;
        [SerializeField] private float _rattleContactTime = 0.16f;
        [SerializeField] private float _contactTime = 0.2f;
        [SerializeField] private float _moveBlendSpeed = 4f;
        [SerializeField] private float _runBobMultiplier = 1.6f;
        [SerializeField] private float _breathFrequency = 1.6f;
        [SerializeField] private float _maxSway = 0.05f;
        [SerializeField] private float _rightBobDown = 0.6f;
        [SerializeField] private float _rightBobUp = 0.4f;
        [SerializeField] private float _aimJitterFrequency = 47f;
        [SerializeField] private float _aimJitterAmplitude = 0.0025f;
        [SerializeField] private Vector3 _aimLeftOffset = new Vector3(0.02f, 0.01f, 0.02f);
        [SerializeField] private float _releaseDelay = 0.12f;
        [SerializeField] private Vector3 _pushRightOffset = new Vector3(-0.1f, 0.13f, 0.3f);
        [SerializeField] private Vector3 _pushRightEuler = new Vector3(-12f, -12f, -55f);
        [SerializeField] private Vector3 _pushLeftOffset = new Vector3(0f, -0.04f, -0.03f);
        [SerializeField] private float _rattleSettleTime = 0.2f;
        [SerializeField] private float _rattleFrequency = 50f;
        [SerializeField] private float _rattleAmplitude = 0.025f;
        [SerializeField] private float _rattleTwist = 400f;
        [SerializeField] private Vector3 _rattleRightOffset = new Vector3(-0.1f, 0.1f, 0.28f);
        [SerializeField] private Vector3 _rattleRightEuler = new Vector3(-10f, -15f, -65f);
        [SerializeField] private float _pryFrequency = 22f;
        [SerializeField] private float _pryAmplitude = 0.03f;
        [SerializeField] private float _pryTwist = 300f;
        [SerializeField] private Vector3 _pryRightOffset = new Vector3(-0.14f, 0.1f, 0.3f);
        [SerializeField] private Vector3 _pryRightEuler = new Vector3(-10f, -20f, -40f);
        [SerializeField] private Vector3 _pryLeftOffset = new Vector3(0.12f, 0.08f, 0.22f);
        [SerializeField] private Vector3 _pryLeftEuler = new Vector3(-10f, 20f, 30f);
        [SerializeField] private Vector3 _grabRightOffset = new Vector3(-0.05f, -0.06f, 0.3f);
        [SerializeField] private Vector3 _grabRightEuler = new Vector3(25f, -8f, -15f);

        [Header("Two-handed tool")]
        [SerializeField] private Vector3 _oneHandRightOffset = new(0.08f, -0.1f, 0.2f);
        [SerializeField] private Vector3 _oneHandRightEuler = new(-25f, -6f, -62f);
        [SerializeField] private Vector3 _oneHandLeftOffset = new(-0.05f, -0.25f, -0.05f);
        [SerializeField] private float _holdBlendSpeed = 5f;
        [SerializeField] private Vector3 _toolRightOffset = new(-0.04f, 0.06f, 0.12f);
        [SerializeField] private Vector3 _toolRightEuler = new(-6f, -8f, -35f);
        [SerializeField] private Vector3 _fistCenter = new(0f, -0.045f, 0.345f);
        [SerializeField] private MeshFilter _leftMesh;
        [SerializeField] private MeshFilter _rightMesh;
        [SerializeField] private Mesh _leftOpen;
        [SerializeField] private Mesh _leftFist;
        [SerializeField] private Mesh _rightOpen;
        [SerializeField] private Mesh _rightFist;
        [SerializeField] private float _toolBlendSpeed = 9f;

        [Header("Chop")]
        [SerializeField] private float _chopDuration = 0.6f;
        [SerializeField] private float _chopWindupTime = 0.18f;
        [SerializeField] private float _chopContactTime = 0.27f;
        [SerializeField] private float _chopHoldTime = 0.08f;
        [SerializeField] private Vector3 _chopWindupOffset = new(0.12f, -0.04f, -0.04f);
        [SerializeField] private Vector3 _chopWindupEuler = new(-22f, 22f, -30f);
        [SerializeField] private Vector3 _chopStrikeOffset = new(-0.06f, -0.06f, 0.16f);
        [SerializeField] private Vector3 _chopStrikeEuler = new(40f, -4f, -6f);
        [SerializeField] private float _chopRecoil = 0.25f;

        private GestureState _gesture;
        private Transform _leftGrip;
        private float _toolBlend;
        private float _holdBlend;
        private bool _isLeftFist;
        private bool _isCarrying;
        private float _carryBlend;
        private float _bobPhase;
        private float _moveBlend;
        private Vector2 _sway;
        private bool _isAiming;
        private float _aimBlend;

        private struct GestureState
        {
            public HandGesture Type;
            public float Time;
            public float Duration;
            public float ContactTime;
            public bool ContactFired;
            public Action OnContact;
        }

        public bool IsBusy => _gesture.Type != HandGesture.None;

        public void SetVisible(bool isVisible)
        {
            _leftHand.gameObject.SetActive(isVisible);
            _rightHand.gameObject.SetActive(isVisible);
        }

        public void SetAiming(bool isAiming) => _isAiming = isAiming;

        public void SetCarrying(bool isCarrying) => _isCarrying = isCarrying;

        public void SetLeftGrip(Transform grip)
        {
            _leftGrip = grip;
            _rightMesh.sharedMesh = grip != null ? _rightFist : _rightOpen;
        }

        public void Play(HandGesture gesture, Action onContact)
        {
            if (gesture == HandGesture.None)
            {
                onContact?.Invoke();
                return;
            }

            _gesture = new GestureState
            {
                Type = gesture,
                Time = 0f,
                Duration = gesture switch
                {
                    HandGesture.Rattle or HandGesture.Pry => _longGestureDuration,
                    HandGesture.Chop => _chopDuration,
                    _ => _gestureDuration,
                },
                ContactTime = gesture switch
                {
                    HandGesture.Rattle => _rattleContactTime,
                    HandGesture.Chop => _chopContactTime,
                    _ => _contactTime,
                },
                OnContact = onContact,
            };
        }

        public void Tick(float deltaTime, float normalizedSpeed, Vector2 lookDelta)
        {
            _moveBlend = Mathf.MoveTowards(_moveBlend, normalizedSpeed, deltaTime * _moveBlendSpeed);
            _bobPhase += deltaTime * _bobFrequency * Mathf.Lerp(1f, _runBobMultiplier, normalizedSpeed) * Mathf.PI * 2f;
            float bobX = Mathf.Sin(_bobPhase) * _bobAmplitude.x * _moveBlend;
            float bobY = -Mathf.Abs(Mathf.Cos(_bobPhase)) * _bobAmplitude.y * _moveBlend;
            float breath = Mathf.Sin(Time.time * _breathFrequency) * _breathAmplitude * (1f - _moveBlend);

            Vector2 targetSway = -lookDelta * _swayAmount;
            targetSway = Vector2.ClampMagnitude(targetSway, _maxSway);
            _sway = Vector2.Lerp(_sway, targetSway, 1f - Mathf.Exp(-_swaySmoothing * deltaTime));

            Vector3 common = new Vector3(_sway.x, _sway.y + breath, 0f);
            Vector3 left = _leftRest + common + new Vector3(bobX, bobY, 0f);
            Vector3 right = _rightRest + common + new Vector3(bobX, -bobY * _rightBobDown + bobY * _rightBobUp, 0f);
            Vector3 leftEuler = _leftRestEuler;
            Vector3 rightEuler = _rightRestEuler;

            _aimBlend = Mathf.MoveTowards(_aimBlend, _isAiming ? 1f : 0f, deltaTime * _aimSpeed);
            if (_aimBlend > 0f)
            {
                float jitter = Mathf.Sin(Time.time * _aimJitterFrequency) * _aimJitterAmplitude * _aimBlend;
                right += _aimOffset * _aimBlend + new Vector3(jitter, jitter * 0.5f, 0f);
                rightEuler += _aimEuler * _aimBlend;
                left += _aimLeftOffset * _aimBlend;
            }

            _carryBlend = Mathf.MoveTowards(_carryBlend, _isCarrying ? 1f : 0f, deltaTime * _carrySpeed);
            if (_carryBlend > 0f)
            {
                float k = Easing.Smooth(_carryBlend);
                left += _carryLeftOffset * k;
                right += _carryRightOffset * k;
                leftEuler += _carryLeftEuler * k;
                rightEuler += _carryRightEuler * k;
            }

            bool hasGrip = _leftGrip != null && _leftGrip.gameObject.activeInHierarchy;
            bool isTwoHanded = hasGrip && _gesture.Type == HandGesture.Chop;
            _holdBlend = Mathf.MoveTowards(_holdBlend, hasGrip ? 1f : 0f, deltaTime * _holdBlendSpeed);
            _toolBlend = Mathf.MoveTowards(_toolBlend, isTwoHanded ? 1f : 0f, deltaTime * _toolBlendSpeed);
            float hold = Easing.Smooth(_holdBlend);
            float twoHands = Easing.Smooth(_toolBlend);
            if (hold > 0f)
            {
                right += Vector3.Lerp(_oneHandRightOffset, _toolRightOffset, twoHands) * hold;
                rightEuler += Vector3.Lerp(_oneHandRightEuler, _toolRightEuler, twoHands) * hold;
                left += _oneHandLeftOffset * (hold * (1f - twoHands));
            }

            SetLeftFist(twoHands > 0f);

            ApplyGesture(deltaTime, ref left, ref right, ref leftEuler, ref rightEuler);

            _leftHand.localPosition = left;
            _rightHand.localPosition = right;
            _leftHand.localRotation = Quaternion.Euler(leftEuler);
            _rightHand.localRotation = Quaternion.Euler(rightEuler);

            if (hasGrip && twoHands > 0f)
            {
                AttachLeftToGrip(twoHands);
            }
        }

        private void SetLeftFist(bool isFist)
        {
            if (_isLeftFist == isFist)
            {
                return;
            }

            _isLeftFist = isFist;
            _leftMesh.sharedMesh = isFist ? _leftFist : _leftOpen;
        }

        private void AttachLeftToGrip(float weight)
        {
            Transform space = _leftHand.parent;
            Vector3 grip = space.InverseTransformPoint(_leftGrip.position);
            Vector3 across = space.InverseTransformDirection(_leftGrip.up);
            Vector3 forward = Vector3.ProjectOnPlane(_rightHand.localRotation * Vector3.forward, across).normalized;
            Quaternion aim = Quaternion.LookRotation(forward, Vector3.Cross(forward, across));
            Vector3 position = grip - aim * _fistCenter;

            _leftHand.localPosition = Vector3.Lerp(_leftHand.localPosition, position, weight);
            _leftHand.localRotation = Quaternion.Slerp(_leftHand.localRotation, aim, weight);
        }

        private void ApplyChop(float t, ref Vector3 right, ref Vector3 rightEuler)
        {
            Vector3 offset;
            Vector3 euler;
            if (t < _chopWindupTime)
            {
                float k = Easing.OutQuad(t / _chopWindupTime);
                offset = _chopWindupOffset * k;
                euler = _chopWindupEuler * k;
            }
            else if (t < _chopContactTime)
            {
                float k = Easing.InQuad(Mathf.InverseLerp(_chopWindupTime, _chopContactTime, t));
                offset = Vector3.Lerp(_chopWindupOffset, _chopStrikeOffset, k);
                euler = Vector3.Lerp(_chopWindupEuler, _chopStrikeEuler, k);
            }
            else
            {
                float holdEnd = _chopContactTime + _chopHoldTime;
                float recoil = t < holdEnd ? Mathf.Sin(Mathf.InverseLerp(_chopContactTime, holdEnd, t) * Mathf.PI) * _chopRecoil : 0f;
                float k = 1f - Easing.Smooth(Mathf.InverseLerp(holdEnd, _chopDuration, t));
                offset = _chopStrikeOffset * (k - recoil * k);
                euler = _chopStrikeEuler * (k - recoil * k);
            }

            right += offset;
            rightEuler += euler;
        }

        private void ApplyGesture(float deltaTime, ref Vector3 left, ref Vector3 right, ref Vector3 leftEuler, ref Vector3 rightEuler)
        {
            if (_gesture.Type == HandGesture.None)
            {
                return;
            }

            _gesture.Time += deltaTime;
            float t = _gesture.Time;

            if (!_gesture.ContactFired && t >= _gesture.ContactTime)
            {
                _gesture.ContactFired = true;
                _gesture.OnContact?.Invoke();
            }

            if (_gesture.Type == HandGesture.Chop)
            {
                ApplyChop(t, ref right, ref rightEuler);
                if (t >= _gesture.Duration)
                {
                    _gesture = default;
                }

                return;
            }

            float reach = t < _gesture.ContactTime
                ? Easing.OutQuad(t / _gesture.ContactTime)
                : 1f - Easing.Smooth(Mathf.InverseLerp(_gesture.ContactTime + _releaseDelay, _gesture.Duration, t));

            switch (_gesture.Type)
            {
                case HandGesture.Push:
                    right += _pushRightOffset * reach;
                    rightEuler += _pushRightEuler * reach;
                    left += _pushLeftOffset * reach;
                    break;

                case HandGesture.Rattle:
                    float shake = t > _gesture.ContactTime && t < _gesture.Duration - _rattleSettleTime
                        ? Mathf.Sin((t - _gesture.ContactTime) * _rattleFrequency) * _rattleAmplitude
                        : 0f;
                    right += (_rattleRightOffset + Vector3.right * shake) * reach;
                    rightEuler += (_rattleRightEuler + Vector3.forward * (shake * _rattleTwist)) * reach;
                    break;

                case HandGesture.Pry:
                    float lever = t > _gesture.ContactTime ? Mathf.Sin((t - _gesture.ContactTime) * _pryFrequency) * _pryAmplitude : 0f;
                    right += (_pryRightOffset + Vector3.up * lever) * reach;
                    rightEuler += (_pryRightEuler + Vector3.right * (lever * _pryTwist)) * reach;
                    left += _pryLeftOffset * reach;
                    leftEuler += _pryLeftEuler * reach;
                    break;

                case HandGesture.Grab:
                    right += _grabRightOffset * reach;
                    rightEuler += _grabRightEuler * reach;
                    break;
            }

            if (t >= _gesture.Duration)
            {
                _gesture = default;
            }
        }


    }
}
