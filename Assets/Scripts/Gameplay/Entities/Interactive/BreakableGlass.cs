using System;
using System.Collections.Generic;
using Services.Audio;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Gameplay.Entities.Interactive
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class BreakableGlass : MonoBehaviour, IChoppable
    {
        private sealed class Region
        {
            public Vector2 Min;
            public Vector2 Max;
            public GlassShardSet Set;
            public GlassShard Intact;
            public readonly List<GlassShard> Stuck = new();
            public bool IsFractured;
            public bool IsCracked;

            public Vector2 Center => (Min + Max) * 0.5f;

            public bool Contains(Vector2 point) => point.x >= Min.x && point.x <= Max.x && point.y >= Min.y && point.y <= Max.y;

            public float DistanceTo(Vector2 point) => Vector2.Distance(point, Vector2.Max(Min, Vector2.Min(Max, point)));
        }

        private const float MinShardArea = 0.0003f;
        private const float MinColliderThickness = 0.006f;
        private const float OuterRadius = 3f;
        private const float EdgeEpsilon = 0.002f;
        private const int ToothRing = 100;

        [SerializeField] private GlassShardSet _shardSetPrefab;
        [SerializeField] private MeshRenderer _pane;
        [SerializeField] private Collider _paneCollider;
        [SerializeField] private Transform[] _dividers = Array.Empty<Transform>();
        [SerializeField] private FramePiece[] _supports = Array.Empty<FramePiece>();
        [SerializeField] private Material _shardMaterial;
        [SerializeField] private Material _crackMaterial;

        [Header("Fracture")]
        [SerializeField, Min(3)] private int _spokes = 8;
        [SerializeField] private float[] _ringFractions = { 0.07f, 0.2f, 0.45f };
        [SerializeField, Min(0)] private int _splitFromRing = 1;
        [SerializeField, Range(0f, 0.5f)] private float _angleJitter = 0.35f;
        [SerializeField, Range(0f, 0.5f)] private float _radiusJitter = 0.3f;
        [SerializeField] private float _crackGap = 0.009f;
        [SerializeField] private float _crackSizeFactor = 2.2f;
        [SerializeField, Min(2)] private int _edgeSamples = 5;

        [Header("Release")]
        [SerializeField] private float _firstReleaseRadius = 0.12f;

        [Header("Teeth")]
        [SerializeField] private float _toothInset = 0.13f;
        [SerializeField] private Vector2 _toothDepth = new(0.05f, 0.12f);
        [SerializeField] private Vector2 _toothWidth = new(0.05f, 0.11f);
        [SerializeField, Range(0f, 1f)] private float _toothChance = 0.7f;
        [SerializeField] private float _releaseRadius = 0.28f;
        [SerializeField] private float _burstSpeed = 1.8f;
        [SerializeField] private float _spreadSpeed = 0.7f;
        [SerializeField] private float _liftSpeed = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _backfallChance = 0.35f;
        [SerializeField] private float _backfallSpeed = 0.6f;
        [SerializeField] private float _spinSpeed = 12f;
        [SerializeField] private float _dropSpeed = 0.4f;
        [SerializeField] private float _joltShatterRadius = 0.18f;
        [SerializeField] private float _destroyRadius = 1.2f;
        [SerializeField] private float _shardLifetime = 30f;

        [Header("Feedback")]
        [SerializeField] private SoundType _shatterSound = SoundType.AxeHitGlass;
        [SerializeField] private float _shatterPitch = 0.85f;
        [SerializeField] private float _crackPitch = 1.6f;
        [SerializeField] private GameObject _shatterEffect;
        [SerializeField] private GameObject _dustEffect;
        [SerializeField] private float _effectLifetime = 3f;

        private readonly List<Region> _regions = new();
        private readonly List<Mesh> _meshes = new();
        private Vector3 _size;
        private bool _isDestroyed;

        public bool IsShattered { get; private set; }

        private void OnEnable()
        {
            foreach (FramePiece support in _supports)
            {
                support.Changed += HandleSupportChanged;
            }
        }

        private void OnDisable()
        {
            foreach (FramePiece support in _supports)
            {
                support.Changed -= HandleSupportChanged;
            }

            foreach (Region region in _regions)
            {
                // Scene teardown destroys the shard sets before this component, so they may already be gone.
                if (region.Set == null)
                {
                    continue;
                }

                region.Intact.gameObject.SetActive(false);
                foreach (GlassShard shard in region.Stuck)
                {
                    shard.gameObject.SetActive(false);
                }

                region.Stuck.Clear();
            }
        }

        public void Chop(Vector3 point, Vector3 direction)
        {
            EnsureShattered();
            Vector2 local = ToLocal(point);
            Region region = FindRegion(local);
            if (!region.IsFractured)
            {
                FractureRegion(region, local, direction, point);
                Release(region, point, _firstReleaseRadius, direction, ToothRing);
                return;
            }

            Release(region, point, _releaseRadius, direction, int.MaxValue);
        }

        public void Restore()
        {
            foreach (Region region in _regions)
            {
                Object.Destroy(region.Set.gameObject);
            }

            _regions.Clear();
            IsShattered = false;
            _isDestroyed = false;
            _pane.enabled = true;
            _paneCollider.enabled = true;
            foreach (FramePiece support in _supports)
            {
                support.Changed -= HandleSupportChanged;
                support.Restore();
                support.Changed += HandleSupportChanged;
            }
        }

        public void Smash(Vector3 point, Vector3 direction, bool includeOuterFrame)
        {
            if (_isDestroyed)
            {
                return;
            }

            _isDestroyed = includeOuterFrame;
            EnsureShattered();
            foreach (FramePiece support in _supports)
            {
                if (includeOuterFrame || support.IsInner)
                {
                    support.Changed -= HandleSupportChanged;
                    support.BreakAll(point, direction);
                }
            }

            foreach (Region region in _regions)
            {
                if (!region.IsFractured)
                {
                    FractureRegion(region, Vector2.Max(region.Min, Vector2.Min(region.Max, ToLocal(point))), direction, point);
                }

                Release(region, point, _destroyRadius, direction, includeOuterFrame ? int.MaxValue : ToothRing, includeOuterFrame);
                ReleaseUnheldTeeth(region);
            }
        }

        private void ReleaseUnheldTeeth(Region region)
        {
            for (int i = region.Stuck.Count - 1; i >= 0; i--)
            {
                GlassShard shard = region.Stuck[i];
                if (!IsHeld(shard.Contacts))
                {
                    region.Stuck.RemoveAt(i);
                    shard.Free(Random.insideUnitSphere * _dropSpeed, Random.insideUnitSphere * _spinSpeed * 0.3f, _shardLifetime + Random.value);
                }
            }
        }

        public void Jolt(Vector3 point, Vector3 direction, float radius)
        {
            EnsureShattered();
            Vector2 local = ToLocal(point);
            foreach (Region region in _regions)
            {
                if (region.IsFractured || region.DistanceTo(local) > radius)
                {
                    continue;
                }

                Vector2 nearest = Vector2.Max(region.Min, Vector2.Min(region.Max, local));
                if (!region.IsCracked && region.DistanceTo(local) > _joltShatterRadius)
                {
                    Crack(region, nearest, point);
                    continue;
                }

                FractureRegion(region, nearest, direction, point);
                Release(region, point, _firstReleaseRadius, direction, ToothRing);
            }
        }

        private void EnsureShattered()
        {
            if (IsShattered)
            {
                return;
            }

            IsShattered = true;
            _size = transform.lossyScale;
            var half = new Vector2(_size.x * 0.5f, _size.y * 0.5f);
            var splitsX = new List<(float from, float to)>();
            var splitsY = new List<(float from, float to)>();
            foreach (Transform divider in _dividers)
            {
                Vector2 a = ToLocal(divider.TransformPoint(new Vector3(-0.5f, -0.5f, 0f)));
                Vector2 b = ToLocal(divider.TransformPoint(new Vector3(0.5f, 0.5f, 0f)));
                Vector2 min = Vector2.Min(a, b);
                Vector2 max = Vector2.Max(a, b);
                if (max.y - min.y > max.x - min.x)
                {
                    splitsX.Add((min.x, max.x));
                }
                else
                {
                    splitsY.Add((min.y, max.y));
                }
            }

            foreach ((float x0, float x1) in Spans(-half.x, half.x, splitsX))
            {
                foreach ((float y0, float y1) in Spans(-half.y, half.y, splitsY))
                {
                    var region = new Region { Min = new Vector2(x0, y0), Max = new Vector2(x1, y1) };
                    region.Set = Instantiate(_shardSetPrefab, transform.position, transform.rotation);
                    region.Set.Bind(this);
                    GlassShard[] shards = region.Set.Shards;
                    for (int i = 1; i < shards.Length; i++)
                    {
                        shards[i].gameObject.SetActive(false);
                    }

                    region.Intact = shards[0];
                    region.Intact.IsIntact = true;
                    region.Intact.Renderer.sharedMaterials = new[] { _pane.sharedMaterial };
                    BuildShard(region.Intact, Rect(region), 0f, region.Center, 1f);
                    _regions.Add(region);
                }
            }

            _pane.enabled = false;
            _paneCollider.enabled = false;
        }

        private void Crack(Region region, Vector2 center, Vector3 point)
        {
            region.IsCracked = true;
            float size = Vector2.Distance(region.Min, region.Max) * _crackSizeFactor;
            BuildShard(region.Intact, Rect(region), 0f, center, size);
            region.Intact.Renderer.sharedMaterials = new[] { _pane.sharedMaterial, _crackMaterial };
            AudioService.Instance.PlayAt(_shatterSound, point, _crackPitch);
        }

        private void FractureRegion(Region region, Vector2 center, Vector3 direction, Vector3 point)
        {
            region.IsFractured = true;
            Vector2 inset = Vector2.one * _toothInset;
            Vector2 innerMin = Vector2.Min(region.Min + inset, region.Center);
            Vector2 innerMax = Vector2.Max(region.Max - inset, region.Center);
            List<(List<Vector2> polygon, int ring)> cells = BuildCells(Vector2.Max(innerMin, Vector2.Min(innerMax, center)), innerMin, innerMax);
            cells.AddRange(BuildTeeth(region));
            GlassShard[] shards = region.Set.Shards;
            for (int i = 0; i < shards.Length; i++)
            {
                GlassShard shard = shards[i];
                shard.IsIntact = false;
                bool isBuilt = i < cells.Count && cells[i].polygon.Count >= 3 && Area(cells[i].polygon) >= MinShardArea;
                shard.gameObject.SetActive(isBuilt);
                if (!isBuilt)
                {
                    continue;
                }

                shard.Ring = cells[i].ring;
                shard.Renderer.sharedMaterials = new[] { _shardMaterial };
                BuildShard(shard, cells[i].polygon, _crackGap, center, 1f);
                shard.Contacts = EdgeContacts(region, cells[i].polygon);
                region.Stuck.Add(shard);
            }

            AudioService.Instance.PlayAt(_shatterSound, point, _shatterPitch);
            if (direction != Vector3.zero)
            {
                Spawn(_shatterEffect, point, Quaternion.LookRotation(-direction));
                Spawn(_dustEffect, point, Quaternion.LookRotation(direction));
            }
        }

        private Vector3[] EdgeContacts(Region region, List<Vector2> polygon)
        {
            var contacts = new List<Vector3>();
            foreach (Vector2 p in polygon)
            {
                bool onEdge = Mathf.Abs(p.x - region.Min.x) < EdgeEpsilon || Mathf.Abs(p.x - region.Max.x) < EdgeEpsilon
                    || Mathf.Abs(p.y - region.Min.y) < EdgeEpsilon || Mathf.Abs(p.y - region.Max.y) < EdgeEpsilon;
                if (onEdge)
                {
                    contacts.Add(ToWorld(p));
                }
            }

            return contacts.ToArray();
        }

        private void HandleSupportChanged(FramePiece support)
        {
            EnsureShattered();
            foreach (Region region in _regions)
            {
                if (!region.IsFractured)
                {
                    if (!IsRegionHeld(region))
                    {
                        Vector3 center = ToWorld(region.Center);
                        FractureRegion(region, region.Center, Vector3.zero, center);
                        Release(region, center, float.MaxValue, Vector3.zero, int.MaxValue);
                    }

                    continue;
                }

                for (int i = region.Stuck.Count - 1; i >= 0; i--)
                {
                    GlassShard shard = region.Stuck[i];
                    if (!IsHeld(shard.Contacts))
                    {
                        region.Stuck.RemoveAt(i);
                        shard.Free(Random.insideUnitSphere * _dropSpeed, Random.insideUnitSphere * _spinSpeed * 0.3f, _shardLifetime + Random.value);
                    }
                }
            }
        }

        private bool IsRegionHeld(Region region)
        {
            List<Vector2> rect = Rect(region);
            for (int edge = 0; edge < rect.Count; edge++)
            {
                Vector2 from = rect[edge];
                Vector2 to = rect[(edge + 1) % rect.Count];
                for (int i = 0; i < _edgeSamples; i++)
                {
                    if (IsSolid(ToWorld(Vector2.Lerp(from, to, (i + 0.5f) / _edgeSamples))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsHeld(Vector3[] contacts)
        {
            foreach (Vector3 contact in contacts)
            {
                if (IsSolid(contact))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSolid(Vector3 point)
        {
            foreach (FramePiece support in _supports)
            {
                if (support.IsSolidAt(point))
                {
                    return true;
                }
            }

            return false;
        }

        private List<(List<Vector2> polygon, int ring)> BuildTeeth(Region region)
        {
            var teeth = new List<(List<Vector2> polygon, int ring)>();
            List<Vector2> rect = Rect(region);
            Vector2 center = region.Center;
            for (int edge = 0; edge < rect.Count; edge++)
            {
                Vector2 from = rect[edge];
                Vector2 to = rect[(edge + 1) % rect.Count];
                float length = Vector2.Distance(from, to);
                Vector2 along = (to - from) / length;
                Vector2 inward = new Vector2(-along.y, along.x);
                if (Vector2.Dot(inward, center - from) < 0f)
                {
                    inward = -inward;
                }

                float cursor = 0f;
                while (cursor < length)
                {
                    float width = Mathf.Min(Random.Range(_toothWidth.x, _toothWidth.y), length - cursor);
                    if (Random.value < _toothChance)
                    {
                        Vector2 a = from + along * cursor;
                        Vector2 b = from + along * (cursor + width);
                        Vector2 apex = from + along * (cursor + width * Random.Range(0.2f, 0.8f)) + inward * Random.Range(_toothDepth.x, _toothDepth.y);
                        teeth.Add((new List<Vector2> { a, b, apex }, ToothRing));
                    }

                    cursor += width;
                }
            }

            return teeth;
        }

        private List<(List<Vector2> polygon, int ring)> BuildCells(Vector2 center, Vector2 min, Vector2 max)
        {
            float scale = Vector2.Distance(min, max);
            int rings = _ringFractions.Length + 1;
            var points = new Vector2[rings, _spokes];
            var angles = new float[_spokes];
            float offset = Random.value;
            for (int j = 0; j < _spokes; j++)
            {
                angles[j] = (j + offset + Random.Range(-_angleJitter, _angleJitter)) / _spokes * Mathf.PI * 2f;
            }

            for (int i = 0; i < rings; i++)
            {
                float radius = i < _ringFractions.Length ? _ringFractions[i] * scale : OuterRadius;
                for (int j = 0; j < _spokes; j++)
                {
                    float jittered = radius * (1f + Random.Range(-_radiusJitter, _radiusJitter));
                    points[i, j] = center + new Vector2(Mathf.Cos(angles[j]), Mathf.Sin(angles[j])) * jittered;
                }
            }

            var cells = new List<(List<Vector2> polygon, int ring)>();
            for (int j = 0; j < _spokes; j++)
            {
                int next = (j + 1) % _spokes;
                cells.Add((Clip(new List<Vector2> { center, points[0, j], points[0, next] }, min, max), 0));
                for (int i = 0; i < rings - 1; i++)
                {
                    Vector2 a = points[i, j];
                    Vector2 b = points[i + 1, j];
                    Vector2 c = points[i + 1, next];
                    Vector2 d = points[i, next];
                    if (i < _splitFromRing)
                    {
                        cells.Add((Clip(new List<Vector2> { a, b, c, d }, min, max), i + 1));
                        continue;
                    }

                    bool flip = Random.value > 0.5f;
                    cells.Add((Clip(flip ? new List<Vector2> { a, b, c } : new List<Vector2> { a, b, d }, min, max), i + 1));
                    cells.Add((Clip(flip ? new List<Vector2> { a, c, d } : new List<Vector2> { b, c, d }, min, max), i + 1));
                }
            }

            return cells;
        }

        private void BuildShard(GlassShard shard, List<Vector2> polygon, float gap, Vector2 uvCenter, float uvSize)
        {
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in polygon)
            {
                centroid += p;
            }

            centroid /= polygon.Count;
            var local = new List<Vector2>(polygon.Count);
            var uvs = new List<Vector2>(polygon.Count);
            foreach (Vector2 p in polygon)
            {
                Vector2 offset = p - centroid;
                local.Add(offset - offset.normalized * Mathf.Min(gap, offset.magnitude * 0.5f));
                uvs.Add((p - uvCenter) / uvSize + Vector2.one * 0.5f);
            }

            Mesh mesh = Extrude(local, uvs, _size.z, (centroid - uvCenter) / uvSize + Vector2.one * 0.5f);
            _meshes.Add(mesh);
            shard.Place(new Vector3(centroid.x, centroid.y, 0f), Quaternion.identity);
            shard.Filter.sharedMesh = mesh;
            Vector3 box = mesh.bounds.size;
            box.z = Mathf.Max(box.z, MinColliderThickness);
            shard.Collider.center = mesh.bounds.center;
            shard.Collider.size = box;
            shard.Collider.enabled = true;
            shard.Body.isKinematic = true;
        }

        private void Release(Region region, Vector3 point, float radius, Vector3 direction, int keepFromRing, bool knocksKeptNearHit = true)
        {
            for (int i = region.Stuck.Count - 1; i >= 0; i--)
            {
                GlassShard shard = region.Stuck[i];
                float distance = Vector3.Distance(shard.transform.position, point);
                if (shard.Ring >= keepFromRing && (distance > radius || !knocksKeptNearHit))
                {
                    continue;
                }

                region.Stuck.RemoveAt(i);
                // Shards near the blow get the full push; ones further out mostly just drop out of the frame.
                float falloff = radius < float.MaxValue ? Mathf.Clamp01(1f - distance / radius) : 0f;
                Vector3 push = Random.value < _backfallChance ? -direction * _backfallSpeed : direction * (_burstSpeed * (0.2f + 0.8f * falloff));
                Vector3 velocity = push + Random.insideUnitSphere * (_spreadSpeed * (0.3f + 0.7f * falloff))
                    + Vector3.up * (_liftSpeed * falloff) + Random.insideUnitSphere * _dropSpeed;
                shard.Free(velocity, Random.insideUnitSphere * _spinSpeed, _shardLifetime + Random.Range(0f, 1f));
            }
        }

        private void Spawn(GameObject prefab, Vector3 point, Quaternion rotation)
        {
            if (prefab != null)
            {
                Destroy(Instantiate(prefab, point, rotation), _effectLifetime);
            }
        }

        private Region FindRegion(Vector2 local)
        {
            Region best = _regions[0];
            float bestDistance = float.MaxValue;
            foreach (Region region in _regions)
            {
                if (region.Contains(local))
                {
                    return region;
                }

                float distance = region.DistanceTo(local);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = region;
                }
            }

            return best;
        }

        private static List<Vector2> Rect(Region region) => new() { region.Min, new(region.Max.x, region.Min.y), region.Max, new(region.Min.x, region.Max.y) };

        private Vector2 ToLocal(Vector3 world)
        {
            Vector3 unit = transform.InverseTransformPoint(world);
            Vector3 size = IsShattered ? _size : transform.lossyScale;
            return new Vector2(unit.x * size.x, unit.y * size.y);
        }

        private Vector3 ToWorld(Vector2 local) => transform.TransformPoint(new Vector3(local.x / _size.x, local.y / _size.y, 0f));

        private static List<(float from, float to)> Spans(float from, float to, List<(float from, float to)> splits)
        {
            splits.Sort((a, b) => a.from.CompareTo(b.from));
            var spans = new List<(float from, float to)>();
            float cursor = from;
            foreach ((float start, float end) in splits)
            {
                if (start > cursor)
                {
                    spans.Add((cursor, Mathf.Min(start, to)));
                }

                cursor = Mathf.Max(cursor, end);
            }

            if (cursor < to)
            {
                spans.Add((cursor, to));
            }

            return spans;
        }

        private static Mesh Extrude(List<Vector2> polygon, List<Vector2> polygonUvs, float thickness, Vector2 centerUv)
        {
            int count = polygon.Count;
            float halfDepth = thickness * 0.5f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -halfDepth : halfDepth;
                vertices.Add(new Vector3(0f, 0f, z));
                uvs.Add(centerUv);
                for (int i = 0; i < count; i++)
                {
                    vertices.Add(new Vector3(polygon[i].x, polygon[i].y, z));
                    uvs.Add(polygonUvs[i]);
                }
            }

            int back = count + 1;
            bool isClockwise = SignedArea(polygon) < 0f;
            for (int i = 0; i < count; i++)
            {
                int a = 1 + i;
                int b = 1 + (i + 1) % count;
                AddTriangle(triangles, 0, isClockwise ? a : b, isClockwise ? b : a);
                AddTriangle(triangles, back, back + (isClockwise ? b : a), back + (isClockwise ? a : b));
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int start = vertices.Count;
                vertices.Add(new Vector3(polygon[i].x, polygon[i].y, -halfDepth));
                vertices.Add(new Vector3(polygon[next].x, polygon[next].y, -halfDepth));
                vertices.Add(new Vector3(polygon[next].x, polygon[next].y, halfDepth));
                vertices.Add(new Vector3(polygon[i].x, polygon[i].y, halfDepth));
                uvs.Add(polygonUvs[i]);
                uvs.Add(polygonUvs[next]);
                uvs.Add(polygonUvs[next]);
                uvs.Add(polygonUvs[i]);
                if (isClockwise)
                {
                    AddTriangle(triangles, start, start + 2, start + 1);
                    AddTriangle(triangles, start, start + 3, start + 2);
                }
                else
                {
                    AddTriangle(triangles, start, start + 1, start + 2);
                    AddTriangle(triangles, start, start + 2, start + 3);
                }
            }

            var mesh = new Mesh { name = "GlassShard" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        // Shoelace formula: Σ (x_i·y_{i+1} − x_{i+1}·y_i) / 2, positive for counter-clockwise polygons.
        private static float SignedArea(List<Vector2> polygon)
        {
            float sum = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                sum += a.x * b.y - b.x * a.y;
            }

            return sum * 0.5f;
        }

        private static float Area(List<Vector2> polygon) => Mathf.Abs(SignedArea(polygon));

        private static List<Vector2> Clip(List<Vector2> polygon, Vector2 min, Vector2 max)
        {
            polygon = ClipEdge(polygon, p => p.x >= min.x, (a, b) => Intersect(a, b, min.x, true));
            polygon = ClipEdge(polygon, p => p.x <= max.x, (a, b) => Intersect(a, b, max.x, true));
            polygon = ClipEdge(polygon, p => p.y >= min.y, (a, b) => Intersect(a, b, min.y, false));
            return ClipEdge(polygon, p => p.y <= max.y, (a, b) => Intersect(a, b, max.y, false));
        }

        // Sutherland–Hodgman: keep inside points, add the crossing point wherever an edge leaves or enters.
        private static List<Vector2> ClipEdge(List<Vector2> polygon, Func<Vector2, bool> inside, Func<Vector2, Vector2, Vector2> cross)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                bool currentIn = inside(current);
                if (currentIn != inside(previous))
                {
                    result.Add(cross(previous, current));
                }

                if (currentIn)
                {
                    result.Add(current);
                }
            }

            return result;
        }

        private static Vector2 Intersect(Vector2 a, Vector2 b, float value, bool alongX)
        {
            float t = alongX ? (value - a.x) / (b.x - a.x) : (value - a.y) / (b.y - a.y);
            return Vector2.Lerp(a, b, t);
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _meshes)
            {
                Destroy(mesh);
            }
        }
    }
}
