using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Vr
{
    public sealed class VrRadio : Interactable, IUsableWhileCarrying
    {
        [SerializeField] private string _hint = "[E] Рація — підказка";
        [SerializeField] private Renderer _indicator;
        [SerializeField] private Color _idleColor = new(0.35f, 0.15f, 0.05f, 1f);
        [SerializeField] private Color _talkColor = new(1f, 0.6f, 0.15f, 1f);
        [SerializeField, Min(0.05f)] private float _talkTime = 3.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;
        private bool _isUsed;
        private float _talkLeft;

        public override string GetHint(Transform interactor) => _hint;

        public bool ConsumeUse()
        {
            bool isUsed = _isUsed;
            _isUsed = false;
            return isUsed;
        }

        public void ShowTalking() => _talkLeft = _talkTime;

        protected override void OnInteract(Transform interactor) => _isUsed = true;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Paint(_idleColor);
        }

        private void Update()
        {
            if (_talkLeft <= 0f)
            {
                return;
            }

            _talkLeft -= Time.deltaTime;
            Paint(_talkLeft > 0f ? _talkColor : _idleColor);
        }

        private void Paint(Color color)
        {
            _block.SetColor(BaseColorId, color);
            _indicator.SetPropertyBlock(_block);
        }
    }
}
