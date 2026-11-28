using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Effects.Puddle
{
    public sealed class PuddleControl : MonoBehaviour
    {
        private const float Half = 0.5f;
        private const float GroundReach = 0.08f;
        private const float ProbeLift = 0.05f;
        private const float ObstacleLift = 0.25f;
        private const float ObstacleRadius = 0.04f;
        private const float MinGroundUp = 0.7f;
        private const int MaxObstacleHits = 8;

        private static readonly int WaterMapId = Shader.PropertyToID("_WaterMap");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");
        private static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);
        private static readonly List<PuddleControl> Zones = new();
        private static readonly Collider[] Obstacles = new Collider[MaxObstacleHits];

        [SerializeField] private Renderer _renderer;
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private PuddleSim _sim = new();

        private Texture2D _map;
        private MaterialPropertyBlock _block;
        private PuddleControl _east;
        private PuddleControl _west;
        private PuddleControl _north;
        private PuddleControl _south;
        private bool _isBuilt;

        public bool IsDry => !_sim.IsWet;

        public static bool PourAll(Vector3 point, float litres)
        {
            bool isCovered = false;
            foreach (PuddleControl zone in Zones)
            {
                isCovered |= zone.Pour(point, litres);
            }

            return isCovered;
        }

        public static bool IsCovered(Vector3 point)
        {
            foreach (PuddleControl zone in Zones)
            {
                if (zone.Covers(point, 0f, 0f))
                {
                    return true;
                }
            }

            return false;
        }

        public void Place(Vector3 centre, float size)
        {
            transform.SetPositionAndRotation(centre, Flat);
            transform.localScale = new Vector3(size, size, 1f);
            Build();
        }

        private void OnEnable() => Zones.Add(this);

        private void OnDisable()
        {
            Zones.Remove(this);
            Relink();
        }

        private void OnDestroy()
        {
            Destroy(_map);
        }

        private void Update()
        {
            if (!_isBuilt || !_sim.Tick(Time.deltaTime))
            {
                return;
            }

            // A sleeping neighbour does not step, so the awake side carries the exchange across their shared edge for it.
            if (_east != null)
            {
                _sim.Exchange(_east._sim, true);
            }

            if (_north != null)
            {
                _sim.Exchange(_north._sim, false);
            }

            if (_west != null && _west.IsDry)
            {
                _west._sim.Exchange(_sim, true);
            }

            if (_south != null && _south.IsDry)
            {
                _south._sim.Exchange(_sim, false);
            }

            Upload();
        }

        private static void Relink()
        {
            foreach (PuddleControl zone in Zones)
            {
                Vector3 size = zone.transform.lossyScale;
                Vector3 centre = zone.transform.position;
                zone._east = FindBuilt(centre + Vector3.right * size.x, zone);
                zone._west = FindBuilt(centre + Vector3.left * size.x, zone);
                zone._north = FindBuilt(centre + Vector3.forward * size.y, zone);
                zone._south = FindBuilt(centre + Vector3.back * size.y, zone);
            }
        }

        private static PuddleControl FindBuilt(Vector3 point, PuddleControl except)
        {
            foreach (PuddleControl zone in Zones)
            {
                if (zone != except && zone._isBuilt && zone.Covers(point, 0f, 0f))
                {
                    return zone;
                }
            }

            return null;
        }

        private void Build()
        {
            Vector3 size = transform.lossyScale;
            _sim.Init(size.x, size.y, IsOpen);
            Destroy(_map);
            _map = new Texture2D(_sim.Columns, _sim.Rows, TextureFormat.RGBAFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _block ??= new MaterialPropertyBlock();
            _block.SetTexture(WaterMapId, _map);
            _isBuilt = true;
            Upload();
            Relink();
        }

        private bool Covers(Vector3 point, float marginX, float marginY)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.z * transform.lossyScale.z) <= GroundReach && Mathf.Abs(local.x) <= Half + marginX && Mathf.Abs(local.y) <= Half + marginY;
        }

        // A splash next to the edge spills its share into this grid too, but only a hit inside the footprint counts as covered.
        private bool Pour(Vector3 point, float litres)
        {
            Vector3 size = transform.lossyScale;
            if (!Covers(point, _sim.SplashRadius / size.x, _sim.SplashRadius / size.y))
            {
                return false;
            }

            if (!_isBuilt)
            {
                Build();
            }

            Vector3 local = transform.InverseTransformPoint(point);
            _sim.Pour(local.x + Half, local.y + Half, litres);
            _block.SetVector(ImpactId, point);
            _renderer.SetPropertyBlock(_block);
            return Covers(point, 0f, 0f);
        }

        // A cell holds water only where level ground lies right under it and no wall or furniture stands on it.
        private bool IsOpen(int column, int row)
        {
            var local = new Vector3((column + Half) / _sim.Columns - Half, (row + Half) / _sim.Rows - Half, 0f);
            Vector3 point = transform.TransformPoint(local);
            int count = Physics.OverlapSphereNonAlloc(point + Vector3.up * ObstacleLift, ObstacleRadius, Obstacles, _groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = Obstacles[i];
                if (obstacle.attachedRigidbody == null && obstacle is not CharacterController && obstacle.GetComponentInParent<Animator>() == null)
                {
                    return false;
                }
            }

            return Physics.Raycast(point + Vector3.up * ProbeLift, Vector3.down, out RaycastHit ground, ProbeLift + GroundReach, _groundMask, QueryTriggerInteraction.Ignore)
                && ground.normal.y >= MinGroundUp;
        }

        private void Upload()
        {
            _map.SetPixelData(_sim.Pixels, 0);
            _map.Apply(false);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
