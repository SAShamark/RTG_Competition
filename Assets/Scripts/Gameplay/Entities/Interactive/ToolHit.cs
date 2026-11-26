using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public readonly struct ToolHit
    {
        public readonly ToolHitKind Kind;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly float Impulse;

        public ToolHit(ToolHitKind kind, Vector3 point, Vector3 direction, float impulse)
        {
            Kind = kind;
            Point = point;
            Direction = direction;
            Impulse = impulse;
        }
    }
}
