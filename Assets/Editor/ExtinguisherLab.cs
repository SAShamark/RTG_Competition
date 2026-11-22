using System.Collections.Generic;
using Gameplay.Entities.Interactive.Items;
using UnityEditor;
using UnityEngine;

public static class ExtinguisherLab
{
    public const string Shader = "FlameBlame/PowderPuff";
    public const string CloudMat = "Assets/Contents/VFX/Smoke/M_Powder_Cloud.mat";
    public const string JetMat = "Assets/Contents/VFX/Smoke/M_Powder_Jet.mat";
    const string Noise = "Assets/Contents/VFX/Smoke/T_PowderNoise.png";
    const string Prefab = "Assets/Contents/Prefabs/Items/Extinguisher_Pickup.prefab";

    static readonly List<ParticleSystemVertexStream> Streams = new()
    {
        ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
        ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX
    };

    static Material Mat(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(UnityEngine.Shader.Find(Shader));
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    public static string Materials()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(Noise);
        ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Repeat; ti.SaveAndReimport();
        var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(Noise);

        var cloud = Mat(CloudMat);
        cloud.SetTexture("_NoiseTex", noise);
        cloud.SetFloat("_EdgeNoise", 0.75f);
        cloud.SetFloat("_NoiseScale", 1.1f);
        cloud.SetFloat("_Softness", 0.45f);
        cloud.SetFloat("_Density", 1.7f);
        cloud.SetColor("_ShadowColor", new Color(0.76f, 0.74f, 0.82f));
        EditorUtility.SetDirty(cloud);

        var jet = Mat(JetMat);
        jet.SetTexture("_NoiseTex", noise);
        jet.SetFloat("_Lit", 0.25f);
        jet.SetFloat("_Density", 2.5f);
        jet.SetFloat("_EdgeNoise", 0.2f);
        jet.SetFloat("_Softness", 0.35f);
        jet.SetFloat("_Erode", 0.3f);
        jet.SetFloat("_NearFade", 0.1f);
        jet.SetFloat("_NearFadeRange", 0.3f);
        jet.SetFloat("_SoftDistance", 0.15f);
        EditorUtility.SetDirty(jet);
        AssetDatabase.SaveAssets();
        return "ok";
    }

