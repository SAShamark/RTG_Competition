using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay.Entities.Characters.Player
{
    public enum PlayerView
    {
        FirstPerson = 0,
        ThirdPerson = 1,
    }

    [Serializable]
    public sealed class PlayerViewLogic
    {
        [SerializeField] private CinemachineCamera _firstPersonCamera;
        [SerializeField] private CinemachineCamera _thirdPersonCamera;
        [SerializeField] private int _activePriority = 10;
        [SerializeField] private int _inactivePriority = 0;
        [SerializeField] private Renderer[] _firstPersonOnly;
        [SerializeField] private Transform _body;
        [SerializeField] private PlayerView _startView = PlayerView.FirstPerson;
        [SerializeField] private float _firstPersonRevealDelay = 0.35f;
        [SerializeField] private float _hideBodyDistance = 0.6f;
        [SerializeField] private Transform _handsRig;
        [SerializeField] private Vector3 _thirdPersonHandsOffset = new(0.04f, -0.3f, 0.12f);

        private Renderer[] _bodyRenderers;
        private bool _isBodyHidden;
        private float _revealTimer = -1f;
        private Transform _outputCamera;

        public PlayerView Current { get; private set; }
        public bool IsThirdPerson => Current == PlayerView.ThirdPerson;

        public event Action<PlayerView> ViewChanged;

        public void Init()
        {
            _bodyRenderers = _body.GetComponentsInChildren<Renderer>(true);
            _outputCamera = Camera.main.transform;
            Apply(_startView);
            ApplyVisuals(_startView);
        }

        public void SetBodyHidden(bool isHidden)
        {
            if (isHidden == _isBodyHidden)
            {
                return;
            }

            _isBodyHidden = isHidden;
            ShadowCastingMode mode = isHidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer renderer in _bodyRenderers)
            {
                renderer.shadowCastingMode = mode;
            }
        }

        public void Toggle()
        {
            PlayerView view = Current == PlayerView.FirstPerson ? PlayerView.ThirdPerson : PlayerView.FirstPerson;
            Apply(view);
            if (view == PlayerView.ThirdPerson)
            {
                _revealTimer = -1f;
                ApplyVisuals(view);
                return;
            }

            _revealTimer = _firstPersonRevealDelay;
        }

        // The camera is still flying back to the head, so the body stays visible until it gets close enough to clip into the hat.
        public void Tick(float deltaTime)
        {
            if (_revealTimer < 0f)
            {
                return;
            }

            _revealTimer -= deltaTime;
            bool isNearHead = (_outputCamera.position - _firstPersonCamera.transform.position).sqrMagnitude <= _hideBodyDistance * _hideBodyDistance;
            if (isNearHead)
            {
                _revealTimer = -1f;
            }

            if (_revealTimer < 0f && Current == PlayerView.FirstPerson)
            {
                ApplyVisuals(PlayerView.FirstPerson);
            }
        }

        private void Apply(PlayerView view)
        {
            Current = view;
            bool isFirstPerson = view == PlayerView.FirstPerson;
            _firstPersonCamera.Priority.Value = isFirstPerson ? _activePriority : _inactivePriority;
            _thirdPersonCamera.Priority.Value = isFirstPerson ? _inactivePriority : _activePriority;
        }

        private void ApplyVisuals(PlayerView view)
        {
            bool isFirstPerson = view == PlayerView.FirstPerson;

            _isBodyHidden = isFirstPerson;
            ShadowCastingMode bodyMode = isFirstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer renderer in _bodyRenderers)
            {
                renderer.shadowCastingMode = bodyMode;
            }

            foreach (Renderer renderer in _firstPersonOnly)
            {
                renderer.enabled = isFirstPerson;
            }

            _handsRig.localPosition = isFirstPerson ? Vector3.zero : _thirdPersonHandsOffset;
            ViewChanged?.Invoke(view);
        }
    }
}
