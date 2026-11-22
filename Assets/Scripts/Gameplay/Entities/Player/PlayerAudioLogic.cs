using Services.Audio;
using System;
using UnityEngine;

namespace Gameplay.Entities.Player
{
    [Serializable]
    public sealed class PlayerAudioLogic
    {
        [SerializeField] private float _stride = 0.75f;
        [SerializeField] private float _runStride = 1.05f;
        [SerializeField] private float _groundProbe = 1.5f;
        [SerializeField] private float _roofProbe = 6f;
        [SerializeField] private float _indoorCheckInterval = 0.25f;
        [SerializeField] private string[] _woodKeywords = { "Parquet", "Wood", "Plank", "Stair", "Step" };
        [SerializeField] private LayerMask _mask = ~0;

        private Transform _body;
        private Transform _head;
        private Vector3 _lastPosition;
        private float _travelled;
        private float _sinceIndoorCheck;

        public void Init(Transform body, Transform head)
        {
            _body = body;
            _head = head;
            _lastPosition = body.position;
        }

        public void Tick(bool isGrounded, bool isRunning, float deltaTime)
        {
            Vector3 position = _body.position;
            Vector3 moved = position - _lastPosition;
            moved.y = 0f;
            _lastPosition = position;

            if (isGrounded)
            {
                _travelled += moved.magnitude;
                float stride = isRunning ? _runStride : _stride;
                if (_travelled >= stride)
                {
                    _travelled = 0f;
                    AudioService.Instance.Play(IsOnWood(position) ? SoundType.FootstepWood : SoundType.FootstepConcrete);
                }
            }

            _sinceIndoorCheck += deltaTime;
            if (_sinceIndoorCheck >= _indoorCheckInterval)
            {
                _sinceIndoorCheck = 0f;
                AudioService.Instance.SetIndoors(Physics.Raycast(_head.position, Vector3.up, _roofProbe, _mask, QueryTriggerInteraction.Ignore));
            }
        }

        private bool IsOnWood(Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, _groundProbe, _mask, QueryTriggerInteraction.Ignore)
                || !hit.collider.TryGetComponent(out Renderer surface))
            {
                return false;
            }

            string name = surface.sharedMaterial != null ? surface.sharedMaterial.name : string.Empty;
            foreach (string keyword in _woodKeywords)
            {
                if (name.Contains(keyword) || hit.collider.name.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
