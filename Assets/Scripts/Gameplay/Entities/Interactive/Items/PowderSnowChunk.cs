using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class PowderSnowChunk : MonoBehaviour
    {
        private enum VertexState : byte
        {
            Unknown,
            Valid,
            Blocked,
            Fading,
        }

        private const int MaxProbeHits = 8;
        private const float ProbeLift = 0.2f;
        private const float ProbeDepth = 0.4f;
        private const float SurfaceTolerance = 0.05f;
        private const float MinNormalMatch = 0.85f;
        private const float BlockProbeRadius = 0.02f;
        private const float BlockProbeGap = 0.03f;

        private const float MinFloorUp = 0.7f;
        private const float PressHeight = 0.3f;
        private const float OwnerCheckInterval = 0.2f;
        private const float OwnerPositionTolerance = 0.005f;
        private const float OwnerAngleTolerance = 0.5f;
        private const float FloorFilm = 1f;
        private const float WallFilm = 0.88f;
        private const float FadeDuration = 0.7f;
        private const int EdgeSearchSteps = 5;
        private const float PackedRecovery = 0.97f;

        private static readonly List<PowderSnowChunk> ActiveList = new();
        private static readonly Vector2Int[] Neighbours = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        private static readonly Collider[] OverlapHits = new Collider[MaxProbeHits];
        private static readonly RaycastHit[] RayHits = new RaycastHit[MaxProbeHits];

        [SerializeField] private MeshFilter _meshFilter;

        private readonly List<int> _triangles = new();
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private float _maxDepth;
        private float[] _depths;
        private VertexState[] _states;
        private int _side;
        private int _cells;
        private float _cellSize;
        private float _sink;
        private LayerMask _surfaceMask;
        private bool _isDirty;
        private readonly List<Collider> _owners = new();
        private readonly List<Pose> _ownerPoses = new();
        private int[] _ownerIndex;
        private float _ownerTimer;
        private float _film;
        private int _fadingCount;

        public void Setup(int cells, float cellSize, float sink, LayerMask surfaceMask, float maxDepth)
        {
            _maxDepth = maxDepth;
            _side = cells + 1;
            _cells = cells;
            _cellSize = cellSize;
            _sink = sink;
            _surfaceMask = surfaceMask;
            _depths = new float[_side * _side];
            _states = new VertexState[_side * _side];
            _vertices = new Vector3[_side * _side];
            _colors = new Color[_side * _side];
            _ownerIndex = new int[_side * _side];
            _film = transform.up.y >= MinFloorUp ? FloorFilm : WallFilm;

            for (int z = 0; z < _side; z++)
            {
                for (int x = 0; x < _side; x++)
                {
                    _vertices[z * _side + x] = new Vector3(x * cellSize, -sink, z * cellSize);
                }
            }

            _mesh = new Mesh { name = "PowderSnow" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _meshFilter.sharedMesh = _mesh;
        }

        public void AddDepth(int x, int z, float amount, float maxDepth)
        {
            int index = z * _side + x;
            if (_states[index] == VertexState.Fading)
            {
                _states[index] = VertexState.Unknown;
                _fadingCount--;
            }

            if (_states[index] == VertexState.Unknown)
            {
                Vector3 grid = GridPoint(x, z);
                _vertices[index].x = grid.x;
                _vertices[index].z = grid.z;
                bool isValid = ProbeSurface(grid, out Collider surface) || TrySnapToEdge(x, z, index, out surface);
                _states[index] = isValid ? VertexState.Valid : VertexState.Blocked;
                if (isValid)
                {
                    _ownerIndex[index] = OwnerOf(surface);
                }
            }

            if (_states[index] == VertexState.Blocked)
            {
                return;
            }

            _colors[index].b *= PackedRecovery;
            SetDepth(index, Mathf.Min(maxDepth, _depths[index] + amount));
        }

        public static float SampleDepth(Vector3 point)
        {
            float depth = 0f;
            for (int i = 0; i < ActiveList.Count; i++)
            {
                depth = Mathf.Max(depth, ActiveList[i].DepthAt(point));
            }

            return depth;
        }

        public static void MeltAll(Vector3 point, float radius, float amount)
        {
            for (int i = 0; i < ActiveList.Count; i++)
            {
                ActiveList[i].Melt(point, radius, amount);
            }
        }

        private float DepthAt(Vector3 point)
        {
            if (!TryGetFloorLocal(point, out Vector3 local))
            {
                return 0f;
            }

            int x = Mathf.RoundToInt(local.x / _cellSize);
            int z = Mathf.RoundToInt(local.z / _cellSize);
            return x < 0 || z < 0 || x > _cells || z > _cells ? 0f : _depths[z * _side + x];
        }

        private void Melt(Vector3 point, float radius, float amount)
        {
            if (!TryGetFloorLocal(point, out Vector3 local))
            {
                return;
            }

            float radiusSqr = radius * radius;
            int minX = Mathf.Max(0, Mathf.FloorToInt((local.x - radius) / _cellSize));
            int maxX = Mathf.Min(_cells, Mathf.CeilToInt((local.x + radius) / _cellSize));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((local.z - radius) / _cellSize));
            int maxZ = Mathf.Min(_cells, Mathf.CeilToInt((local.z + radius) / _cellSize));
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = z * _side + x;
                    float dx = x * _cellSize - local.x;
                    float dz = z * _cellSize - local.z;
                    float distanceSqr = dx * dx + dz * dz;
                    if (_depths[index] <= 0f || distanceSqr >= radiusSqr)
                    {
                        continue;
                    }

                    // Linear falloff: the fire core melts at full rate, the edge of its radius not at all.
                    float falloff = 1f - Mathf.Sqrt(distanceSqr) / radius;
                    SetDepth(index, Mathf.Max(0f, _depths[index] - amount * falloff));
                }
            }
        }

        private bool TryGetFloorLocal(Vector3 point, out Vector3 local)
        {
            local = transform.InverseTransformPoint(point);
            return transform.up.y >= MinFloorUp && Mathf.Abs(local.y) <= PressHeight;
        }

        private void SetDepth(int index, float depth)
        {
            _depths[index] = depth;
            _vertices[index].y = depth - _sink;
            _colors[index].r = depth / _maxDepth;
            _colors[index].g = _film;
            _isDirty = true;
        }

        public static IReadOnlyList<PowderSnowChunk> Active => ActiveList;

        public void Press(Vector3 point, Vector3 forward, float halfWidth, float halfLength, float keep)
        {
            if (transform.up.y < MinFloorUp)
            {
                return;
            }

            Vector3 local = transform.InverseTransformPoint(point);
            if (Mathf.Abs(local.y) > PressHeight)
            {
                return;
            }

            Vector3 localForward = transform.InverseTransformDirection(forward);
            var along = new Vector2(localForward.x, localForward.z).normalized;
            var across = new Vector2(along.y, -along.x);
            int minX = Mathf.Max(0, Mathf.FloorToInt((local.x - halfLength) / _cellSize));
            int maxX = Mathf.Min(_cells, Mathf.CeilToInt((local.x + halfLength) / _cellSize));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((local.z - halfLength) / _cellSize));
            int maxZ = Mathf.Min(_cells, Mathf.CeilToInt((local.z + halfLength) / _cellSize));

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int index = z * _side + x;
                    if (_depths[index] <= 0f)
                    {
                        continue;
                    }

                    var offset = new Vector2(x * _cellSize - local.x, z * _cellSize - local.z);
                    float u = Vector2.Dot(offset, across) / halfWidth;
                    float v = Vector2.Dot(offset, along) / halfLength;
                    float ellipse = u * u + v * v;
                    if (ellipse >= 1f)
                    {
                        continue;
                    }

                    // Soft rim: the sole packs the powder down to a fixed thickness in the middle and leaves the edge untouched,
                    // so stamping the same spot every frame never digs deeper.
                    float rim = ellipse * ellipse;
                    float limit = Mathf.Lerp(_maxDepth * keep, _depths[index], rim);
                    _colors[index].b = Mathf.Max(_colors[index].b, 1f - rim);
                    if (_depths[index] > limit)
                    {
                        SetDepth(index, limit);
                    }

                    _isDirty = true;
                }
            }
        }

        private void OnEnable() => ActiveList.Add(this);

        private void OnDisable() => ActiveList.Remove(this);

        private Vector3 GridPoint(int x, int z) => new(x * _cellSize, 0f, z * _cellSize);

        // A grid point just past a ledge or under an obstacle slides back to the last spot that still has surface,
        // so the layer runs right up to edges and corners instead of stopping a cell short.
        private bool TrySnapToEdge(int x, int z, int index, out Collider surface)
        {
            Vector3 grid = GridPoint(x, z);
            foreach (Vector2Int offset in Neighbours)
            {
                Vector3 inner = GridPoint(x + offset.x, z + offset.y);
                if (!ProbeSurface(inner, out surface))
                {
                    continue;
                }

                float valid = 0f;
                float blocked = 1f;
                for (int i = 0; i < EdgeSearchSteps; i++)
                {
                    float middle = (valid + blocked) * 0.5f;
                    if (ProbeSurface(Vector3.Lerp(inner, grid, middle), out Collider hit))
                    {
                        valid = middle;
                        surface = hit;
                    }
                    else
                    {
                        blocked = middle;
                    }
                }

                Vector3 snapped = Vector3.Lerp(inner, grid, valid);
                _vertices[index].x = snapped.x;
                _vertices[index].z = snapped.z;
                return true;
            }

            surface = null;
            return false;
        }

        private void TickFading(float deltaTime)
        {
            float step = _maxDepth * deltaTime / FadeDuration;
            for (int vertex = 0; vertex < _states.Length; vertex++)
            {
                if (_states[vertex] != VertexState.Fading)
                {
                    continue;
                }

                float depth = Mathf.Max(0f, _depths[vertex] - step);
                SetDepth(vertex, depth);
                if (depth <= 0f)
                {
                    _states[vertex] = VertexState.Unknown;
                    _fadingCount--;
                }
            }
        }

        private int OwnerOf(Collider surface)
        {
            int index = _owners.IndexOf(surface);
            if (index >= 0)
            {
                return index;
            }

            _owners.Add(surface);
            _ownerPoses.Add(new Pose(surface.transform.position, surface.transform.rotation));
            return _owners.Count - 1;
        }

        // Powder only exists while the surface it settled on is still there and has not moved.
        private void ReleaseLostOwners()
        {
            for (int i = 0; i < _owners.Count; i++)
            {
                if (_owners[i] is null || IsAttached(_owners[i], _ownerPoses[i]))
                {
                    continue;
                }

                _owners[i] = null;
                Physics.SyncTransforms();
                for (int vertex = 0; vertex < _ownerIndex.Length; vertex++)
                {
                    if (_ownerIndex[vertex] != i || _states[vertex] != VertexState.Valid)
                    {
                        continue;
                    }

                    // A pane that only cracked or lost its neighbour still holds its powder: rebind to what is there now.
                    if (ProbeSurface(_vertices[vertex], out Collider replacement))
                    {
                        _ownerIndex[vertex] = OwnerOf(replacement);
                        continue;
                    }

                    _states[vertex] = VertexState.Fading;
                    _fadingCount++;
                }
            }
        }

        private static bool IsAttached(Collider owner, Pose pose)
        {
            if (owner == null || !owner.enabled || !owner.gameObject.activeInHierarchy || !IsSolidScenery(owner))
            {
                return false;
            }

            Transform surface = owner.transform;
            return (surface.position - pose.position).sqrMagnitude <= OwnerPositionTolerance * OwnerPositionTolerance
                && Quaternion.Angle(surface.rotation, pose.rotation) <= OwnerAngleTolerance;
        }

        private bool ProbeSurface(Vector3 local, out Collider owner)
        {
            owner = null;
            Vector3 point = transform.TransformPoint(new Vector3(local.x, 0f, local.z));
            Vector3 normal = transform.up;
            if (!CastToSurface(point + normal * ProbeLift, -normal, out RaycastHit surface)
                || Vector3.Dot(surface.normal, normal) < MinNormalMatch
                || Mathf.Abs(Vector3.Dot(surface.point - point, normal)) > SurfaceTolerance)
            {
                return false;
            }

            Vector3 blockProbe = point + normal * (BlockProbeRadius + BlockProbeGap);
            int count = Physics.OverlapSphereNonAlloc(blockProbe, BlockProbeRadius, OverlapHits, _surfaceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsSolidScenery(OverlapHits[i]))
                {
                    return false;
                }
            }

            owner = surface.collider;
            return true;
        }

        private bool CastToSurface(Vector3 origin, Vector3 direction, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, RayHits, ProbeLift + ProbeDepth, _surfaceMask, QueryTriggerInteraction.Ignore);
            nearest = default;
            nearest.distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (RayHits[i].distance < nearest.distance && IsSolidScenery(RayHits[i].collider))
                {
                    nearest = RayHits[i];
                }
            }

            return nearest.distance < float.MaxValue;
        }

        private static bool IsSolidScenery(Collider collider) =>
            (collider.attachedRigidbody == null || collider.attachedRigidbody.isKinematic)
            && collider is not CharacterController
            && collider.GetComponentInParent<FireSource>() == null;

        private void LateUpdate()
        {
            _ownerTimer -= Time.deltaTime;
            if (_ownerTimer <= 0f)
            {
                _ownerTimer = OwnerCheckInterval;
                ReleaseLostOwners();
            }

            if (_fadingCount > 0)
            {
                TickFading(Time.deltaTime);
            }

            if (!_isDirty)
            {
                return;
            }

            _isDirty = false;
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            RebuildTriangles();
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        private void RebuildTriangles()
        {
            _triangles.Clear();
            for (int z = 0; z < _cells; z++)
            {
                for (int x = 0; x < _cells; x++)
                {
                    int corner = z * _side + x;
                    int a = corner;
                    int b = corner + 1;
                    int c = corner + _side;
                    int d = corner + _side + 1;
                    if (!IsValid(a) || !IsValid(b) || !IsValid(c) || !IsValid(d)
                        || _depths[a] + _depths[b] + _depths[c] + _depths[d] <= 0f)
                    {
                        continue;
                    }

                    _triangles.Add(a);
                    _triangles.Add(c);
                    _triangles.Add(b);
                    _triangles.Add(b);
                    _triangles.Add(c);
                    _triangles.Add(d);
                }
            }

            _mesh.SetTriangles(_triangles, 0);
        }

        private bool IsValid(int index) => _states[index] is VertexState.Valid or VertexState.Fading;

        private void OnDestroy()
        {
            Destroy(_mesh);
        }
    }
}
