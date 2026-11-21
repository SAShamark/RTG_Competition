using System;
using UnityEngine;

namespace Game.Player
{
    public enum HandGesture
    {
        None = 0,
        Push = 1,
        Rattle = 2,
        Grab = 3,
        Pry = 4,
    }

    [Serializable]
    public class HandsAnimationControl
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
        [SerializeField] private Vector3 _carryLeftOffset = new(0.06f, 0.02f, 0.22f);
        [SerializeField] private Vector3 _carryRightOffset = new(-0.06f, 0.02f, 0.22f);
        [SerializeField] private Vector3 _carryLeftEuler = new(-35f, 20f, 50f);
        [SerializeField] private Vector3 _carryRightEuler = new(-35f, -20f, -50f);
        [SerializeField] private float _carrySpeed = 5f;

        private GestureState _gesture;
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
                Duration = gesture == HandGesture.Rattle || gesture == HandGesture.Pry ? 0.8f : 0.6f,
                ContactTime = gesture == HandGesture.Rattle ? 0.16f : 0.2f,
                OnContact = onContact,
            };
        }

        public void Tick(float deltaTime, float normalizedSpeed, Vector2 lookDelta)
        {
            _moveBlend = Mathf.MoveTowards(_moveBlend, normalizedSpeed, deltaTime * 4f);
            _bobPhase += deltaTime * _bobFrequency * Mathf.Lerp(1f, 1.6f, normalizedSpeed) * Mathf.PI * 2f;
            float bobX = Mathf.Sin(_bobPhase) * _bobAmplitude.x * _moveBlend;
            float bobY = -Mathf.Abs(Mathf.Cos(_bobPhase)) * _bobAmplitude.y * _moveBlend;
            float breath = Mathf.Sin(Time.time * 1.6f) * _breathAmplitude * (1f - _moveBlend);

            Vector2 targetSway = -lookDelta * _swayAmount;
            targetSway = Vector2.ClampMagnitude(targetSway, 0.05f);
            _sway = Vector2.Lerp(_sway, targetSway, 1f - Mathf.Exp(-_swaySmoothing * deltaTime));

            Vector3 common = new Vector3(_sway.x, _sway.y + breath, 0f);
            Vector3 left = _leftRest + common + new Vector3(bobX, bobY, 0f);
            Vector3 right = _rightRest + common + new Vector3(bobX, -bobY * 0.6f + bobY * 0.4f, 0f);
            Vector3 leftEuler = _leftRestEuler;
            Vector3 rightEuler = _rightRestEuler;

            _aimBlend = Mathf.MoveTowards(_aimBlend, _isAiming ? 1f : 0f, deltaTime * _aimSpeed);
            if (_aimBlend > 0f)
            {
                float jitter = Mathf.Sin(Time.time * 47f) * 0.0025f * _aimBlend;
                right += _aimOffset * _aimBlend + new Vector3(jitter, jitter * 0.5f, 0f);
                rightEuler += _aimEuler * _aimBlend;
                left += new Vector3(0.02f, 0.01f, 0.02f) * _aimBlend;
            }

            _carryBlend = Mathf.MoveTowards(_carryBlend, _isCarrying ? 1f : 0f, deltaTime * _carrySpeed);
            if (_carryBlend > 0f)
            {
                float k = Smooth(_carryBlend);
                left += _carryLeftOffset * k;
                right += _carryRightOffset * k;
                leftEuler += _carryLeftEuler * k;
                rightEuler += _carryRightEuler * k;
            }

            ApplyGesture(deltaTime, ref left, ref right, ref leftEuler, ref rightEuler);

            _leftHand.localPosition = left;
            _rightHand.localPosition = right;
            _leftHand.localRotation = Quaternion.Euler(leftEuler);
            _rightHand.localRotation = Quaternion.Euler(rightEuler);
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

            float reach = t < _gesture.ContactTime
                ? EaseOut(t / _gesture.ContactTime)
                : 1f - Smooth(Mathf.InverseLerp(_gesture.ContactTime + 0.12f, _gesture.Duration, t));

            switch (_gesture.Type)
            {
                case HandGesture.Push:
                    right += new Vector3(-0.1f, 0.13f, 0.3f) * reach;
                    rightEuler += new Vector3(-12f, -12f, -55f) * reach;
                    left += new Vector3(0f, -0.04f, -0.03f) * reach;
                    break;

                case HandGesture.Rattle:
                    float shake = t > _gesture.ContactTime && t < _gesture.Duration - 0.2f
                        ? Mathf.Sin((t - _gesture.ContactTime) * 50f) * 0.025f
                        : 0f;
                    right += new Vector3(-0.1f + shake, 0.1f, 0.28f) * reach;
                    rightEuler += new Vector3(-10f, -15f, -65f + shake * 400f) * reach;
                    break;

                case HandGesture.Pry:
                    float lever = t > _gesture.ContactTime ? Mathf.Sin((t - _gesture.ContactTime) * 22f) * 0.03f : 0f;
                    right += new Vector3(-0.14f, 0.1f + lever, 0.3f) * reach;
                    rightEuler += new Vector3(-10f + lever * 300f, -20f, -40f) * reach;
                    left += new Vector3(0.12f, 0.08f, 0.22f) * reach;
                    leftEuler += new Vector3(-10f, 20f, 30f) * reach;
                    break;

                case HandGesture.Grab:
                    right += new Vector3(-0.05f, -0.06f, 0.3f) * reach;
                    rightEuler += new Vector3(25f, -8f, -15f) * reach;
                    break;
            }

            if (t >= _gesture.Duration)
            {
                _gesture = default;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
