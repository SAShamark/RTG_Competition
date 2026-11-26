using Gameplay.Entities.Environment;
using Gameplay.Entities.Interactive;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Reversible draw-call optimisation for the Location scene.
// Combine: static environment renderers are merged per material inside 12 m grid cells (per top-level group).
//          The originals stay in the scene with their colliders; only their MeshRenderer is switched off.
// Uncombine: deletes the combined meshes and switches the original renderers back on (for hand editing).
// Build: the merged originals that are nothing but a switched-off renderer are left out of the built scene, so it loads faster.
public static class SceneOptimizer
{
    const string RootName = "--- Combined (generated) ---";
    const string OriginalsFile = "Assets/Contents/Generated/CombinedOriginals.txt";
    const string MeshDir = "Assets/Contents/Generated/Combined";
    const float Cell = 12f;
    const int BareComponentCount = 3;

    static readonly string[] ExcludedRoots =
    {
        "Player", "CutsceneTruck", "Items", "Scene02_UncleBurst", "PlayBounds", "Cutscene_Intro", "CutsceneUI", "UIManager",
        "EntryPoint", "EventSystem", "Main Camera",
    };

    [MenuItem("Flame&Blame/Optimize/Combine Static Meshes")]
    public static string Combine()
    {
        Uncombine();
        System.IO.Directory.CreateDirectory(MeshDir);
        foreach (var old in AssetDatabase.FindAssets("t:Mesh", new[] { MeshDir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));

        var buckets = new Dictionary<(string, Vector2Int, Material, ShadowCastingMode), List<MeshRenderer>>();
        int used = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (!Eligible(r)) continue;
            var c = r.bounds.center;
            var cell = new Vector2Int(Mathf.FloorToInt(c.x / Cell), Mathf.FloorToInt(c.z / Cell));
            var mode = r.bounds.size.magnitude < 0.6f ? ShadowCastingMode.Off : r.shadowCastingMode;
            var key = (TopGroup(r.transform), cell, r.sharedMaterial, mode);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<MeshRenderer>();
            list.Add(r);
            used++;
        }

        var root = new GameObject(RootName);
        root.isStatic = true;
        int meshes = 0;
        var disabled = new List<MeshRenderer>();
        foreach (var kv in buckets)
        {
            if (kv.Value.Count < 2) continue;   // nothing to gain
            var combine = new List<CombineInstance>();
            foreach (var r in kv.Value)
            {
                var mf = r.GetComponent<MeshFilter>();
                for (int s = 0; s < mf.sharedMesh.subMeshCount && s < 1; s++)
                    combine.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = r.localToWorldMatrix });
            }

            var mesh = new Mesh { name = $"{kv.Key.Item1}_{kv.Key.Item2.x}_{kv.Key.Item2.y}_{kv.Key.Item3.name}_{meshes}", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(combine.ToArray(), true, true, false);
            mesh.RecalculateBounds();
            mesh.Optimize();
            AssetDatabase.CreateAsset(mesh, $"{MeshDir}/C_{meshes}.asset");

            var go = new GameObject(mesh.name);
            go.transform.SetParent(root.transform, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = kv.Key.Item3;
            mr.shadowCastingMode = kv.Key.Item4;
            foreach (var r in kv.Value)
            {
                Undo.RecordObject(r, "combine");
                r.enabled = false;
                disabled.Add(r);
            }
            meshes++;
        }

        System.IO.File.WriteAllLines(OriginalsFile, disabled.ConvertAll(r => GlobalObjectId.GetGlobalObjectIdSlow(r).ToString()));
        AssetDatabase.ImportAsset(OriginalsFile);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        return $"merged {used} renderers into {meshes} meshes";
    }

    [MenuItem("Flame&Blame/Optimize/Uncombine (for editing)")]
    public static void Uncombine()
    {
        var root = GameObject.Find(RootName);
        if (root == null) return;
        if (System.IO.File.Exists(OriginalsFile))
        {
            foreach (string line in System.IO.File.ReadAllLines(OriginalsFile))
            {
                if (GlobalObjectId.TryParse(line, out GlobalObjectId id) && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is MeshRenderer r)
                {
                    Undo.RecordObject(r, "uncombine");
                    r.enabled = true;
                }
            }
        }
        Object.DestroyImmediate(root);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    static bool Eligible(MeshRenderer r)
    {
        if (!r.enabled) return false;
        if (!r.gameObject.isStatic) return false;
        if (r.GetComponent<TextMesh>() != null) return false;
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;
        if (r.sharedMaterials.Length != 1 || r.sharedMaterial == null) return false;
        if (r.GetComponentInParent<Door>() != null) return false;
        if (r.GetComponentInParent<ChainLock>() != null) return false;
        // invisible blockers keep their renderer disabled on purpose: never touch them
        var t = r.transform;
        while (t != null)
        {
            if (t.name == RootName) return false;
            foreach (var ex in ExcludedRoots) if (t.name == ex) return false;
            t = t.parent;
        }
        return !IsInvisibleBlocker(r);
    }

    static bool IsInvisibleBlocker(MeshRenderer r)
    {
        string n = r.name;
        return n == "GateBlock" || n == "RailingBlock" || n == "Block" || n.StartsWith("PlayWall");
    }

    static string TopGroup(Transform t)
    {
        // second-level object under a folder ("--- Environment ---/OppositeSide"), or the root itself
        while (t.parent != null && t.parent.parent != null) t = t.parent;
        return t.name;
    }

    // Bare originals of a combined scene: static leaf objects holding only Transform + MeshFilter + a disabled MeshRenderer,
    // which no script points at. Everything with a collider, a script or children stays.
    public static List<GameObject> FindBareOriginals(Scene scene)
    {
        var result = new List<GameObject>();
        var roots = scene.GetRootGameObjects();
        if (!System.Array.Exists(roots, root => root.name == RootName)) return result;

        var referenced = new HashSet<Object>();
        foreach (var root in roots)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var property = new SerializedObject(behaviour).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = property.objectReferenceValue;
                    if (target is GameObject go) referenced.Add(go);
                    else if (target is Component component) referenced.Add(component.gameObject);
                }
            }
        }

        foreach (var root in roots)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.enabled || !r.gameObject.isStatic || r.transform.childCount > 0) continue;
                if (r.GetComponents<Component>().Length != BareComponentCount) continue;
                if (referenced.Contains(r.gameObject)) continue;
                result.Add(r.gameObject);
            }
        }

        return result;
    }

    sealed class BuildStripper : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // no report = the scene is being prepared for Play Mode in the editor, where deleting would only cost time
            if (report == null) return;

            var originals = FindBareOriginals(scene);
            foreach (var original in originals) Object.DestroyImmediate(original);
            if (originals.Count > 0) Debug.Log($"SceneOptimizer: left {originals.Count} combined originals out of '{scene.name}'");
        }
    }
}
