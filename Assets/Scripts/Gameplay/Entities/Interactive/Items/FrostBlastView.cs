using Services;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class FrostBlastView : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        [Header("Crystal burst")]
        [SerializeField] private Transform _star;
        [SerializeField] private Vector2 _starScale = new(0.9f, 1.25f);
        [SerializeField, Min(0.01f)] private float _starPop = 0.1f;
        [SerializeField, Min(0f)] private float _starHold = 0.45f;
        [SerializeField, Min(0.01f)] private float _starShatter = 0.12f;
        [SerializeField, Min(0f)] private float _overshoot = 2.2f;
        [SerializeField] private ParticleSystem _shards;

        [Header("Shockwave")]
        [SerializeField] private Transform _wave;
        [SerializeField] private Renderer _waveRenderer;
        [SerializeField, Min(0.01f)] private float _waveDuration = 0.5f;
        [SerializeField, Min(0f)] private float _waveDiameter = 10f;

        private MaterialPropertyBlock _block;
        private float _scale;
        private float _time;
        private bool _hasShattered;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _scale = Random.Range(_starScale.x, _starScale.y);
            _star.rotation = Random.rotation;
            _star.localScale = Vector3.zero;
            _wave.localScale = Vector3.zero;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            TickWave();
            TickStar();
        }

        private void TickWave()
        {
            float progress = Mathf.Clamp01(_time / _waveDuration);
            _wave.localScale = Vector3.one * (_waveDiameter * Easing.OutCubic(progress));
            _block.SetFloat(FadeId, 1f - progress);
            _waveRenderer.SetPropertyBlock(_block);
            _waveRenderer.enabled = progress < 1f;
        }

        private void TickStar()
        {
            float shatterTime = _starPop + _starHold;
            if (!_hasShattered && _time >= shatterTime)
            {
                _hasShattered = true;
                _shards.Play();
            }

            float grown = Easing.OutBack(Mathf.Clamp01(_time / _starPop), _overshoot);
            float kept = 1f - Mathf.Clamp01((_time - shatterTime) / _starShatter);
            _star.localScale = Vector3.one * (_scale * grown * kept);
        }
    }
}
