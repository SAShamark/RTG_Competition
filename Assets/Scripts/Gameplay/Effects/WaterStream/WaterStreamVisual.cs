using UnityEngine;

namespace Gameplay.Effects.WaterStream
{
    public abstract class WaterStreamVisual : MonoBehaviour
    {
        private const float MinLength = 0.05f;

        [SerializeField] private WaterImpactFx _impact = new();

        private void Awake()
        {
            _impact.Init();
            OnInit();
        }

        private void OnDestroy() => OnDispose();

        public void Show(IWaterStreamCurve curve, float cut, float reach, bool isLanded, float deltaTime)
        {
            if (reach - cut < MinLength)
            {
                Hide();
                return;
            }

            OnShow(curve, cut, reach, isLanded, deltaTime);
            bool hasImpact = ResolveImpact(curve, isLanded, out Vector3 point, out Vector3 normal);
            _impact.Tick(point, normal, hasImpact, deltaTime);
        }

        public void Hide()
        {
            OnHide();
            _impact.Hide();
        }

        public void Steam(Vector3 point, float deltaTime) => _impact.EmitSteam(point, deltaTime);

        protected virtual void OnInit()
        {
        }

        protected virtual void OnDispose()
        {
        }

        protected virtual bool ResolveImpact(IWaterStreamCurve curve, bool isLanded, out Vector3 point, out Vector3 normal)
        {
            point = curve.ImpactPoint;
            normal = curve.ImpactNormal;
            return isLanded && curve.HasImpact;
        }

        protected abstract void OnShow(IWaterStreamCurve curve, float cut, float reach, bool isLanded, float deltaTime);

        protected abstract void OnHide();
    }
}
