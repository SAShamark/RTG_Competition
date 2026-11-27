using UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Vr
{
    public sealed class VrWristDisplay : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Text _objective;
        [SerializeField] private Text _rescued;
        [SerializeField] private UiPulse _pulse;
        [SerializeField] private string _rescuedFormat = "ВРЯТОВАНО {0} / {1}";
        [SerializeField, Range(-1f, 1f)] private float _showFacing = 0.55f;
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float _objectivePulse = 0.3f;
        [SerializeField, Min(0.01f)] private float _objectivePulseDuration = 0.15f;

        private Transform _head;
        private VrInputLogic _input;

        public void Initialize(Transform head, VrInputLogic input)
        {
            _head = head;
            _input = input;
            _group.alpha = 0f;
            _rescued.text = string.Empty;
        }

        public void SetObjective(string text)
        {
            if (_objective.text == text)
            {
                return;
            }

            _objective.text = text;
            _pulse.Play();
            _input.Pulse(true, _objectivePulse, _objectivePulseDuration);
        }

        public void UpdateObjective(string text)
        {
            _objective.text = text;
            _pulse.Play();
        }

        public void ShowRescued(int value, int total) => _rescued.text = string.Format(_rescuedFormat, value, total);

        public void HideRescued() => _rescued.text = string.Empty;

        private void LateUpdate()
        {
            Transform face = transform;
            Vector3 toHead = (_head.position - face.position).normalized;
            bool isFacing = Vector3.Dot(-face.forward, toHead) >= _showFacing;
            _group.alpha = Mathf.MoveTowards(_group.alpha, isFacing ? 1f : 0f, _fadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
