using Gameplay.Entities.Interactive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ExtinguisherTestStage
{
    const string ScenePath = "Assets/Scenes/Test/ExtinguisherTest.unity";
    const string ExtPrefab = "Assets/Contents/Prefabs/Items/Extinguisher_Pickup.prefab";
    const string FirePrefab = "Assets/Contents/Prefabs/Fire/Fire_Source.prefab";
    static readonly Vector3 Origin = new(1000f, 0f, 1000f);

    static Scene StageScene()
    {
        var s = SceneManager.GetSceneByPath(ScenePath);
        if (s.IsValid() && s.isLoaded) return s;
        if (System.IO.File.Exists(ScenePath)) return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        return default;
    }

    static T Find<T>(string name) where T : Component
    {
        foreach (var root in StageScene().GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c.name == name) return c;
        return null;
    }

    static Material MaterialAt(Vector3 from, Vector3 dir)
    {
        return Physics.Raycast(from, dir, out var hit, 20f) && hit.collider.TryGetComponent<Renderer>(out var r) ? r.sharedMaterial : null;
    }

    public static string Build()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes/Test");
        var active = SceneManager.GetActiveScene();
        var floorMat = MaterialAt(new Vector3(2.4f, 1.5f, -4.5f), Vector3.down);
        var wallMat = MaterialAt(new Vector3(2.4f, 1.5f, -5.2f), Vector3.left);
        var mainCam = Camera.main;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var fpc = Object.FindAnyObjectByType<Gameplay.Entities.Player.FirstPersonController>();
        var hands = typeof(Gameplay.Entities.Player.FirstPersonController).GetField("_hands", bf).GetValue(fpc);
        var ht = hands.GetType();
        var leftFist = (Mesh)ht.GetField("_leftFist", bf).GetValue(hands);
        var rightFist = (Mesh)ht.GetField("_rightFist", bf).GetValue(hands);
        var armMat = ((MeshFilter)ht.GetField("_rightMesh", bf).GetValue(hands)).GetComponent<MeshRenderer>().sharedMaterials;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor"; floor.transform.position = Origin + new Vector3(0f, -0.05f, 2f); floor.transform.localScale = new Vector3(8f, 0.1f, 10f);
        if (floorMat) floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall"; wall.transform.position = Origin + new Vector3(0f, 1.5f, 5f); wall.transform.localScale = new Vector3(8f, 3f, 0.2f);
        if (wallMat) wall.GetComponent<Renderer>().sharedMaterial = wallMat;
        var side = Object.Instantiate(wall); side.name = "WallLeft";
        side.transform.position = Origin + new Vector3(-2.5f, 1.5f, 2f); side.transform.localScale = new Vector3(0.2f, 3f, 10f);

        var fire = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefab), scene);
        fire.transform.position = Origin + new Vector3(0f, 0f, 3f);

        var camGo = new GameObject("TestCamera");
        camGo.transform.position = Origin + new Vector3(0f, 1.7f, 0f);
        camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = mainCam.fieldOfView; cam.nearClipPlane = mainCam.nearClipPlane; cam.enabled = false;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        var srcData = mainCam.GetComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = srcData.renderPostProcessing;
        data.antialiasing = srcData.antialiasing;
        data.requiresDepthTexture = true;

        var handsGo = new GameObject("Hands");
        handsGo.transform.SetParent(camGo.transform, false);
        var armL = MakeArm(handsGo.transform, "Arm_L", leftFist, armMat);
        var armR = MakeArm(handsGo.transform, "Arm_R", rightFist, armMat);

        var ext = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ExtPrefab), scene);
        ext.transform.SetParent(armR, false);
        foreach (var rb in ext.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;

        EditorSceneManager.SaveScene(scene, ScenePath);
        SceneManager.SetActiveScene(active);
        return $"built floor:{floorMat?.name} wall:{wallMat?.name}";
    }

    static Transform MakeArm(Transform parent, string name, Mesh mesh, Material[] mats)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mats; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    public static string Pose(float armYaw, float armRoll, float fx, float fy, float fz, float yaw, float tilt, float lift, float gx, float gy, float gz)
    {
        ExtinguisherLab.ArmYaw = armYaw; ExtinguisherLab.ArmRoll = armRoll;
        var p = ExtinguisherLab.Solve(new Vector3(fx, fy, fz), yaw, tilt, lift, new Vector3(gx, gy, gz));
        var armR = Find<Transform>("Arm_R"); var armL = Find<Transform>("Arm_L");
        armR.localPosition = p.HandPosition; armR.localEulerAngles = p.HandEuler;
        var ext = armR.GetComponentInChildren<ExtinguisherItem>(true).transform;
        ext.localPosition = p.HoldPosition; ext.localEulerAngles = p.HoldEuler;
        var grip = ext.GetComponent<ToolItem>().LeftGrip;
        grip.localPosition = p.GripPosition;
        grip.localEulerAngles = p.GripEuler;

        Transform space = armL.parent;
        Vector3 g = space.InverseTransformPoint(grip.position);
        Vector3 across = space.InverseTransformDirection(grip.up);
        Vector3 forward = Vector3.ProjectOnPlane(space.InverseTransformDirection(grip.forward), across).normalized;
        Quaternion aim = Quaternion.LookRotation(forward, Vector3.Cross(forward, across));
        armL.localPosition = g - aim * ExtinguisherLab.FistCenter;
        armL.localRotation = aim;
        return "posed";
    }

    public static string Spray(float seconds, float impactDistance)
    {
        var ext = Find<Transform>("Arm_R").GetComponentInChildren<ExtinguisherItem>(true);
        var cam = Find<Camera>("TestCamera");
        var so = new SerializedObject(ext);
        var nozzle = (Transform)so.FindProperty("_nozzle").objectReferenceValue;
        Vector3 aimPoint = cam.transform.position + cam.transform.forward * impactDistance;
        nozzle.rotation = Quaternion.LookRotation(aimPoint - nozzle.position, ext.transform.up);
        var impact = (ParticleSystem)so.FindProperty("_impactCloud").objectReferenceValue;
        var floor = (ParticleSystem)so.FindProperty("_floorPowder").objectReferenceValue;
        impact.transform.position = aimPoint;
        floor.transform.position = new Vector3(aimPoint.x, Origin.y + 0.02f, aimPoint.z);
        foreach (var name in new[] { "_jet", "_grains", "_impactCloud", "_floorPowder" })
        {
            var ps = (ParticleSystem)so.FindProperty(name).objectReferenceValue;
            if (seconds <= 0f) { ps.Clear(true); continue; }
            ps.Simulate(seconds, false, true, true);
        }
        foreach (var f in Object.FindObjectsByType<Gameplay.Entities.Interactive.FireSource>(FindObjectsSortMode.None))
            if (f.gameObject.scene == StageScene())
                foreach (var ps in f.GetComponentsInChildren<ParticleSystem>())
                    if (ps.main.playOnAwake) ps.Simulate(2f, false, true, true);
        return "sprayed";
    }

    public static string Render(string file, int width = 960, int height = 540)
    {
        var cam = Find<Camera>("TestCamera");
        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        return file;
    }
}
