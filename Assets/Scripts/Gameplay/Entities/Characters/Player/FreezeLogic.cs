using System;
using Services;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class FreezeLogic
    {
        private const float MinAwaySqr = 0.01f;

        [SerializeField, Min(0.1f)] private float _duration = 5f;
        [SerializeField] private Transform _body;
        [SerializeField] private Material _ice;

        [Header("Icicles")]
        [SerializeField] private Transform _shell;
        [SerializeField, Min(0.01f)] private float _growDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float _meltDuration = 0.4f;
        [SerializeField, Min(0f)] private float _overshoot = 1.6f;

        [Header("Sound")]
        [SerializeField] private SoundType _freezeSound = SoundType.GlassTinkle;
        [SerializeField] private SoundType _thawSound = SoundType.GlassShatter;

        private Transform _owner;
        private Renderer[] _renderers;
        private Material[][] _original;
        private float _remaining;
        private float _time;
        private float _melt;

        public bool IsFrozen { get; private set; }
        public float Duration => _duration;
        public float Remaining => _remaining;

        public event Action Frozen;
        public event Action Thawed;

        public void Init(Transform owner)
        {
            _owner = owner;
            _renderers = _body.GetComponentsInChildren<Renderer>(true);
            _original = new Material[_renderers.Length][];
            _shell.gameObject.SetActive(false);
        }

        public void Freeze(Vector3 source)
        {
            _remaining = _duration;
            if (IsFrozen)
            {
                return;
            }

            IsFrozen = true;
            _time = 0f;
            _melt = 0f;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _original[i] = _renderers[i].sharedMaterials;
                var iced = new Material[_original[i].Length];
                Array.Fill(iced, _ice);
                _renderers[i].sharedMaterials = iced;
            }

            Vector3 away = Vector3.ProjectOnPlane(_owner.position - source, Vector3.up);
            _shell.rotation = Quaternion.LookRotation(away.sqrMagnitude > MinAwaySqr ? away : -_owner.forward);
            _shell.localScale = Vector3.zero;
            _shell.gameObject.SetActive(true);
            AudioService.Instance.Play(_freezeSound);
            Frozen?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (IsFrozen)
            {
                _time += deltaTime;
                _remaining -= deltaTime;
                _shell.localScale = Vector3.one * Easing.OutBack(Mathf.Clamp01(_time / _growDuration), _overshoot);
                if (_remaining <= 0f)
                {
                    Thaw();
                }

                return;
            }

            if (_melt <= 0f)
            {
                return;
            }

            _melt -= deltaTime;
            _shell.localScale = Vector3.one * Mathf.Clamp01(_melt / _meltDuration);
            if (_melt <= 0f)
            {
                _shell.gameObject.SetActive(false);
            }
        }

        private void Thaw()
        {
            IsFrozen = false;
            _remaining = 0f;
            _melt = _meltDuration;
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].sharedMaterials = _original[i];
            }

            AudioService.Instance.Play(_thawSound);
            Thawed?.Invoke();
        }
    }
}
