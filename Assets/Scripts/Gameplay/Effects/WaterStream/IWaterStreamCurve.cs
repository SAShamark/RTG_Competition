using UnityEngine;

namespace Gameplay.Effects.WaterStream
{
    public interface IWaterStreamCurve
    {
        float Length { get; }
        bool HasImpact { get; }
        Vector3 ImpactPoint { get; }
        Vector3 ImpactNormal { get; }

        void Sample(float distance, out Vector3 point, out Vector3 velocity);
    }
}
