using UnityEngine;

namespace Gameplay.Effects.Puddle
{
    public sealed class PuddleControl : MonoBehaviour
    {
        private static readonly int WaterMapId = Shader.PropertyToID("_WaterMap");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");

        [SerializeField] private Renderer _renderer;
        [SerializeField, Min(0f)] private float _catchHeight = 3f;
        [SerializeField, Range(0f, 1f)] private float _catchShare = 0.5f;
        [SerializeField] private PuddleSim _sim = new();

        private Texture2D _map;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            Vector3 size = transform.lossyScale;
            _sim.Init(size.x, size.y);
            _map = new Texture2D(_sim.Columns, _sim.Rows, TextureFormat.RGBAFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _block = new MaterialPropertyBlock();
            _block.SetTexture(WaterMapId, _map);
            Upload();
        }

        private void OnDestroy()
        {
            Destroy(_map);
        }

        private void Update()
        {
            if (_sim.Tick(Time.deltaTime))
            {
                Upload();
            }
        }

        public void Pour(Vector3 point, float litres)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            float height = local.z * transform.lossyScale.z;
            if (Mathf.Abs(height) > _catchHeight || Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f)
            {
                return;
            }

            _sim.Pour(local.x + 0.5f, local.y + 0.5f, litres * _catchShare);
            _block.SetVector(ImpactId, point);
            _renderer.SetPropertyBlock(_block);
        }

        private void Upload()
        {
            _map.SetPixelData(_sim.Pixels, 0);
            _map.Apply(false);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
