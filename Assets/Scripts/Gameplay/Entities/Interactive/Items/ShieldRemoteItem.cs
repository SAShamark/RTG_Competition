using Services;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ShieldRemoteItem : MonoBehaviour
    {
        [SerializeField] private Transform _button;
        [SerializeField, Min(0f)] private float _pressDepth = 0.007f;
        [SerializeField, Range(0f, 1f)] private float _latchedDepth = 0.45f;
        [SerializeField, Min(0.01f)] private float _clickDuration = 0.22f;

        [Header("Emitter")]
        [SerializeField] private Transform _emitter;
        [SerializeField] private Renderer _body;
        [SerializeField, Min(0)] private int _ledMaterialIndex = 3;
        [SerializeField] private Material _ledIdle;
        [SerializeField] private Material _ledEmitting;

        private Vector3 _rest;
        private float _from;
        private float _to;
        private float _time;

        public Transform Emitter => _emitter;

        public void SetEmitting(bool isEmitting)
        {
            Material[] materials = _body.sharedMaterials;
            materials[_ledMaterialIndex] = isEmitting ? _ledEmitting : _ledIdle;
            _body.sharedMaterials = materials;
        }

        public void SetPressed(bool isPressed)
        {
            _from = _to;
            _to = isPressed ? _latchedDepth : 0f;
            _time = 0f;
        }

        private void Awake()
        {
            _rest = _button.localPosition;
            _time = _clickDuration;
        }

        private void Update()
        {
            if (_time >= _clickDuration)
            {
                return;
            }

            _time = Mathf.Min(_time + Time.deltaTime, _clickDuration);
            float progress = _time / _clickDuration;
            float settled = Mathf.Lerp(_from, _to, progress);
            // The button bottoms out mid-click and then settles: latched after a press, flush after a release.
            float depth = settled + Easing.Parabola(progress) * (1f - settled);
            _button.localPosition = _rest + Vector3.back * (depth * _pressDepth);
        }
    }
}
