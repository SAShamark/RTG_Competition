using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public static class HandTwist
    {
        public static Vector3 Reference(Quaternion hand, Vector3 axis)
        {
            Vector3 reference = Vector3.ProjectOnPlane(hand * Vector3.up, axis);
            return reference.sqrMagnitude > Vector3.kEpsilon ? reference : Vector3.ProjectOnPlane(hand * Vector3.forward, axis);
        }

        public static Vector3 WorldAxis(Transform turned, Quaternion rest, Vector3 localAxis) => turned.parent.rotation * rest * localAxis;
    }
}
