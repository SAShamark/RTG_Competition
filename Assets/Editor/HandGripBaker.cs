using UnityEditor;
using UnityEngine;

public static class HandGripBaker
{
    private const string Folder = "Assets/Contents/Characters/";
    private const float FingerBase = 0.345f;
    private const float FingerAxisY = -0.017f;
    private const float GripRadius = 0.028f;
    private const float PalmHalfWidth = 0.066f;
    private const float ThumbStartZ = 0.31f;
    private const float ThumbBaseEndZ = 0.37f;
    private const float ThumbBaseSide = 0.066f;
    private const float ThumbBaseY = -0.032f;
    private const float ThumbSide = 0.068f;
    private const float ThumbY = -0.01f;
    private const float ThumbPivotY = -0.04f;
    private const float ThumbCurl = 30f;
    private const float ThumbWrap = 80f;
    private const float ThumbReachLength = 0.07f;
    private static readonly Vector3 ThumbReach = new(0f, 0.012f, 0.05f);
    private const float BlendLength = 0.012f;

    [MenuItem("Tools/Hands/Bake Grip Meshes")]
    public static void Bake()
    {
        BakeHand("FP_Arm_R", -1f);
        BakeHand("FP_Arm_L", 1f);
        AssetDatabase.SaveAssets();
    }

    private static void BakeHand(string name, float thumbSide)
    {
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + name + ".asset");
        Mesh fist = Object.Instantiate(source);
        fist.name = name + "_Grip";

        Vector3[] vertices = fist.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = IsThumb(vertices[i], thumbSide) ? CurlThumb(vertices[i], thumbSide) : CurlFinger(vertices[i]);
        }

        fist.vertices = vertices;
        fist.RecalculateNormals();
        fist.RecalculateBounds();

        string path = Folder + fist.name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(fist, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(fist, path);
        }
    }

    private static bool IsThumb(Vector3 v, float thumbSide)
    {
        float side = v.x * thumbSide;
        bool isBase = v.z < ThumbBaseEndZ && v.y < ThumbBaseY && side > ThumbBaseSide;
        bool isTip = v.y < ThumbY && side > ThumbSide;
        return v.z > ThumbStartZ && (isBase || isTip);
    }

    // Wraps the finger along an arc of radius GripRadius: s is the length along the finger, h the offset from its axis.
    private static Vector3 CurlFinger(Vector3 v)
    {
        float s = v.z - FingerBase;
        if (s <= 0f)
        {
            return v;
        }

        float weight = Mathf.Clamp01(s / BlendLength);
        float phi = s / GripRadius;
        float h = v.y - FingerAxisY;
        float z = FingerBase + GripRadius * Mathf.Sin(phi) + h * Mathf.Sin(phi);
        float y = FingerAxisY - GripRadius * (1f - Mathf.Cos(phi)) + h * Mathf.Cos(phi);
        return Vector3.Lerp(v, new Vector3(v.x, y, z), weight);
    }

    private static Vector3 CurlThumb(Vector3 v, float thumbSide)
    {
        Vector3 pivot = new(PalmHalfWidth * thumbSide, ThumbPivotY, v.z);
        float weight = Mathf.Clamp01((v.z - ThumbStartZ) / BlendLength);
        Quaternion curl = Quaternion.AngleAxis(-ThumbCurl * thumbSide * weight, Vector3.forward);
        Vector3 curled = pivot + curl * (v - pivot);
        Vector3 wrapPivot = new(PalmHalfWidth * thumbSide, curled.y, ThumbStartZ);
        Quaternion wrap = Quaternion.AngleAxis(-ThumbWrap * thumbSide * weight, Vector3.up);
        float reach = Mathf.Clamp01((v.z - ThumbStartZ) / ThumbReachLength);
        return wrapPivot + wrap * (curled - wrapPivot) + ThumbReach * reach;
    }
}
