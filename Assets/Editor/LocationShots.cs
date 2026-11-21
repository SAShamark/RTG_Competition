using System.IO;
using UnityEditor;
using UnityEngine;

// Editor helper: renders preview shots of the current scene from given viewpoints.
public static class LocationShots
{
    public static string Shot(string name, Vector3 pos, Vector3 lookAt, float fov = 55f, int w = 1280, int h = 720)
    {
        var go = new GameObject("__ShotCam");
        try
        {
            var cam = go.AddComponent<Camera>();
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "Shots");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            return path;
        }
        finally { Object.DestroyImmediate(go); }
    }
}
