using System.Collections.Generic;
using Gameplay.Entities.Interactive;
using UnityEditor;
using UnityEngine;

public static class BreakableDoorBaker
{
    private const string MeshAssetPath = "Assets/Contents/Generated/BreakableDoor_Wood_Chunks.asset";
    private const string PrefabPath = "Assets/Contents/Prefabs/Environment/BreakableDoor_Wood.prefab";
    private const string MaterialFolder = "Assets/Contents/Materials/";
    private const string LitTemplatePath = MaterialFolder + "AwningWhite.mat";
    private const string FaceMaterialPath = MaterialFolder + "M_DoorPlank.mat";
    private const string GrainMaterialPath = MaterialFolder + "M_DoorPlankGrain.mat";
    private const string SliverMaterialPath = MaterialFolder + "M_DoorSliver.mat";
    private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
    private const string HandleSourcePath = "Assets/Plugins/polyperfect/Low Poly Ultimate Pack/M/- Meshes_M/Buildings_M/Parts_M/Building Doors_M/door-wood-a.fbx";
    private const string HandleMaterialPath = MaterialFolder + "Brass.mat";
    private const int HandleSubmesh = 0;
    private const float HandleCenterX = -0.9f;
    private const float HandleSourceSurface = 0.05f;
    private const string HandleChunkName = "Plank_0_1";
    private const string SmokePrefabPath = "Assets/Plugins/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR3 Hit Misc F Smoke.prefab";

    private static readonly Color FaceColor = new(0.55f, 0.14f, 0.11f);
    private static readonly Color GrainColor = new(0.96f, 0.64f, 0.36f);

    private const float DoorLeft = -1f;
    private const float DoorRight = 0f;
    private const float DoorBottom = 0f;
    private const float DoorTop = 2.1f;
    private const int PlankCount = 5;
    private const float PlankGap = 0.006f;
    private const float PlankThickness = 0.045f;
    private const int SegmentsPerPlank = 3;
    private const float BreakJitter = 0.18f;
    private const int TeethPerBreak = 2;
    private const float ToothMin = 0.05f;
    private const float ToothMax = 0.15f;
    private const float BattenHeight = 0.12f;
    private const float BattenThickness = 0.035f;
    private static readonly float[] BattenHeights = { 0.45f, 1.75f };
    private const int Seed = 1337;

    private const float ChunkMassPerCubicMeter = 600f;
    private const float SliverLifetime = 0.5f;
    private const float SliverSpeedMin = 2.5f;
    private const float SliverSpeedMax = 5f;
    private const float SliverSize = 0.025f;
    private const float SliverStretch = 4f;
    private const float SliverGravity = 1.5f;
    private const float FlashLifetime = 0.09f;
    private const float FlashSpeed = 9f;
    private const float FlashSize = 0.06f;
    private const float FlashStretch = 6f;
    private const short FlashCount = 6;
    private const float ChipLifetime = 3f;
    private const float ChipSpeedMin = 1f;
    private const float ChipSpeedMax = 3f;
    private const float ChipSizeMin = 0.03f;
    private const float ChipSizeMax = 0.06f;
    private const float ChipGravity = 1f;
    private const float ChipBounce = 0.3f;
    private const short ChipCount = 5;
    private const float ConeAngle = 40f;
    private const int MaxParticles = 100;
    private const float SmokeScale = 0.6f;

    private sealed class ChunkData
    {
        public Mesh Mesh;
        public Vector3 Center;
        public float Volume;
    }

