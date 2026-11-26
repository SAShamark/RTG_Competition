using System;
using System.Collections.Generic;
using UnityEngine;

namespace Services
{
    [Serializable]
    public sealed class ShaderWarmup : IDisposable
    {
        private const string InternalShaderPrefix = "Hidden/";

        [SerializeField, Min(1)] private int _materialsPerFrame = 1;
        [SerializeField, Min(0.1f)] private float _distance = 1f;
        [SerializeField, Min(0.0001f)] private float _size = 0.001f;

        private readonly List<Material> _queue = new();
        private Mesh _mesh;
        private int _next;

        public bool IsDone => _next >= _queue.Count;

        public void Initialize()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                return;
            }

            _mesh = BuildQuad();
            var seen = new HashSet<string>();
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
            {
                Shader shader = material.shader;
                if (shader == null || !shader.isSupported || shader.name.StartsWith(InternalShaderPrefix))
                {
                    continue;
                }

                string variant = shader.name + "|" + string.Join(",", material.shaderKeywords) + "|" + material.renderQueue;
                if (seen.Add(variant))
                {
                    _queue.Add(material);
                }
            }
        }

        public void Tick()
        {
            Camera camera = Camera.main;
            if (IsDone || camera == null)
            {
                return;
            }

            Transform view = camera.transform;
            Matrix4x4 matrix = Matrix4x4.TRS(view.position + view.forward * _distance, view.rotation, Vector3.one * _size);
            for (int i = 0; i < _materialsPerFrame && !IsDone; i++)
            {
                Material material = _queue[_next++];
                if (material != null)
                {
                    Graphics.DrawMesh(_mesh, matrix, material, 0, camera, 0, null, true, true);
                }
            }
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                UnityEngine.Object.Destroy(_mesh);
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "ShaderWarmupQuad" };
            mesh.SetVertices(new[] { new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f) });
            mesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetTangents(new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) });
            mesh.SetUVs(0, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return mesh;
        }
    }
}
