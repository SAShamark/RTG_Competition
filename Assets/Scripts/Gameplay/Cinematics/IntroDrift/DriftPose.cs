using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    public readonly struct DriftPose
    {
        public readonly Vector3 Position;
        public readonly Vector3 Heading;
        public readonly Quaternion Rotation;
        public readonly float Slip;

        public DriftPose(Vector3 position, Vector3 heading, Quaternion rotation, float slip)
        {
            Position = position;
            Heading = heading;
            Rotation = rotation;
            Slip = slip;
        }
    }
}
