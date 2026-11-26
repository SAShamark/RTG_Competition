using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    [Serializable]
    public sealed class ToolResponse
    {
        [SerializeField, Min(0f)] private float _powderFactor = 1f;
        [SerializeField, Min(0f)] private float _waterFactor = 1f;
        [SerializeField, Min(0f)] private float _strikeFactor = 1f;
        [SerializeField, Min(0f)] private float _maxKickSpeed = 6f;

        public void Apply(Rigidbody body, in ToolHit hit)
        {
            bool isStrike = hit.Kind is ToolHitKind.Axe or ToolHitKind.Thrown;
            if (body.isKinematic)
            {
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            else if (hit.Kind == ToolHitKind.Thrown)
            {
                return;
            }

            float impulse = hit.Impulse * Factor(hit.Kind);
            if (isStrike)
            {
                impulse = Mathf.Min(impulse, body.mass * _maxKickSpeed);
            }

            body.AddForceAtPosition(hit.Direction * impulse, hit.Point, ForceMode.Impulse);
        }

        private float Factor(ToolHitKind kind) => kind switch
        {
            ToolHitKind.Powder => _powderFactor,
            ToolHitKind.Water => _waterFactor,
            _ => _strikeFactor,
        };
    }
}
