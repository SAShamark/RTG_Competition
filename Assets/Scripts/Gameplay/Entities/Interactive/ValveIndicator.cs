using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class ValveIndicator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private PipeValve _valve;
        [SerializeField] private Renderer _lamp;
        [SerializeField, ColorUsage(false, true)] private Color _idleColor = new(3f, 0.15f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color _progressColor = new(3f, 1.8f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color _doneColor = new(0.2f, 3f, 0.3f);
        [SerializeField, ColorUsage(false, true)] private Color _offColor = new(0.12f, 0.02f, 0.02f);
        [SerializeField, Min(0.01f)] private float _blinkInterval = 0.15f;

        private MaterialPropertyBlock _block;
        private float _blinkTime;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _valve.Turned += HandleChanged;
            _valve.RewindStarted += HandleRewindStarted;
            _valve.Rewound += HandleChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            _valve.Turned -= HandleChanged;
            _valve.RewindStarted -= HandleRewindStarted;
            _valve.Rewound -= HandleChanged;
        }

        private void Update()
        {
            if (!_valve.IsRewinding)
            {
                return;
            }

            _blinkTime += Time.deltaTime;
            bool isLit = Mathf.FloorToInt(_blinkTime / _blinkInterval) % 2 == 0;
            Show(isLit ? _idleColor : _offColor);
        }

        private void HandleRewindStarted(PipeValve valve)
        {
            _blinkTime = 0f;
            Show(_idleColor);
        }

        private void HandleChanged(PipeValve valve) => Refresh();

        private void Refresh()
        {
            if (_valve.IsRewinding)
            {
                return;
            }

            if (_valve.Turns == _valve.TargetTurns)
            {
                Show(_doneColor);
                return;
            }

            Show(_valve.Turns == 0 ? _idleColor : _progressColor);
        }

        private void Show(Color color)
        {
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color);
            _lamp.SetPropertyBlock(_block);
        }
    }
}
