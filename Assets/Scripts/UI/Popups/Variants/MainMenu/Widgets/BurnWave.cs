using Services;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Popups.Variants.MainMenu.Widgets
{
    public sealed class BurnWave : MonoBehaviour
    {
        private static readonly int Center = Shader.PropertyToID("_Center");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int Aspect = Shader.PropertyToID("_Aspect");
        private static readonly int Phase = Shader.PropertyToID("_Phase");

        [SerializeField] private Image _image;
        [SerializeField, Min(0.1f)] private float _duration = 2.2f;
        [SerializeField, Min(0f)] private float _overshoot = 0.5f;
        [SerializeField, Min(0f)] private float _punchScale = 0.04f;
        [SerializeField, Min(0.05f)] private float _punchTime = 0.6f;

        private Material _material;
        private float _time = -1f;
        private float _maxRadius;

        public bool IsBurning => _time >= 0f && _time < _duration;

        private void Awake()
        {
            _material = new Material(_image.material);
            _image.material = _material;
            _material.SetFloat(Radius, 0f);
        }

        private void OnDestroy() => Destroy(_material);

        public void Burn(Vector3 worldPoint)
        {
            RectTransform rect = _image.rectTransform;
            Vector2 center = Rect.PointToNormalized(rect.rect, rect.InverseTransformPoint(worldPoint));
            float aspect = rect.rect.width / rect.rect.height;

            // the wave has to pass the farthest corner of the picture
            Vector2 reach = new Vector2(Mathf.Max(center.x, 1f - center.x) * aspect, Mathf.Max(center.y, 1f - center.y));
            _maxRadius = reach.magnitude + _overshoot;

            _material.SetVector(Center, center);
            _material.SetFloat(Aspect, aspect);
            _time = 0f;
        }

        private void Update()
        {
            if (_time < 0f)
            {
                return;
            }

            _time += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_time / _duration);
            _material.SetFloat(Radius, _maxRadius * Easing.OutQuad(progress));
            _material.SetFloat(Phase, Time.unscaledTime);

            float punch = Mathf.Clamp01(_time / _punchTime);
            transform.localScale = Vector3.one * (1f + _punchScale * Mathf.Sin(punch * Mathf.PI) * (1f - punch));
        }
    }
}
