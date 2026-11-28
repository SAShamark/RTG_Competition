using System;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    public sealed class ShieldEffect : MonoBehaviour
    {
        private const float Projected = 1f;
        private const float Formed = 2f;

        private static readonly int AppearState = Animator.StringToHash("HoloShieldAppear");

        [SerializeField] private Transform _markersRoot;
        [SerializeField, Range(0f, 1f)] private float _formedTime = 0.42f;
        [SerializeField, Min(0f)] private float _markerFormedTime = 5f;

        [Header("Projection")]
        [SerializeField] private Transform _beam;
        [SerializeField] private Vector3 _projectionOffset = new(-0.05f, 0.075f, 0.02f);
        [SerializeField, Range(0.001f, 1f)] private float _projectedScale = 0.024f;
        [SerializeField, Min(0f)] private float _beamRadius = 0.026f;
        [SerializeField, Range(0f, 1f)] private float _beamCutoff = 0.12f;
        [SerializeField, Min(0.01f)] private float _projectDuration = 0.3f;
        [SerializeField, Min(0f)] private float _projectHold = 0.35f;
        [SerializeField, Min(0.01f)] private float _retractDuration = 0.2f;

        [Header("Expansion")]
        [SerializeField, Min(0.01f)] private float _growDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float _collapseDuration = 0.35f;
        [SerializeField, Min(0f)] private float _overshoot = 1.6f;

        private Animator[] _animators;
        private ParticleSystem[] _markers;
        private Transform _origin;
        private Vector3 _homePosition;
        private Vector3 _homeScale;
        private float _stage;
        private float _hold;
        private bool _isShown;

        public bool IsVisible => gameObject.activeSelf;

        public event Action<bool> Detached;

        public void Init()
        {
            _animators = GetComponentsInChildren<Animator>(true);
            _markers = _markersRoot.GetComponentsInChildren<ParticleSystem>(true);
            _homePosition = transform.localPosition;
            _homeScale = transform.localScale;
            _beam.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public void Show(Transform origin)
        {
            _origin = origin;
            _isShown = true;
            if (!gameObject.activeSelf)
            {
                _stage = 0f;
                _hold = _projectHold;
                gameObject.SetActive(true);
            }

            foreach (Animator animator in _animators)
            {
                animator.speed = 0f;
                animator.Play(AppearState, 0, _formedTime);
            }

            foreach (ParticleSystem marker in _markers)
            {
                marker.Simulate(_markerFormedTime, false, true);
            }

            Apply();
        }

        public void Hide(Transform origin)
        {
            _origin = origin;
            _isShown = false;
        }

        private void Update()
        {
            float previous = _stage;
            if (_isShown)
            {
                Advance(Time.deltaTime);
            }
            else
            {
                float duration = _stage > Projected ? _collapseDuration : _retractDuration;
                float floor = _stage > Projected ? Projected : 0f;
                _stage = Mathf.MoveTowards(_stage, floor, Time.deltaTime / duration);
            }

            if (previous <= Projected != _stage <= Projected)
            {
                Detached?.Invoke(_stage > Projected);
            }

            Apply();
            if (!_isShown && _stage <= 0f)
            {
                _beam.gameObject.SetActive(false);
                gameObject.SetActive(false);
            }
        }

        private void Advance(float deltaTime)
        {
            if (_stage < Projected)
            {
                _stage = Mathf.MoveTowards(_stage, Projected, deltaTime / _projectDuration);
                return;
            }

            if (_hold > 0f)
            {
                _hold -= deltaTime;
                return;
            }

            _stage = Mathf.MoveTowards(_stage, Formed, deltaTime / _growDuration);
        }

        private void Apply()
        {
            Transform space = transform.parent;
            Vector3 source = space.InverseTransformPoint(_origin.position);
            Vector3 projection = space.InverseTransformPoint(_origin.TransformPoint(_projectionOffset));
            Vector3 smallScale = _homeScale * _projectedScale;
            float expansion = _stage - Projected;
            if (expansion <= 0f)
            {
                float reach = Easing.OutCubic(_stage);
                transform.localPosition = Vector3.Lerp(source, projection, reach);
                transform.localScale = smallScale * reach;
            }
            else
            {
                float size = _isShown ? Easing.OutBack(expansion, _overshoot) : Easing.InQuad(expansion);
                transform.localPosition = Vector3.Lerp(projection, _homePosition, size);
                transform.localScale = Vector3.LerpUnclamped(smallScale, _homeScale, size);
            }

            bool isBeamVisible = _stage > 0f && expansion < _beamCutoff;
            _beam.gameObject.SetActive(isBeamVisible);
            if (!isBeamVisible)
            {
                return;
            }

            Vector3 toShield = transform.position - _origin.position;
            float width = _beamRadius * Mathf.Clamp01(_stage);
            _beam.SetPositionAndRotation(_origin.position, Quaternion.LookRotation(toShield, _origin.up));
            _beam.localScale = new Vector3(width, width, toShield.magnitude);
        }
    }
}
