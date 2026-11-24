using System;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    public enum HandGesture
    {
        None = 0,
        Push = 1,
        Rattle = 2,
        Grab = 3,
        Pry = 4,
        Chop = 5,
        Reach = 6,
        Toss = 7,
    }

    [Serializable]
    public sealed class HandsAnimationControl
    {
        private const float LeftSide = -1f;
        private const float RightSide = 1f;

        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Vector3 _leftRest = new(-0.24f, -0.285f, 0.05f);
        [SerializeField] private Vector3 _rightRest = new(0.24f, -0.285f, 0.05f);
        [SerializeField] private Vector3 _leftRestEuler = new(-8f, 8f, 25f);
        [SerializeField] private Vector3 _rightRestEuler = new(-8f, -8f, -25f);

        [Header("Motion")]
        [SerializeField] private Vector2 _bobAmplitude = new(0.012f, 0.018f);
        [SerializeField] private float _swayAmount = 0.0009f;
        [SerializeField] private float _swaySmoothing = 10f;
        [SerializeField] private float _breathAmplitude = 0.004f;

        [Header("Arm swing")]
        [SerializeField, Range(0f, 1f)] private float _runSwingStart = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _walkSwing = 0.3f;
        [SerializeField] private float _swingReach = 0.05f;
        [SerializeField] private float _swingLift = 0.03f;
        [SerializeField] private float _swingPitch = 2f;
        [SerializeField] private float _runRaise = -0.03f;
        [SerializeField, Range(0f, 1f)] private float _toolSwing = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _fistSwing = 0.5f;

        [Header("Toss")]
        [SerializeField] private float _tossDuration = 0.45f;
        [SerializeField] private float _tossContactTime = 0.2f;
        [SerializeField, Range(0.1f, 0.9f)] private float _tossWindupShare = 0.5f;
        [SerializeField] private Vector3 _tossWindupOffset = new(0.03f, -0.03f, -0.08f);
        [SerializeField] private Vector3 _tossReleaseOffset = new(-0.03f, 0.07f, 0.16f);
        [SerializeField] private float _tossWindupPitch = 8f;
        [SerializeField] private float _tossReleasePitch = -18f;

        [Header("View edge")]
        [SerializeField] private float _viewHalfTan = 0.7f;
        [SerializeField] private float _sleeveRadius = 0.09f;
        [SerializeField] private float _reachCross = 0.02f;

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
        [SerializeField] private Vector3 _carryShoulderGrip = new(0f, 0.03f, 0.03f);
        [SerializeField] private Vector3 _carryKneeGrip = new(0f, 0.01f, 0.05f);
        [SerializeField] private Vector3 _carryLeftElbow = new(-0.25f, -0.6f, 0.35f);
        [SerializeField] private Vector3 _carryRightElbow = new(0.3f, -0.55f, 0.25f);
        [SerializeField] private float _longGestureDuration = 0.8f;
        [SerializeField] private float _gestureDuration = 0.6f;
        [SerializeField] private float _rattleContactTime = 0.16f;
        [SerializeField] private float _contactTime = 0.2f;
        [SerializeField] private float _moveBlendSpeed = 4f;
        [SerializeField] private float _breathFrequency = 1.6f;
        [SerializeField] private float _maxSway = 0.05f;
        [SerializeField] private float _rightBobDown = 0.6f;
        [SerializeField] private float _rightBobUp = 0.4f;
        [SerializeField] private float _aimJitterFrequency = 47f;
        [SerializeField] private float _aimJitterAmplitude = 0.0025f;
        [SerializeField] private Vector3 _sprayKickOffset = new(0f, 0.025f, -0.05f);
        [SerializeField] private Vector3 _sprayKickEuler = new(-9f, 0f, 0f);
        [SerializeField] private float _sprayKickDecay = 9f;
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
        [SerializeField] private Vector3 _fistCenter = new(0f, -0.045f, 0.345f);
        [SerializeField] private MeshFilter _leftMesh;
        [SerializeField] private MeshFilter _rightMesh;
        [SerializeField] private Mesh _leftOpen;
        [SerializeField] private Mesh _leftFist;
        [SerializeField] private Mesh _rightOpen;
        [SerializeField] private Mesh _rightFist;
        [SerializeField] private float _toolBlendSpeed = 9f;
        [SerializeField] private float _toolReleaseSpeed = 3f;
        [SerializeField] private float _gripBlendSpeed = 2.5f;

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
        [SerializeField] private float _chopBounceRecoil = 0.7f;
        [SerializeField] private Vector3 _chopAltWindupOffset = new(0.16f, 0.04f, -0.06f);
        [SerializeField] private Vector3 _chopAltWindupEuler = new(-18f, 30f, -70f);
        [SerializeField] private Vector3 _chopAltStrikeOffset = new(-0.14f, -0.08f, 0.14f);
        [SerializeField] private Vector3 _chopAltStrikeEuler = new(30f, -20f, 35f);

        [SerializeField] private float _reachMaxDistance = 0.6f;

        [SerializeField, Range(0f, 1f)] private float _sputterThreshold = 0.25f;
        [SerializeField] private float _sputterAmplitude = 0.012f;
        [SerializeField] private float _sputterFrequency = 23f;

        private bool _hasAimPose;
        private Vector3 _handAimPosition;
        private Quaternion _handAimRotation = Quaternion.identity;
        private float _stance;
        private bool _isChopAlternate;
        private float _freeze;
        private bool _isChopBounced;
        private Quaternion _reachAim = Quaternion.identity;
        private float _reachWeight;
        private float _tossPitch;
        private float _gripBlend;

        private GestureState _gesture;
        private Transform _leftGrip;
        private float _toolBlend;
        private float _holdBlend;
        private bool _isLeftFist;
        private bool _isRightFist;
        private Transform _carryShoulder;
        private Transform _carryKnee;
        private bool _isCarrying;
        private float _carryBlend;
        private float _moveBlend;
        private Vector2 _sway;
        private bool _isAiming;
        private float _aimBlend;
        private float _aimPressure = 1f;
        private float _sprayKick;
        private bool _hasHandPose;
        private Vector3 _handPosition;
        private Quaternion _handRotation = Quaternion.identity;

        private struct GestureState
        {
            public HandGesture Type;
            public float Time;
            public float Duration;
            public float ContactTime;
            public bool ContactFired;
            public Action OnContact;
            public Vector3 Target;
            public bool IsLeft;
        }

        public bool IsBusy => _gesture.Type != HandGesture.None;

        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;

        public void SetVisible(bool isVisible)
        {
            _leftHand.gameObject.SetActive(isVisible);
            _rightHand.gameObject.SetActive(isVisible);
        }

        public void SetAiming(bool isAiming, float pressure = 1f)
        {
            if (isAiming && !_isAiming)
            {
                _sprayKick = 1f;
            }

            _isAiming = isAiming;
            _aimPressure = pressure;
        }

        public void SetCarrying(bool isCarrying) => _isCarrying = isCarrying;

        public void SetCarryGrips(Transform farShoulder, Transform farKnee)
        {
            _carryShoulder = farShoulder;
            _carryKnee = farKnee;
        }

        public void SetLeftGrip(Transform grip)
        {
            _leftGrip = grip;
        }

        public void SetHandPose(bool hasPose, Vector3 position, Vector3 euler)
        {
            _hasHandPose = hasPose;
            _handPosition = position;
            _handRotation = Quaternion.Euler(euler);
            _hasAimPose = false;
        }

        public void SetAimPose(Vector3 position, Vector3 euler)
        {
            _hasAimPose = true;
            _handAimPosition = position;
            _handAimRotation = Quaternion.Euler(euler);
        }

        public void SetStance(float weight) => _stance = weight;

        public void Play(HandGesture gesture, Action onContact)
        {
            bool isHoldingTool = _leftGrip != null && gesture != HandGesture.Chop && gesture != HandGesture.Toss;
            if (gesture == HandGesture.None || isHoldingTool)
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
                    HandGesture.Toss => _tossDuration,
                    _ => _gestureDuration,
                },
                ContactTime = gesture switch
                {
                    HandGesture.Rattle => _rattleContactTime,
                    HandGesture.Chop => _chopContactTime,
                    HandGesture.Toss => _tossContactTime,
                    _ => _contactTime,
                },
                OnContact = onContact,
            };
        }

        public void Tick(float deltaTime, float normalizedSpeed, float stepPhase, Vector2 lookDelta)
        {
            _moveBlend = Mathf.MoveTowards(_moveBlend, normalizedSpeed, deltaTime * _moveBlendSpeed);
            float bobPhase = stepPhase * Mathf.PI;
            float bobX = Mathf.Sin(bobPhase) * _bobAmplitude.x * _moveBlend;
            float bobY = -Mathf.Abs(Mathf.Cos(bobPhase)) * _bobAmplitude.y * _moveBlend;
            float breath = Mathf.Sin(Time.time * _breathFrequency) * _breathAmplitude * (1f - _moveBlend);

            Vector2 targetSway = -lookDelta * _swayAmount;
            targetSway = Vector2.ClampMagnitude(targetSway, _maxSway);
            _sway = Vector2.Lerp(_sway, targetSway, Easing.Damp(_swaySmoothing, deltaTime));

            Vector3 common = new Vector3(_sway.x, _sway.y + breath, 0f);
            Vector3 left = _leftRest + common + new Vector3(bobX, bobY, 0f);
            Vector3 right = _rightRest + common + new Vector3(bobX, -bobY * _rightBobDown + bobY * _rightBobUp, 0f);
            Vector3 leftEuler = _leftRestEuler;
            Vector3 rightEuler = _rightRestEuler;

            float run = Mathf.InverseLerp(_runSwingStart, 1f, _moveBlend);
            float stride = Mathf.Clamp01(_moveBlend / _runSwingStart) * Mathf.Lerp(_walkSwing, 1f, run);
            float free = (1f - Easing.Smooth(_holdBlend)) * (1f - _carryBlend);
            float swing = Mathf.Sin(bobPhase);
            float leftStride = stride * (1f - _carryBlend);
            float rightStride = stride * Mathf.Lerp(_toolSwing, 1f, free);
            left += Swing(-swing, run) * leftStride;
            right += Swing(swing, run) * rightStride;
            leftEuler.x += swing * _swingPitch * leftStride;
            rightEuler.x -= swing * _swingPitch * rightStride;
            bool isSwingingFists = run > _fistSwing;
            _reachWeight = 0f;
            _tossPitch = 0f;

            _sprayKick = Mathf.MoveTowards(_sprayKick, 0f, deltaTime * _sprayKickDecay);
            _aimBlend = Mathf.MoveTowards(_aimBlend, _isAiming ? 1f : 0f, deltaTime * _aimSpeed);
            float jitter = Mathf.Sin(Time.time * _aimJitterFrequency) * _aimJitterAmplitude * _aimBlend * _aimPressure;
            // Below the threshold the pressure stutters: noise grows as the bottle empties.
            float sputter = _aimBlend * Mathf.Clamp01(1f - _aimPressure / _sputterThreshold) * _sputterAmplitude;
            float sputterNoise = (Mathf.PerlinNoise(Time.time * _sputterFrequency, 0f) - 0.5f) * 2f * sputter;
            Vector3 sprayShake = new Vector3(jitter, jitter * 0.5f + sputterNoise, -Mathf.Abs(sputterNoise)) + _sprayKickOffset * _sprayKick;
            if (_aimBlend > 0f)
            {
                right += _aimOffset * _aimBlend + sprayShake;
                rightEuler += _aimEuler * _aimBlend + _sprayKickEuler * _sprayKick;
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
            bool isTwoHanded = hasGrip && _hasHandPose;
            _holdBlend = Mathf.MoveTowards(_holdBlend, hasGrip ? 1f : 0f, deltaTime * _holdBlendSpeed);
            _toolBlend = Mathf.MoveTowards(_toolBlend, isTwoHanded ? 1f : 0f, deltaTime * (isTwoHanded ? _toolBlendSpeed : _toolReleaseSpeed));
            float hold = Easing.Smooth(_holdBlend);
            float twoHands = Easing.Smooth(_toolBlend);
            if (hold > 0f)
            {
                right += _oneHandRightOffset * hold;
                rightEuler += _oneHandRightEuler * hold;
                left += _oneHandLeftOffset * (hold * (1f - twoHands));
            }

            float pose = _hasHandPose ? hold : 0f;
            if (pose > 0f)
            {
                Vector3 posed = _hasAimPose ? Vector3.Lerp(_handPosition, _handAimPosition, _stance) : _handPosition;
                right = Vector3.Lerp(right, posed + common + new Vector3(bobX, bobY, 0f) + sprayShake, pose);
            }

            bool hasCarryGrips = _carryShoulder != null && _carryBlend > 0f;
            bool isReachClosed = _gesture.Type == HandGesture.Reach && _gesture.ContactFired;
            bool isLeftReaching = _gesture.Type == HandGesture.Reach && _gesture.IsLeft;
            ApplyGesture(deltaTime, ref left, ref right, ref leftEuler, ref rightEuler);

            // The off hand only closes on the tool once the reaching hand has brought it back.
            float gripTarget = twoHands * (1f - _reachWeight);
            _gripBlend = Mathf.MoveTowards(_gripBlend, gripTarget, deltaTime * (gripTarget < _gripBlend ? _toolBlendSpeed : _gripBlendSpeed));
            float gripWeight = Easing.Smooth(_gripBlend);
            SetLeftFist(gripWeight > _fistSwing || hasCarryGrips || (isSwingingFists && !isLeftReaching) || (isReachClosed && isLeftReaching));
            SetRightFist(hasGrip || hasCarryGrips || (isSwingingFists && free > _fistSwing) || (isReachClosed && !isLeftReaching));

            _leftHand.localPosition = left;
            _rightHand.localPosition = right;
            _leftHand.localRotation = Quaternion.Euler(leftEuler);
            _rightHand.localRotation = Quaternion.Euler(rightEuler);
            if (pose > 0f)
            {
                Quaternion held = _hasAimPose ? Quaternion.Slerp(_handRotation, _handAimRotation, _stance) : _handRotation;
                Quaternion posed = held * Quaternion.Euler(_sprayKickEuler * _sprayKick);
                // While the hand returns from a reach it turns straight from the reach aim into the held pose: fixed endpoints keep the arc from flipping.
                bool isRightReaching = _gesture.Type == HandGesture.Reach && !_gesture.IsLeft;
                _rightHand.localRotation = isRightReaching ? posed : Quaternion.Slerp(_rightHand.localRotation, posed, pose);
            }

            _rightHand.localRotation *= Quaternion.Euler(_tossPitch, 0f, 0f);
            if (!isLeftReaching)
            {
                _rightHand.localRotation = Quaternion.Slerp(_rightHand.localRotation, _reachAim, _reachWeight);
            }

            if (_leftGrip != null && gripWeight > 0f)
            {
                AttachLeftToGrip(gripWeight);
            }

            if (isLeftReaching)
            {
                _leftHand.localRotation = Quaternion.Slerp(_leftHand.localRotation, _reachAim, _reachWeight);
            }

            if (hasCarryGrips)
            {
                float k = Easing.Smooth(_carryBlend);
                Transform space = _leftHand.parent;
                AttachToBody(_leftHand, space.InverseTransformPoint(_carryShoulder.position) + _carryShoulderGrip, _carryLeftElbow, k);
                AttachToBody(_rightHand, space.InverseTransformPoint(_carryKnee.position) + _carryKneeGrip, _carryRightElbow, k);
            }
        }

        private Vector3 Swing(float swing, float run)
        {
            return new Vector3(0f, Mathf.Max(0f, swing) * _swingLift + run * _runRaise, swing * _swingReach);
        }

        // The sleeve is cut off at the arm's origin, so the origin only travels until it would rise above the bottom edge of the view.
        private void ApplyReach(float reach, Vector3 rest, float roll, float side, ref Vector3 position)
        {
            Vector3 target = _gesture.Target;
            target.x = side * Mathf.Max(side * target.x, -_reachCross);
            Vector3 toTarget = target - rest;
            Vector3 direction = toTarget.normalized;
            float climb = direction.y + _viewHalfTan * direction.z;
            float hidden = -(rest.y + _sleeveRadius + _viewHalfTan * rest.z);
            float limit = climb > 0f ? Mathf.Max(0f, hidden / climb) : _reachMaxDistance;
            float extension = Mathf.Clamp(toTarget.magnitude - _fistCenter.z, 0f, Mathf.Min(limit, _reachMaxDistance));
            position = Vector3.Lerp(position, rest + direction * extension, reach);
            _reachAim = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, roll);
            _reachWeight = reach;
        }

        // Forearm runs from the elbow under the body; palm faces up so the curled fingers wrap the far side.
        private void AttachToBody(Transform hand, Vector3 grip, Vector3 elbow, float weight)
        {
            Quaternion aim = Quaternion.LookRotation(grip - elbow, Vector3.down);
            Vector3 position = grip - aim * _fistCenter;
            hand.localPosition = Vector3.Lerp(hand.localPosition, position, weight);
            hand.localRotation = Quaternion.Slerp(hand.localRotation, aim, weight);
        }

        private void SetRightFist(bool isFist)
        {
            if (_isRightFist == isFist)
            {
                return;
            }

            _isRightFist = isFist;
            _rightMesh.sharedMesh = isFist ? _rightFist : _rightOpen;
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
            Vector3 reach = _hasHandPose ? space.InverseTransformDirection(_leftGrip.forward) : _rightHand.localRotation * Vector3.forward;
            Vector3 forward = Vector3.ProjectOnPlane(reach, across).normalized;
            Quaternion aim = Quaternion.LookRotation(forward, Vector3.Cross(forward, across));
            Vector3 position = grip - aim * _fistCenter;

            _leftHand.localPosition = Vector3.Lerp(_leftHand.localPosition, position, weight);
            _leftHand.localRotation = Quaternion.Slerp(_leftHand.localRotation, aim, weight);
        }

        public void SetChopTiming(float contact, float duration, float windup)
        {
            _chopContactTime = contact;
            _chopDuration = duration;
            _chopWindupTime = windup;
        }

        public void PlayReach(Vector3 worldTarget, float contactTime, float duration, Action onContact)
        {
            bool isLeft = _leftGrip != null;
            Transform rig = _rightHand.parent;
            _gesture = new GestureState
            {
                Type = HandGesture.Reach,
                Duration = duration,
                ContactTime = contactTime,
                OnContact = onContact,
                Target = rig.InverseTransformPoint(worldTarget),
                IsLeft = isLeft,
            };
        }

        public void SetChopVariant(bool isAlternate)
        {
            _isChopAlternate = isAlternate;
            _isChopBounced = false;
        }

        public void Freeze(float duration) => _freeze = duration;

        public void Bounce() => _isChopBounced = true;

        private void ApplyChop(float t, ref Vector3 right, ref Vector3 rightEuler)
        {
            Vector3 windupOffset = _isChopAlternate ? _chopAltWindupOffset : _chopWindupOffset;
            Vector3 windupEuler = _isChopAlternate ? _chopAltWindupEuler : _chopWindupEuler;
            Vector3 strikeOffset = _isChopAlternate ? _chopAltStrikeOffset : _chopStrikeOffset;
            Vector3 strikeEuler = _isChopAlternate ? _chopAltStrikeEuler : _chopStrikeEuler;
            Vector3 offset;
            Vector3 euler;
            if (t < _chopWindupTime)
            {
                float k = Easing.OutQuad(t / _chopWindupTime);
                offset = windupOffset * k;
                euler = windupEuler * k;
            }
            else if (t < _chopContactTime)
            {
                float k = Easing.InQuad(Mathf.InverseLerp(_chopWindupTime, _chopContactTime, t));
                offset = Vector3.Lerp(windupOffset, strikeOffset, k);
                euler = Vector3.Lerp(windupEuler, strikeEuler, k);
            }
            else
            {
                float holdEnd = _chopContactTime + _chopHoldTime;
                float recoil = t < holdEnd ? Mathf.Sin(Mathf.InverseLerp(_chopContactTime, holdEnd, t) * Mathf.PI) * (_isChopBounced ? _chopBounceRecoil : _chopRecoil) : 0f;
                float k = 1f - Easing.Smooth(Mathf.InverseLerp(holdEnd, _chopDuration, t));
                offset = strikeOffset * (k - recoil * k);
                euler = strikeEuler * (k - recoil * k);
            }

            right += offset;
            rightEuler += euler;
        }

        private void ApplyToss(float t, ref Vector3 right)
        {
            float windup = _gesture.ContactTime * _tossWindupShare;
            Vector3 offset;
            if (t < windup)
            {
                float k = Easing.OutQuad(t / windup);
                offset = _tossWindupOffset * k;
                _tossPitch = _tossWindupPitch * k;
            }
            else if (t < _gesture.ContactTime)
            {
                float k = Easing.InQuad(Mathf.InverseLerp(windup, _gesture.ContactTime, t));
                offset = Vector3.Lerp(_tossWindupOffset, _tossReleaseOffset, k);
                _tossPitch = Mathf.Lerp(_tossWindupPitch, _tossReleasePitch, k);
            }
            else
            {
                float k = 1f - Easing.Smooth(Mathf.InverseLerp(_gesture.ContactTime, _gesture.Duration, t));
                offset = _tossReleaseOffset * k;
                _tossPitch = _tossReleasePitch * k;
            }

            right += offset;
        }

        private void ApplyGesture(float deltaTime, ref Vector3 left, ref Vector3 right, ref Vector3 leftEuler, ref Vector3 rightEuler)
        {
            if (_gesture.Type == HandGesture.None)
            {
                return;
            }

            if (_freeze > 0f)
            {
                _freeze -= deltaTime;
                deltaTime = 0f;
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

            if (_gesture.Type == HandGesture.Toss)
            {
                ApplyToss(t, ref right);
                if (t >= _gesture.Duration)
                {
                    _gesture = default;
                }

                return;
            }

            float release = _gesture.Type == HandGesture.Reach ? _gesture.ContactTime : _gesture.ContactTime + _releaseDelay;
            float reach = t < _gesture.ContactTime
                ? Easing.OutQuad(t / _gesture.ContactTime)
                : 1f - Easing.Smooth(Mathf.InverseLerp(release, _gesture.Duration, t));

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

                case HandGesture.Reach when _gesture.IsLeft:
                    ApplyReach(reach, _leftRest, _leftRestEuler.z, LeftSide, ref left);
                    break;

                case HandGesture.Reach:
                    ApplyReach(reach, _rightRest, _rightRestEuler.z, RightSide, ref right);
                    break;
            }

            if (t >= _gesture.Duration)
            {
                _gesture = default;
            }
        }


    }
}