    public static void Use(ParticleSystem ps, Material m)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.SetActiveVertexStreams(Streams);
    }

    public static Vector3 HandleGrip = new(-0.1f, 0.5f, 0f);
    public static readonly Vector3 FistCenter = new(0f, -0.045f, 0.345f);

    public struct Pose
    {
        public Vector3 HandPosition, HandEuler, HoldPosition, HoldEuler, GripPosition, GripEuler;
    }

    // Everything is solved in the Hands space, which matches the camera.
    public static float ItemScale = 0.75f;
    public static float ArmYaw = -45f;
    public static float ArmRoll = 0f;
    public static Vector3 LeftReach = new(0.3f, 0.75f, 0.6f);

    public static Pose Solve(Vector3 fist, float yaw, float tilt, float forearmLift, Vector3 gripPoint)
    {
        Vector3 d = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
        Vector3 u = Quaternion.AngleAxis(-tilt, Vector3.forward) * Vector3.up;
        Vector3 modelZ = Vector3.Cross(-d, u);
        Quaternion item = Quaternion.LookRotation(modelZ, u);

        Vector3 f = (Quaternion.AngleAxis(ArmYaw, Vector3.up) * Vector3.forward + Vector3.up * forearmLift).normalized;
        Quaternion hand = Quaternion.LookRotation(f, Quaternion.AngleAxis(ArmRoll, f) * Vector3.up);
        Vector3 handPos = fist - hand * FistCenter;

        Vector3 itemPos = fist - item * (HandleGrip * ItemScale);
        Quaternion inv = Quaternion.Inverse(hand);
        return new Pose
        {
            HandPosition = handPos,
            HandEuler = hand.eulerAngles,
            HoldPosition = inv * (itemPos - handPos),
            HoldEuler = (inv * item).eulerAngles,
            GripPosition = gripPoint,
            GripEuler = (Quaternion.Inverse(item) * Quaternion.LookRotation(LeftReach.normalized, u)).eulerAngles,
        };
    }

    public static string ApplyLive(float armYaw, float armRoll, float fx, float fy, float fz, float yaw, float tilt, float lift, float gx, float gy, float gz)
    {
        ArmYaw = armYaw; ArmRoll = armRoll;
        var p = Solve(new Vector3(fx, fy, fz), yaw, tilt, lift, new Vector3(gx, gy, gz));
        var fpc = Object.FindAnyObjectByType<Gameplay.Entities.Player.FirstPersonController>();
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hands = (Gameplay.Entities.Player.HandsAnimationControl)typeof(Gameplay.Entities.Player.FirstPersonController).GetField("_hands", bf).GetValue(fpc);
        hands.SetHandPose(true, p.HandPosition, p.HandEuler);
        ExtinguisherItem item = null;
        foreach (var candidate in Object.FindObjectsByType<ExtinguisherItem>(FindObjectsInactive.Include))
            if (candidate.GetComponentInParent<Gameplay.Entities.Player.FirstPersonController>() != null) item = candidate;
        item.transform.localPosition = p.HoldPosition;
        item.transform.localEulerAngles = p.HoldEuler;
        var tool = item.GetComponent<ToolItem>();
        tool.LeftGrip.localPosition = p.GripPosition;
        tool.LeftGrip.localEulerAngles = p.GripEuler;
        hands.SetLeftGrip(tool.LeftGrip);
        Write(new SerializedObject(tool), p);
        return $"hand {p.HandPosition:F3} {p.HandEuler:F1} hold {p.HoldPosition:F3} {p.HoldEuler:F1}";
    }

    static void Write(SerializedObject so, Pose p)
    {
        so.FindProperty("_hasHandPose").boolValue = true;
        so.FindProperty("_handPosition").vector3Value = p.HandPosition;
        so.FindProperty("_handEuler").vector3Value = p.HandEuler;
        so.FindProperty("_holdPosition").vector3Value = p.HoldPosition;
        so.FindProperty("_holdEuler").vector3Value = p.HoldEuler;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static string SavePose(float armYaw, float armRoll, float fx, float fy, float fz, float yaw, float tilt, float lift, float gx, float gy, float gz)
    {
        ArmYaw = armYaw; ArmRoll = armRoll;
        var p = Solve(new Vector3(fx, fy, fz), yaw, tilt, lift, new Vector3(gx, gy, gz));
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        var tool = root.GetComponent<ToolItem>();
        Write(new SerializedObject(tool), p);
        tool.LeftGrip.localPosition = p.GripPosition;
        tool.LeftGrip.localEulerAngles = p.GripEuler;
        PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        PrefabUtility.UnloadPrefabContents(root);
        return "saved";
    }

    public static string Capture(string file, int width = 960, int height = 540)
    {
        var cam = Camera.main;
        var rt = new RenderTexture(width, height, 24);
        var old = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = old;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        return file;
    }

    static Gradient Fade(float peak, float peakAt)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, peakAt), new GradientAlphaKey(peak * 0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    public static string ApplyFx()
    {
        Materials();
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        var so = new SerializedObject(root.GetComponent<ExtinguisherItem>());
        var jet = (ParticleSystem)so.FindProperty("_jet").objectReferenceValue;
        var cloud = (ParticleSystem)so.FindProperty("_impactCloud").objectReferenceValue;
        Use(jet, Mat(JetMat));
        Use(cloud, Mat(CloudMat));
        var jr = jet.GetComponent<ParticleSystemRenderer>(); jr.renderMode = ParticleSystemRenderMode.Billboard;
        var jm = jet.main; jm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.09f); jm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.35f); jm.maxParticles = 1000;
        var je = jet.emission; je.rateOverTime = 800f;
        var js = jet.sizeOverLifetime; js.enabled = true; js.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 5f));
        var jc = jet.colorOverLifetime; jc.enabled = true; jc.color = Fade(0.9f, 0.08f);
        var cs = cloud.sizeOverLifetime; cs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 2.6f));
        var cm2 = cloud.main; cm2.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.36f); cm2.maxParticles = 400;
        var ce = cloud.emission; ce.rateOverTime = 75f;
        var csh = cloud.shape; csh.shapeType = ParticleSystemShapeType.Hemisphere; csh.radius = 0.25f; csh.rotation = new Vector3(-90f, 0f, 0f);
        cm2.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        var cc = cloud.colorOverLifetime; cc.color = Fade(0.8f, 0.2f);
        var ts = cloud.textureSheetAnimation; ts.enabled = false;
        var cl = cloud.main; cl.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.8f);

        var grains = (ParticleSystem)so.FindProperty("_grains").objectReferenceValue;
        Use(grains, Mat(JetMat));
        var gm = grains.main; gm.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f); gm.startSpeed = new ParticleSystem.MinMaxCurve(7f, 12f);
        gm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f); gm.gravityModifier = 0.5f;
        var ge = grains.emission; ge.rateOverTime = 600f;
        var gs = grains.shape; gs.angle = 9f;
        var gc = grains.colorOverLifetime; gc.color = Fade(1f, 0.05f);

        var floorPs = (ParticleSystem)so.FindProperty("_floorPowder").objectReferenceValue;
        var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Contents/VFX/Smoke/M_PowderFloor.mat");
        var floorTex = "Assets/Contents/VFX/Smoke/T_PowderFloor.png";
        var fti = (TextureImporter)AssetImporter.GetAtPath(floorTex); fti.alphaIsTransparency = true; fti.wrapMode = TextureWrapMode.Clamp; fti.SaveAndReimport();
        floorMat.shader = UnityEngine.Shader.Find("Universal Render Pipeline/Particles/Unlit");
        floorMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(floorTex));
        floorMat.SetFloat("_Surface", 1f); floorMat.SetFloat("_Blend", 0f);
        floorMat.SetFloat("_SoftParticlesEnabled", 0f); floorMat.SetFloat("_CameraFadingEnabled", 0f);
        floorMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        floorMat.SetOverrideTag("RenderType", "Transparent");
        floorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        floorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        floorMat.SetInt("_ZWrite", 0);
        floorMat.renderQueue = 3000;
        EditorUtility.SetDirty(floorMat);
        var fm = floorPs.main; fm.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f); fm.startColor = new Color(1f, 1f, 1f, 0.9f);
        var fe = floorPs.emission; fe.rateOverTime = 10f;
        var fsh = floorPs.shape; fsh.radius = 0.5f;
        var main = cloud.main; main.startRotation = 0f;
        var rot = cloud.rotationOverLifetime; rot.enabled = false;
        PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        PrefabUtility.UnloadPrefabContents(root);
        return "fx";
    }
}
