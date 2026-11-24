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
        private const float TangentStep = 2f;
        private const int EdgeSearchSteps = 6;
        private const float Half = 0.5f;

        [SerializeField] private float _reach = 2.4f;
        [SerializeField] private float _radius = 0.08f;
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
        [SerializeField] private float _alternateTilt = 40f;

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
        private Transform _aim;
        private Transform _owner;
        private InventoryLogic _items;
        private float _recoverTimer;

        public float Stamina { get; private set; } = 1f;
        public bool IsReady => _items.HeldItem == ItemType.Axe && Stamina >= _swingCost;
        public Vector3 SwingDirection { get; private set; }

        public event Action<RaycastHit, AxeImpactLibrary.Impact> Struck;
        public event Action Missed;

        public void Init(Transform aim, Transform owner, InventoryLogic items)
        {
            _aim = aim;
            _owner = owner;
            _items = items;
        }

        public void Tick(float deltaTime)
        {
            if (_recoverTimer > 0f)
            {
                _recoverTimer -= deltaTime;
                return;
            }

            Stamina = Mathf.Min(1f, Stamina + _recoverRate * deltaTime);
        }

        public void SpendSwing()
        {
            Stamina = Mathf.Max(0f, Stamina - _swingCost);
            _recoverTimer = _recoverDelay;
        }

        public bool IsAimingAtProtected => TryCast(_aim.forward, out RaycastHit hit) && _library.Get(_library.Resolve(hit.collider)).BlocksDamage;

        public void Strike(bool isAlternate)
        {
            if (!TrySweep(isAlternate, out RaycastHit hit))
            {
                Missed?.Invoke();
                return;
            }

            Vector3 direction = SwingDirection;
            AxeImpactLibrary.Impact impact = _library.Get(_library.Resolve(hit.collider));
            AudioService.Instance.PlayAt(impact.Sound, hit.point, impact.Pitch);
            SpawnEffect(impact, hit, direction);
            SpawnDebris(impact, hit, direction);

            if (!impact.BlocksDamage)
            {
                ChopTargets(hit, direction);
                Push(hit, direction, impact.PushForce);
                PlaceMarks(impact, hit, direction);
            }

            Struck?.Invoke(hit, impact);
        }

        // The hit always lands on the crosshair; the blade's arc only decides which way the edge travels through it.
        private bool TrySweep(bool isAlternate, out RaycastHit hit)
        {
            Vector3 forward = _aim.forward;
            Vector3 axis = Quaternion.AngleAxis(isAlternate ? _alternateTilt : 0f, forward) * _aim.right;
            SwingDirection = ((Quaternion.AngleAxis(TangentStep, axis) * forward) - (Quaternion.AngleAxis(-TangentStep, axis) * forward)).normalized;
            return TryCast(forward, out hit);
        }

        private bool TryRay(Vector3 direction, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(_aim.position, direction, _hits, _reach, _hitMask, QueryTriggerInteraction.Ignore);
            return TryPickNearest(count, out nearest);
        }

        private bool TryCast(Vector3 direction, out RaycastHit nearest)
        {
            if (TryRay(direction, out nearest))
            {
                return true;
            }

            int count = Physics.SphereCastNonAlloc(_aim.position, _radius, direction, _hits, _reach, _hitMask, QueryTriggerInteraction.Ignore);
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

        private static void Push(RaycastHit hit, Vector3 direction, float force)
        {
            Rigidbody body = hit.rigidbody;
            if (body != null && !body.isKinematic)
            {
                body.AddForceAtPosition(direction * force, hit.point, ForceMode.Impulse);
            }
        }

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
                swing = Vector3.ProjectOnPlane(_aim.up, hit.normal);
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
