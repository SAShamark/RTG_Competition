using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class FrostField : MonoBehaviour
    {
        private struct Patch
        {
            public Collider Surface;
            public Vector3 Point;
            public Vector3 Normal;
            public float Delay;
            public bool IsCenter;
        }

        private const int MaxZones = 8;
        private const float GoldenAngle = 2.39996323f;
        private const float FullTurn = 360f;

        private static readonly int ZonesId = Shader.PropertyToID("_FrostZones");
        private static readonly int StrengthsId = Shader.PropertyToID("_FrostStrengths");
        private static readonly int ZoneCountId = Shader.PropertyToID("_FrostZoneCount");

        private static readonly List<FrostField> Fields = new();
        private static readonly Vector4[] Zones = new Vector4[MaxZones];
        private static readonly float[] Strengths = new float[MaxZones];

        [SerializeField, Min(0f)] private float _radius = 5f;
        [SerializeField, Min(1)] private int _rays = 64;
        [SerializeField, Min(0f)] private float _originLift = 0.15f;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField, Min(0.1f)] private float _spreadSpeed = 14f;
        [SerializeField, Min(0f)] private float _duration = 12f;
        [SerializeField, Min(0.01f)] private float _meltDuration = 1.5f;

        [Header("Surface")]
        [SerializeField] private FrostCrystal _centerPlates;
        [SerializeField] private FrostCrystal _plates;
        [SerializeField] private FrostCrystal _crystal;
        [SerializeField] private FrostCrystal _icicles;
        [SerializeField, Range(0f, 1f)] private float _plateChance = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _floorCrystalChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _wallCrystalChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _icicleChance = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _minFloorNormal = 0.7f;

        [Header("Frozen look")]
        [SerializeField] private ScriptableRendererFeature[] _zoneFeatures;
        [SerializeField, Min(0f)] private float _zoneRadiusFactor = 1.1f;

        [Header("Slippery floor")]
        [SerializeField, Range(0f, 1f)] private float _grip = 0.12f;
        [SerializeField, Min(0f)] private float _slipRadius = 1.1f;
        [SerializeField, Min(0f)] private float _slipHeight = 0.6f;

        private readonly List<Patch> _pending = new();
        private readonly List<Vector3> _floor = new();
        private int _next;
        private float _time;

        private float ZoneRadius => Mathf.Min(_radius * _zoneRadiusFactor, _time * _spreadSpeed);
        private float ZoneStrength => Mathf.Clamp01((_duration + _meltDuration - _time) / _meltDuration);

        public static float GripAt(Vector3 position)
        {
            float grip = 1f;
            foreach (FrostField field in Fields)
            {
                if (field.Covers(position))
                {
                    grip = Mathf.Min(grip, field._grip);
                }
            }

            return grip;
        }

        private void OnEnable()
        {
            Fields.Add(this);
            SetZonePass(true);
        }

        private void OnDisable()
        {
            Fields.Remove(this);
            if (Fields.Count == 0)
            {
                Shader.SetGlobalFloat(ZoneCountId, 0f);
                SetZonePass(false);
            }
        }

        private void Start()
        {
            Vector3 origin = transform.position + Vector3.up * _originLift;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, _radius, _mask, QueryTriggerInteraction.Ignore)
                && ground.normal.y >= _minFloorNormal && SurfaceSplat.CanStickTo(ground.collider))
            {
                _pending.Add(new Patch { Surface = ground.collider, Point = ground.point, Normal = ground.normal, IsCenter = true });
            }

            Scan(origin);
            _pending.Sort((a, b) => a.Delay.CompareTo(b.Delay));
            Destroy(gameObject, _duration + _meltDuration);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            while (_next < _pending.Count && _pending[_next].Delay <= _time)
            {
                Freeze(_pending[_next]);
                _next++;
            }

            if (Fields[0] == this)
            {
                PushZones();
            }
        }

        private static void PushZones()
        {
            int count = Mathf.Min(Fields.Count, MaxZones);
            for (int i = 0; i < count; i++)
            {
                FrostField field = Fields[i];
                Vector3 center = field.transform.position;
                Zones[i] = new Vector4(center.x, center.y, center.z, field.ZoneRadius);
                Strengths[i] = field.ZoneStrength;
            }

            Shader.SetGlobalVectorArray(ZonesId, Zones);
            Shader.SetGlobalFloatArray(StrengthsId, Strengths);
            Shader.SetGlobalFloat(ZoneCountId, count);
        }

        private void SetZonePass(bool isActive)
        {
            foreach (ScriptableRendererFeature feature in _zoneFeatures)
            {
                feature.SetActive(isActive);
            }
        }

        private void Scan(Vector3 origin)
        {
            for (int i = 0; i < _rays; i++)
            {
                // Fibonacci sphere: evenly spread directions without clustering at the poles.
                float height = 1f - 2f * (i + 0.5f) / _rays;
                float ring = Mathf.Sqrt(1f - height * height);
                float angle = i * GoldenAngle;
                var direction = new Vector3(Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring);
                if (!Physics.Raycast(origin, direction, out RaycastHit hit, _radius, _mask, QueryTriggerInteraction.Ignore) || !SurfaceSplat.CanStickTo(hit.collider))
                {
                    continue;
                }

                _pending.Add(new Patch { Surface = hit.collider, Point = hit.point, Normal = hit.normal, Delay = hit.distance / _spreadSpeed });
            }
        }

        private void Freeze(Patch patch)
        {
            if (patch.Surface == null)
            {
                return;
            }

            bool isFloor = patch.Normal.y >= _minFloorNormal;
            if (isFloor)
            {
                _floor.Add(patch.Point);
            }

            Rigidbody body = patch.Surface.attachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                return;
            }

            if (patch.IsCenter)
            {
                Grow(_centerPlates, patch);
            }
            else if (isFloor)
            {
                TryGrow(_plates, patch, _plateChance);
                TryGrow(_crystal, patch, _floorCrystalChance);
            }
            else if (patch.Normal.y <= -_minFloorNormal)
            {
                TryGrow(_icicles, patch, _icicleChance);
            }
            else
            {
                TryGrow(_crystal, patch, _wallCrystalChance);
            }
        }

        private void TryGrow(FrostCrystal prefab, Patch patch, float chance)
        {
            if (Random.value < chance)
            {
                Grow(prefab, patch);
            }
        }

        private void Grow(FrostCrystal prefab, Patch patch)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, patch.Normal) * Quaternion.Euler(0f, Random.Range(0f, FullTurn), 0f);
            Instantiate(prefab, patch.Point, rotation, transform).Grow(_duration - _time, _meltDuration);
        }

        private bool Covers(Vector3 position)
        {
            if (_time >= _duration)
            {
                return false;
            }

            float reach = _slipRadius * _slipRadius;
            foreach (Vector3 point in _floor)
            {
                Vector3 offset = point - position;
                float height = offset.y;
                offset.y = 0f;
                if (Mathf.Abs(height) <= _slipHeight && offset.sqrMagnitude <= reach)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