    [MenuItem("Tools/Doors/Bake Breakable Wood Door")]
    public static void Bake()
    {
        Material face = EnsureLitMaterial(FaceMaterialPath, FaceColor);
        Material grain = EnsureLitMaterial(GrainMaterialPath, GrainColor);
        Material sliver = EnsureSliverMaterial();
        var random = new System.Random(Seed);
        var chunks = new List<ChunkData>();

        float plankWidth = (DoorRight - DoorLeft - PlankGap * (PlankCount - 1)) / PlankCount;
        float segmentHeight = (DoorTop - DoorBottom) / SegmentsPerPlank;
        for (int p = 0; p < PlankCount; p++)
        {
            float left = DoorLeft + p * (plankWidth + PlankGap);
            float right = left + plankWidth;
            float[][] breaks = BuildBreaks(random, segmentHeight);
            for (int s = 0; s < SegmentsPerPlank; s++)
            {
                chunks.Add(BuildSegment(left, right, breaks[s], breaks[s + 1], -PlankThickness * 0.5f, PlankThickness * 0.5f, $"Plank_{p}_{s}"));
            }

            for (int b = 0; b < BattenHeights.Length; b++)
            {
                float z = PlankThickness * 0.5f;
                float[] bottom = Flat(BattenHeights[b] - BattenHeight * 0.5f);
                float[] top = Flat(BattenHeights[b] + BattenHeight * 0.5f);
                float battenRight = p < PlankCount - 1 ? right + PlankGap : right;
                chunks.Add(BuildSegment(left, battenRight, bottom, top, z, z + BattenThickness, $"Batten_{p}_{b}"));
            }
        }

        Mesh handle = BuildHandle();
        SaveMeshes(chunks, handle);
        BuildPrefab(chunks, new[] { face, grain }, grain, sliver, handle);
        AssetDatabase.SaveAssets();
    }

    private static float[] Flat(float y)
    {
        var line = new float[TeethPerBreak * 2 + 1];
        for (int i = 0; i < line.Length; i++)
        {
            line[i] = y;
        }

        return line;
    }

    // Each break line is a zigzag shared by the segments above and below it; odd samples form the teeth.
    private static float[][] BuildBreaks(System.Random random, float segmentHeight)
    {
        var breaks = new float[SegmentsPerPlank + 1][];
        breaks[0] = Flat(DoorBottom);
        breaks[SegmentsPerPlank] = Flat(DoorTop);
        for (int s = 1; s < SegmentsPerPlank; s++)
        {
            float baseY = DoorBottom + s * segmentHeight + Range(random, -BreakJitter, BreakJitter) * segmentHeight;
            float slope = Range(random, -ToothMax, ToothMax);
            var line = new float[TeethPerBreak * 2 + 1];
            for (int i = 0; i < line.Length; i++)
            {
                float t = (float)i / (line.Length - 1);
                float tooth = i % 2 == 1 ? Range(random, ToothMin, ToothMax) : -Range(random, 0f, ToothMin);
                line[i] = baseY + slope * (t - 0.5f) + tooth;
            }

            breaks[s] = line;
        }

        return breaks;
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static ChunkData BuildSegment(float left, float right, float[] bottom, float[] top, float front, float back, string name)
    {
        int count = bottom.Length;
        var xs = new float[count];
        float centerY = 0f;
        for (int i = 0; i < count; i++)
        {
            xs[i] = Mathf.Lerp(left, right, (float)i / (count - 1));
            centerY += (bottom[i] + top[i]) * 0.5f;
        }

        Vector3 center = new((left + right) * 0.5f, centerY / count, (front + back) * 0.5f);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var faces = new List<int>();
        var grain = new List<int>();

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 b0 = new(xs[i], bottom[i]);
            Vector3 b1 = new(xs[i + 1], bottom[i + 1]);
            Vector3 t1 = new(xs[i + 1], top[i + 1]);
            Vector3 t0 = new(xs[i], top[i]);
            AddQuad(Z(b0, front), Z(t0, front), Z(t1, front), Z(b1, front), Vector3.back, center, vertices, normals, uvs, faces);
            AddQuad(Z(b0, back), Z(b1, back), Z(t1, back), Z(t0, back), Vector3.forward, center, vertices, normals, uvs, faces);
            AddQuad(Z(b0, front), Z(b1, front), Z(b1, back), Z(b0, back), EdgeNormal(b0, b1, false), center, vertices, normals, uvs, grain);
            AddQuad(Z(t0, front), Z(t0, back), Z(t1, back), Z(t1, front), EdgeNormal(t0, t1, true), center, vertices, normals, uvs, grain);
        }

        Vector3 bl = new(left, bottom[0]);
        Vector3 tl = new(left, top[0]);
        Vector3 br = new(right, bottom[count - 1]);
        Vector3 tr = new(right, top[count - 1]);
        AddQuad(Z(bl, front), Z(bl, back), Z(tl, back), Z(tl, front), Vector3.left, center, vertices, normals, uvs, faces);
        AddQuad(Z(br, front), Z(tr, front), Z(tr, back), Z(br, back), Vector3.right, center, vertices, normals, uvs, faces);

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(faces, 0);
        mesh.SetTriangles(grain, 1);
        mesh.RecalculateBounds();

        Vector3 size = mesh.bounds.size;
        return new ChunkData { Mesh = mesh, Center = center, Volume = size.x * size.y * size.z };
    }

