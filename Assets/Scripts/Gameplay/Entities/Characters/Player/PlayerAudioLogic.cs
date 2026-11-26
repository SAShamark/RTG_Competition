using System;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class PlayerAudioLogic
    {
        private const float StepsPerBob = 2f;

        [SerializeField] private float _minStepSpeed = 0.6f;
        [SerializeField] private float _groundProbe = 1.5f;
        [SerializeField] private float _roofProbe = 6f;
        [SerializeField] private float _indoorCheckInterval = 0.25f;
        [SerializeField] private string[] _woodKeywords = { "Parquet", "Wood", "Plank", "Stair", "Step" };
        [SerializeField] private string[] _metalKeywords = { "Metal", "truck", "Truck", "Grate", "Ladder" };
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private float _jumpPitch = 1.15f;
        [SerializeField] private float _landingPitch = 0.85f;
        [SerializeField] private float _hardLandingPitch = 0.7f;

        public float StepPhase { get; private set; }

        private Transform _body;
        private Transform _head;
        private Vector3 _lastPosition;
        private int _lastStep;
        private bool _wasStriding;
        private float _sinceIndoorCheck;

        public void Init(Transform body, Transform head)
        {
            _body = body;
            _head = head;
            _lastPosition = body.position;
        }

        public void Tick(bool isGrounded, bool isRunning, bool isStriding, float stride, float deltaTime)
        {
            Vector3 position = _body.position;
            Vector3 moved = position - _lastPosition;
            moved.y = 0f;
            _lastPosition = position;

            int step = Mathf.FloorToInt(stride);
            bool isMoving = moved.magnitude >= _minStepSpeed * deltaTime;
            if (isGrounded && isMoving && isStriding && _wasStriding && step != _lastStep)
            {
                AudioService.Instance.Play(StepSound(position, isRunning));
            }

            _lastStep = step;
            _wasStriding = isStriding;
            StepPhase = Mathf.Repeat(stride, StepsPerBob);

            _sinceIndoorCheck += deltaTime;
            if (_sinceIndoorCheck >= _indoorCheckInterval)
            {
                _sinceIndoorCheck = 0f;
                AudioService.Instance.SetIndoors(Physics.Raycast(_head.position, Vector3.up, _roofProbe, _mask, QueryTriggerInteraction.Ignore));
            }
        }

        public void PlayJump()
        {
            AudioService.Instance.Play(StepSound(_body.position, false), _jumpPitch);
            AudioService.Instance.Play(SoundType.Jump);
        }

        public void PlayLanding(bool isHard)
        {
            AudioService.Instance.Play(StepSound(_body.position, false), isHard ? _hardLandingPitch : _landingPitch);
            AudioService.Instance.Play(isHard ? SoundType.LandHard : SoundType.Land);
        }

        private SoundType StepSound(Vector3 position, bool isRunning)
        {
            if (!Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, _groundProbe, _mask, QueryTriggerInteraction.Ignore)
                || !hit.collider.TryGetComponent(out Renderer surface))
            {
                return isRunning ? SoundType.FootstepConcreteRun : SoundType.FootstepConcrete;
            }

            string material = surface.sharedMaterial != null ? surface.sharedMaterial.name : string.Empty;
            if (Matches(_metalKeywords, material, hit.collider.name))
            {
                return isRunning ? SoundType.FootstepMetalRun : SoundType.FootstepMetal;
            }

            if (Matches(_woodKeywords, material, hit.collider.name))
            {
                return isRunning ? SoundType.FootstepWoodRun : SoundType.FootstepWood;
            }

            return isRunning ? SoundType.FootstepConcreteRun : SoundType.FootstepConcrete;
        }

        private static bool Matches(string[] keywords, string material, string collider)
        {
            foreach (string keyword in keywords)
            {
                if (material.Contains(keyword) || collider.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
