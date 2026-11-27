using System;
using UnityEngine;

namespace Vr
{
    public enum VrScreenMode
    {
        Visor = 0,
        Panel = 1,
        Cinema = 2,
    }

    [Serializable]
    public sealed class VrScreenLogic
    {
        [SerializeField] private Transform _screen;
        [SerializeField] private Transform _uiQuad;
        [SerializeField] private Transform _cinemaQuad;
        [SerializeField] private RenderTexture _uiTexture;

        [Header("Visor (gameplay HUD)")]
        [SerializeField, Min(0.1f)] private float _visorDistance = 1.2f;
        [SerializeField, Range(20f, 90f)] private float _visorFieldOfView = 52f;

        [Header("Panel (menus over gameplay)")]
        [SerializeField, Min(0.1f)] private float _panelDistance = 1.5f;
        [SerializeField, Min(0.1f)] private float _panelHeight = 1.1f;

        [Header("Cinema (cutscenes and dialogues)")]
        [SerializeField, Min(0.1f)] private float _cinemaDistance = 2.6f;
        [SerializeField, Min(0.1f)] private float _cinemaHeight = 1.7f;
        [SerializeField, Min(0f)] private float _uiLift = 0.01f;
        [SerializeField] private float _screenDrop = 0.1f;

        [Header("Follow")]
        [SerializeField, Range(10f, 180f)] private float _followAngle = 60f;
        [SerializeField, Min(0.1f)] private float _followDistance = 1.2f;
        [SerializeField, Min(0.1f)] private float _followHeight = 0.6f;

        public VrScreenMode Mode { get; private set; } = VrScreenMode.Visor;
        public float VisorFieldOfView => _visorFieldOfView;
        public Transform UiQuad => _uiQuad;
        public Vector2 Resolution => new(_uiTexture.width, _uiTexture.height);

        private float Aspect => (float)_uiTexture.width / _uiTexture.height;

        public void Initialize(Transform head) => Apply(VrScreenMode.Visor, head);

        public void SetMode(VrScreenMode mode, Transform head)
        {
            if (mode != Mode)
            {
                Apply(mode, head);
            }
        }

        public void Tick(Transform head)
        {
            if (Mode == VrScreenMode.Visor)
            {
                return;
            }

            float distance = Mode == VrScreenMode.Cinema ? _cinemaDistance : _panelDistance;
            Vector3 toScreen = _screen.position - head.position;
            Vector3 flat = Vector3.ProjectOnPlane(toScreen, Vector3.up);
            Vector3 facing = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            bool isLost = Vector3.Angle(flat, facing) > _followAngle
                || Mathf.Abs(flat.magnitude - distance) > _followDistance
                || Mathf.Abs(toScreen.y + _screenDrop) > _followHeight;
            if (isLost)
            {
                Apply(Mode, head);
            }
        }

        private void Apply(VrScreenMode mode, Transform head)
        {
            Mode = mode;
            _cinemaQuad.gameObject.SetActive(mode == VrScreenMode.Cinema);
            if (mode == VrScreenMode.Visor)
            {
                // Visor height covers the HUD camera's vertical field of view at the chosen distance, so screen-space markers stay over their world targets.
                float height = 2f * _visorDistance * Mathf.Tan(_visorFieldOfView * 0.5f * Mathf.Deg2Rad);
                _uiQuad.SetParent(head, false);
                _uiQuad.localPosition = Vector3.forward * _visorDistance;
                _uiQuad.localRotation = Quaternion.identity;
                _uiQuad.localScale = new Vector3(height * Aspect, height, 1f);
                return;
            }

            bool isCinema = mode == VrScreenMode.Cinema;
            float distance = isCinema ? _cinemaDistance : _panelDistance;
            float screenHeight = isCinema ? _cinemaHeight : _panelHeight;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < Mathf.Epsilon)
            {
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            }

            forward.Normalize();
            _screen.SetPositionAndRotation(head.position + forward * distance + Vector3.down * _screenDrop, Quaternion.LookRotation(forward));
            var scale = new Vector3(screenHeight * Aspect, screenHeight, 1f);
            _cinemaQuad.localScale = scale;
            _uiQuad.SetParent(_screen, false);
            _uiQuad.localPosition = isCinema ? Vector3.back * _uiLift : Vector3.zero;
            _uiQuad.localRotation = Quaternion.identity;
            _uiQuad.localScale = scale;
        }
    }
}
