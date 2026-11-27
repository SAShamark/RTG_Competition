using Services.Audio;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class AxeLogic
    {
        private const int MaxRayHits = 12;
        private const int EdgeSearchSteps = 6;
        private const float Half = 0.5f;
        private const int IgnoreRaycastLayer = 2;

        [SerializeField] private float _radius = 0.05f;
        [SerializeField] private LayerMask _hitMask = ~0;
        [SerializeField] private AxeImpactLibrary _library;

        [Header("Debris")]
        [SerializeField] private ParticleSystem _debrisPrefab;
        [SerializeField] private float _debrisLifetime = 4f;
        [SerializeField] private Vector2 _debrisSpeed = new(0.8f, 2.6f);
        [SerializeField] private float _debrisSpread = 0.6f;
        [SerializeField] private Vector2 _debrisShade = new(0.7f, 1.1f);
        [SerializeField] private Color _fallbackColor = new(0.6f, 0.6f, 0.6f);

        [Header("Swing")]
        [SerializeField] private Vector3 _bladeOffset = new(-0.33f, 0f, 0f);
        [SerializeField] private float _minStrikeSpeed = 2.2f;
        [SerializeField] private float _fullStrikeSpeed = 5f;
        [SerializeField] private float _swingSoundSpeed = 3.5f;
        [SerializeField] private float _strikeCooldown = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _weakPushFactor = 0.3f;

        [Header("Two hands")]
        [SerializeField, Range(0.1f, 1f)] private float _twoHandedSpeedFactor = 0.75f;
        [SerializeField, Range(0.1f, 1f)] private float _twoHandedCostFactor = 0.6f;

        [Header("Stamina")]
        [SerializeField, Range(0f, 1f)] private float _swingCost = 0.22f;
        [SerializeField] private float _recoverDelay = 0.6f;
        [SerializeField] private float _recoverRate = 0.35f;

        [Header("Marks")]
        [SerializeField, Min(1)] private int _maxMarks = 40;
        [SerializeField] private float _markOffset = 0.006f;
        [SerializeField] private Vector2 _markScale = new(0.85f, 1.15f);
        [SerializeField] private float _edgeProbe = 0.07f;
        [SerializeField] private float _edgeDepth = 0.03f;
        [SerializeField, Range(0f, 1f)] private float _sameFaceNormal = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _edgeFaceNormal = 0.5f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxRayHits];
        private readonly Queue<GameObject> _marks = new();
        private Transform _owner;
        private Transform _trackingSpace;
        private Vector3 _bladeTracked;
        private InventoryLogic _items;
        private int _queryMask;
        private float _recoverTimer;
        private float _cooldown;
        private Vector3 _blade;
        private bool _hasBlade;
        private bool _isSwinging;
        private Transform _support;

        public float Stamina { get; private set; } = 1f;
        public Vector3 SwingDirection { get; private set; }
        public bool IsTwoHanded => _support != null;
        public Vector3 HandleAxis => _bladeOffset.normalized;
        public float HandleLength => _bladeOffset.magnitude;

        public event Action<RaycastHit, AxeImpactLibrary.Impact, float> Struck;

        public void Init(Transform owner, InventoryLogic items, Transform trackingSpace)
        {
            _owner = owner;
            _trackingSpace = trackingSpace;
            _items = items;
            _queryMask = _hitMask & ~(1 << IgnoreRaycastLayer);
        }

        public void SetSupport(Transform support) => _support = support;

        public void Tick(float deltaTime)
        {
            RecoverStamina(deltaTime);
            _cooldown -= deltaTime;
            ToolItem axe = _items.HeldTool;
            if (_items.HeldItem != ItemType.Axe || axe == null || !axe.gameObject.activeInHierarchy || deltaTime <= 0f)
            {
                _hasBlade = false;
                return;
            }

            AlignToSupport(axe.transform);
            Vector3 blade = axe.transform.TransformPoint(_bladeOffset);
            Vector3 tracked = _trackingSpace.InverseTransformPoint(blade);
            Vector3 from = _blade;
            Vector3 trackedFrom = _bladeTracked;
            _blade = blade;
            _bladeTracked = tracked;
            if (!_hasBlade)
            {
                _hasBlade = true;
                return;
            }

            Vector3 travel = blade - from;
            float speed = (tracked - trackedFrom).magnitude / deltaTime;
            TickSwingSound(speed);
            float minSpeed = IsTwoHanded ? _minStrikeSpeed * _twoHandedSpeedFactor : _minStrikeSpeed;
            float cost = IsTwoHanded ? _swingCost * _twoHandedCostFactor : _swingCost;
            if (_cooldown > 0f || speed < minSpeed || Stamina < cost || !TrySweep(from, travel, out RaycastHit hit))
            {
                return;
            }

            _cooldown = _strikeCooldown;
            Stamina = Mathf.Max(0f, Stamina - cost);
            _recoverTimer = _recoverDelay;
            Strike(hit, travel.normalized, Mathf.InverseLerp(minSpeed, _fullStrikeSpeed, speed));
        }

        private void AlignToSupport(Transform axe)
        {
            if (_support == null)
            {
                return;
            }

            Vector3 handle = axe.TransformDirection(HandleAxis);
            Vector3 toSupport = _support.position - axe.position;
            if (toSupport.sqrMagnitude < Mathf.Epsilon)
            {
                return;
            }

            Vector3 target = toSupport.normalized * Mathf.Sign(Vector3.Dot(toSupport, handle));
            axe.rotation = Quaternion.FromToRotation(handle, target) * axe.rotation;
        }

        private void RecoverStamina(float deltaTime)
        {
            if (_recoverTimer > 0f)
            {
                _recoverTimer -= deltaTime;
                return;
            }

            Stamina = Mathf.Min(1f, Stamina + _recoverRate * deltaTime);
        }

        private void TickSwingSound(float speed)
        {
            bool isSwinging = speed >= _swingSoundSpeed;
            if (isSwinging && !_isSwinging)
            {
                AudioService.Instance.Play(SoundType.AxeSwing);
            }

            _isSwinging = isSwinging;
        }

        private void Strike(RaycastHit hit, Vector3 direction, float power)
        {
            SwingDirection = direction;
            AxeImpactLibrary.Impact impact = _library.Get(_library.Resolve(hit.collider));
            AudioService.Instance.PlayAt(impact.Sound, hit.point, impact.Pitch);
            SpawnEffect(impact, hit, direction);
            SpawnDebris(impact, hit, direction);

            if (!impact.BlocksDamage)
            {
                ChopTargets(hit, direction);
                Push(hit, direction, impact.PushForce * Mathf.Lerp(_weakPushFactor, 1f, power));
                PlaceMarks(impact, hit, direction);
            }
            else
            {
                Push(hit, direction, 0f);
            }

            Struck?.Invoke(hit, impact, power);
        }

        private bool TrySweep(Vector3 from, Vector3 travel, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(from, _radius, travel.normalized, _hits, travel.magnitude, _queryMask, QueryTriggerInteraction.Ignore);
            return TryPickNearest(count, out nearest);
        }

        private bool TryPickNearest(int count, out RaycastHit nearest)
        {
            nearest = default;
            nearest.distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance > 0f && hit.distance < nearest.distance && !hit.transform.IsChildOf(_owner))
                {
                    nearest = hit;
                }
            }

            return nearest.distance < float.MaxValue;
        }

        private static void ChopTargets(RaycastHit hit, Vector3 direction)
        {
            foreach (IChoppable target in hit.collider.GetComponentsInParent<IChoppable>())
            {
                target.Chop(hit.point, direction);
            }

            foreach (IChoppable target in hit.collider.GetComponentsInChildren<IChoppable>())
            {
                if (target is Component component && component.transform != hit.collider.transform)
                {
                    target.Chop(hit.point, direction);
                }
            }
        }

        private static void Push(RaycastHit hit, Vector3 direction, float force) =>
            ToolImpact.Apply(hit.collider, new ToolHit(ToolHitKind.Axe, hit.point, direction, force));

        private static void SpawnEffect(AxeImpactLibrary.Impact impact, RaycastHit hit, Vector3 direction)
        {
            if (impact.Effect == null)
            {
                return;
            }

            // Debris leaves along the blade's glancing bounce, half way between the reflection and the surface normal.
            Vector3 spray = (Vector3.Reflect(direction, hit.normal) + hit.normal).normalized;
            GameObject effect = UnityEngine.Object.Instantiate(impact.Effect, hit.point, Quaternion.LookRotation(spray));
            UnityEngine.Object.Destroy(effect, impact.EffectLifetime);
        }

        private void SpawnDebris(AxeImpactLibrary.Impact impact, RaycastHit hit, Vector3 direction)
        {
            if (impact.DebrisCount == 0)
            {
                return;
            }

            Color color = SurfaceColor(hit);
            Vector3 spray = (Vector3.Reflect(direction, hit.normal) + hit.normal).normalized;
            ParticleSystem debris = UnityEngine.Object.Instantiate(_debrisPrefab, hit.point, Quaternion.identity);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < impact.DebrisCount; i++)
            {
                Vector3 heading = (spray + UnityEngine.Random.insideUnitSphere * _debrisSpread).normalized;
                emit.position = hit.point + hit.normal * 0.01f;
                emit.velocity = heading * UnityEngine.Random.Range(_debrisSpeed.x, _debrisSpeed.y);
                emit.startSize = UnityEngine.Random.Range(impact.DebrisSize.x, impact.DebrisSize.y);
                emit.rotation3D = UnityEngine.Random.insideUnitSphere * 180f;
                emit.startColor = color * UnityEngine.Random.Range(_debrisShade.x, _debrisShade.y);
                debris.Emit(emit, 1);
            }

            UnityEngine.Object.Destroy(debris.gameObject, _debrisLifetime);
        }

        private Color SurfaceColor(RaycastHit hit)
        {
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = hit.collider.GetComponentInParent<Renderer>();
            }

            if (renderer == null)
            {
                renderer = hit.collider.GetComponentInChildren<Renderer>();
            }

            if (renderer == null || renderer.sharedMaterial == null)
            {
                return _fallbackColor;
            }

            Material material = renderer.sharedMaterial;
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.HasProperty("_Color") ? material.color : _fallbackColor;
            if (material.mainTexture is Texture2D texture && texture.isReadable && hit.collider is MeshCollider)
            {
                color *= texture.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
            }

            color.a = 1f;
            return color;
        }

        private void PlaceMarks(AxeImpactLibrary.Impact impact, RaycastHit hit, Vector3 direction)
        {
            if (impact.Mark == null || hit.rigidbody != null)
            {
                return;
            }

            Vector3 swing = Vector3.ProjectOnPlane(direction, hit.normal);
            if (swing.sqrMagnitude < Mathf.Epsilon)
            {
                swing = Vector3.ProjectOnPlane(Vector3.up, hit.normal);
            }

            swing.Normalize();
            float scale = UnityEngine.Random.Range(_markScale.x, _markScale.y);
            if (impact.EdgeMark == null || !TryFindEdge(hit, swing, out Vector3 edgePoint, out RaycastHit side))
            {
                PlaceMark(impact.Mark, hit.collider, hit.point, hit.normal, swing, scale);
                return;
            }

            // A blow next to an edge bites the corner itself: the crater is centred on the edge and the chip on the
            // neighbouring face hangs from the same spot, its cut side flush with the edge.
            PlaceMark(impact.Mark, hit.collider, edgePoint, hit.normal, swing, scale);
            Vector3 towardEdge = Vector3.ProjectOnPlane(hit.normal, side.normal).normalized;
            DecalProjector chip = impact.EdgeMark.GetComponent<DecalProjector>();
            float chipScale = chip.scaleMode == DecalScaleMode.InheritFromHierarchy ? scale : 1f;
            Vector3 chipCentre = edgePoint - towardEdge * (chip.size.y * chipScale * Half);
            PlaceMark(impact.EdgeMark, side.collider, chipCentre, side.normal, towardEdge, scale);
        }

        private bool IsOnFace(RaycastHit hit, Vector3 point)
        {
            return Physics.Raycast(point + hit.normal * _edgeDepth, -hit.normal, out RaycastHit face, _edgeDepth * 2f, _hitMask, QueryTriggerInteraction.Ignore)
                && face.collider == hit.collider && Vector3.Dot(face.normal, hit.normal) >= _sameFaceNormal;
        }

        private bool TryFindEdge(RaycastHit hit, Vector3 swing, out Vector3 edgePoint, out RaycastHit edge)
        {
            Vector3 side = Vector3.Cross(hit.normal, swing);
            Vector3 diagonal = (swing + side).normalized;
            Vector3 antiDiagonal = (swing - side).normalized;
            foreach (Vector3 tangent in new[] { swing, -swing, side, -side, diagonal, -diagonal, antiDiagonal, -antiDiagonal })
            {
                Vector3 probe = hit.point + tangent * _edgeProbe;
                if (IsOnFace(hit, probe))
                {
                    continue;
                }

                Vector3 outside = probe - hit.normal * _edgeDepth;
                if (Physics.Raycast(outside, -tangent, out edge, _edgeProbe * 2f, _hitMask, QueryTriggerInteraction.Ignore)
                    && edge.collider == hit.collider && Vector3.Dot(edge.normal, tangent) > _edgeFaceNormal)
                {
                    float onFace = 0f;
                    float offFace = _edgeProbe;
                    for (int i = 0; i < EdgeSearchSteps; i++)
                    {
                        float middle = (onFace + offFace) * Half;
                        if (IsOnFace(hit, hit.point + tangent * middle))
                        {
                            onFace = middle;
                        }
                        else
                        {
                            offFace = middle;
                        }
                    }

                    edgePoint = hit.point + tangent * offFace;
                    return true;
                }
            }

            edgePoint = default;
            edge = default;
            return false;
        }

        private void PlaceMark(GameObject prefab, Collider surface, Vector3 point, Vector3 normal, Vector3 up, float size)
        {
            if (_marks.Count >= _maxMarks)
            {
                GameObject oldest = _marks.Dequeue();
                if (oldest != null)
                {
                    UnityEngine.Object.Destroy(oldest);
                }
            }

            GameObject mark = UnityEngine.Object.Instantiate(prefab, point + normal * _markOffset, Quaternion.LookRotation(-normal, up));
            mark.GetComponent<AxeMark>().Bind(surface);
            mark.transform.localScale *= size;

            Transform target = surface.transform;
            Vector3 scale = target.lossyScale;
            if (Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.y, scale.z))
            {
                mark.transform.SetParent(target, true);
            }

            _marks.Enqueue(mark);
        }
    }
}