    private static Vector3 Z(Vector3 point, float z) => new(point.x, point.y, z);

    private static Vector3 EdgeNormal(Vector3 a, Vector3 b, bool isTop)
    {
        Vector3 edge = b - a;
        Vector3 normal = new Vector3(-edge.y, edge.x, 0f).normalized;
        return isTop == normal.y > 0f ? normal : -normal;
    }

    private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector3 center,
        List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices)
    {
        int start = vertices.Count;
        foreach (Vector3 corner in new[] { a, b, c, d })
        {
            vertices.Add(corner);
            normals.Add(normal);
            uvs.Add(new Vector2(corner.x + corner.z, corner.y));
        }

        bool isFacing = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f;
        int[] order = isFacing ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
        foreach (int i in order)
        {
            indices.Add(start + i);
        }
    }

    private static Material EnsureLitMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(LitTemplatePath));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureSliverMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SliverMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find(ParticleShader));
            AssetDatabase.CreateAsset(material, SliverMaterialPath);
        }

        material.color = Color.white;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Takes the source door's handle (back side only), moves it onto the first plank and mirrors it to the front.
    private static Mesh BuildHandle()
    {
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(HandleSourcePath);
        Vector3[] positions = source.vertices;
        Vector3[] sourceNormals = source.normals;
        int[] triangles = source.GetTriangles(HandleSubmesh);

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (int index in triangles)
        {
            minX = Mathf.Min(minX, positions[index].x);
            maxX = Mathf.Max(maxX, positions[index].x);
        }

        float shift = HandleCenterX - (minX + maxX) * 0.5f;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        foreach (float side in new[] { 1f, -1f })
        {
            int start = vertices.Count;
            var remap = new Dictionary<int, int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int[] corners = side > 0f
                    ? new[] { triangles[i], triangles[i + 1], triangles[i + 2] }
                    : new[] { triangles[i], triangles[i + 2], triangles[i + 1] };
                foreach (int index in corners)
                {
                    if (!remap.TryGetValue(index, out int mapped))
                    {
                        Vector3 p = positions[index];
                        Vector3 n = sourceNormals[index];
                        vertices.Add(new Vector3(p.x + shift, p.y, (p.z - HandleSourceSurface + PlankThickness * 0.5f) * side));
                        normals.Add(new Vector3(n.x, n.y, n.z * side));
                        mapped = vertices.Count - 1 - start;
                        remap[index] = mapped;
                    }

                    indices.Add(start + mapped);
                }
            }
        }

        var mesh = new Mesh { name = "Handle" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SaveMeshes(List<ChunkData> chunks, Mesh handle)
    {
        AssetDatabase.DeleteAsset(MeshAssetPath);
        AssetDatabase.CreateAsset(chunks[0].Mesh, MeshAssetPath);
        for (int i = 1; i < chunks.Count; i++)
        {
            AssetDatabase.AddObjectToAsset(chunks[i].Mesh, MeshAssetPath);
        }

        AssetDatabase.AddObjectToAsset(handle, MeshAssetPath);

        AssetDatabase.ImportAsset(MeshAssetPath);
    }

    private static void BuildPrefab(List<ChunkData> data, Material[] materials, Material chipMaterial, Material sliverMaterial, Mesh handle)
    {
        var root = new GameObject("BreakableDoor_Wood");
        var blocker = root.AddComponent<BoxCollider>();
        blocker.center = new Vector3((DoorLeft + DoorRight) * 0.5f, (DoorBottom + DoorTop) * 0.5f, BattenThickness * 0.5f);
        blocker.size = new Vector3(DoorRight - DoorLeft, DoorTop - DoorBottom, PlankThickness + BattenThickness);

        var chunksRoot = new GameObject("Chunks").transform;
        chunksRoot.SetParent(root.transform, false);

        var chunks = new BreakableDoor.Chunk[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            var chunk = new GameObject(data[i].Mesh.name);
            chunk.transform.SetParent(chunksRoot, false);
            chunk.AddComponent<MeshFilter>().sharedMesh = data[i].Mesh;
            chunk.AddComponent<MeshRenderer>().sharedMaterials = materials;

            var box = chunk.AddComponent<BoxCollider>();
            box.center = data[i].Mesh.bounds.center;
            box.size = data[i].Mesh.bounds.size;

            var body = chunk.AddComponent<Rigidbody>();
            body.mass = data[i].Volume * ChunkMassPerCubicMeter;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            chunks[i] = new BreakableDoor.Chunk(body, box, data[i].Center);

            if (data[i].Mesh.name == HandleChunkName)
            {
                var handleObject = new GameObject("Handle");
                handleObject.transform.SetParent(chunk.transform, false);
                handleObject.AddComponent<MeshFilter>().sharedMesh = handle;
                handleObject.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HandleMaterialPath);
            }
        }

        ParticleSystem slivers = BuildBurst(root.transform, "Slivers", sliverMaterial, ParticleSystemRenderMode.Stretch,
            SliverLifetime, SliverSpeedMin, SliverSpeedMax, SliverSize, SliverSize, SliverGravity, SliverStretch, 0);
        ParticleSystem flash = BuildBurst(root.transform, "Flash", sliverMaterial, ParticleSystemRenderMode.Stretch,
            FlashLifetime, FlashSpeed, FlashSpeed, FlashSize, FlashSize, 0f, FlashStretch, FlashCount);
        ParticleSystem.ShapeModule flashShape = flash.shape;
        flashShape.shapeType = ParticleSystemShapeType.Sphere;
        ParticleSystem chips = BuildBurst(root.transform, "Chips", chipMaterial, ParticleSystemRenderMode.Mesh,
            ChipLifetime, ChipSpeedMin, ChipSpeedMax, ChipSizeMin, ChipSizeMax, ChipGravity, 0f, ChipCount);
        ParticleSystem.CollisionModule collision = chips.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.bounce = ChipBounce;

        var smoke = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SmokePrefabPath), root.transform);
        smoke.transform.localScale = Vector3.one * SmokeScale;
        smoke.GetComponent<CartoonFX.CFXR_Effect>().clearBehavior = CartoonFX.CFXR_Effect.ClearBehavior.None;
        var smokeParticles = smoke.GetComponent<ParticleSystem>();
        foreach (ParticleSystem part in smoke.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule partMain = part.main;
            partMain.playOnAwake = false;
            partMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        var door = root.AddComponent<BreakableDoor>();
        var serialized = new SerializedObject(door);
        SerializedProperty array = serialized.FindProperty("_chunks");
        array.arraySize = chunks.Length;
        for (int i = 0; i < chunks.Length; i++)
        {
            SerializedProperty element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("_body").objectReferenceValue = chunks[i].Body;
            element.FindPropertyRelative("_collider").objectReferenceValue = chunks[i].Collider;
            element.FindPropertyRelative("_center").vector2Value = chunks[i].Center;
        }

        serialized.FindProperty("_blocker").objectReferenceValue = blocker;
        serialized.FindProperty("_splinters").objectReferenceValue = slivers;
        SerializedProperty impacts = serialized.FindProperty("_impacts");
        ParticleSystem[] bursts = { flash, chips, smokeParticles };
        impacts.arraySize = bursts.Length;
        for (int i = 0; i < bursts.Length; i++)
        {
            impacts.GetArrayElementAtIndex(i).objectReferenceValue = bursts[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static ParticleSystem BuildBurst(Transform parent, string name, Material material, ParticleSystemRenderMode mode,
        float lifetime, float speedMin, float speedMax, float sizeMin, float sizeMax, float gravity, float stretch, short burstCount)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = lifetime;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation3D = mode == ParticleSystemRenderMode.Mesh;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = MaxParticles;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        if (burstCount > 0)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });
        }

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = ConeAngle;
        shape.radius = sizeMin;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = mode;
        renderer.lengthScale = stretch;
        renderer.sharedMaterial = material;
        if (mode == ParticleSystemRenderMode.Mesh)
        {
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }

        return particles;
    }
}
