using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics.UncleBurst
{
    [Serializable]
    public sealed class UncleBurstExit
    {
        private const float SeenBoundsPadding = 0.5f;
        private const int FrustumPlaneCount = 6;

        [SerializeField] private float _unseenLimit = 0.4f;

        private readonly Plane[] _frustum = new Plane[FrustumPlaneCount];
        private Camera _camera;
        private GameObject _actors;
        private Renderer[] _renderers;
        private float _unseenTime;

        public void Init(Camera camera, GameObject actors)
        {
            _camera = camera;
            _actors = actors;
        }

        public void Begin()
        {
            _renderers = _actors.GetComponentsInChildren<Renderer>();
            _unseenTime = 0f;
        }

        public bool TickIsGone(float deltaTime)
        {
            _unseenTime = IsSeen() ? 0f : _unseenTime + deltaTime;
            return _unseenTime > _unseenLimit;
        }

        private bool IsSeen()
        {
            if (_camera.TryGetComponent(out CinemachineBrain brain) && brain.IsBlending)
            {
                return true;
            }

            GeometryUtility.CalculateFrustumPlanes(_camera, _frustum);
            foreach (Renderer actorRenderer in _renderers)
            {
                Bounds bounds = actorRenderer.bounds;
                bounds.Expand(SeenBoundsPadding);
                if (GeometryUtility.TestPlanesAABB(_frustum, bounds))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
