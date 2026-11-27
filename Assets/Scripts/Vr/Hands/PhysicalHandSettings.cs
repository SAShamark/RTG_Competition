using System;
using UnityEngine;

namespace Vr.Hands
{
    [Serializable]
    public struct PhysicalHandSettings
    {
        [SerializeField, Range(0.1f, 1f)] private float _followStrength;
        [SerializeField, Min(0.05f)] private float _snapDistance;
        [SerializeField, Min(0.003f)] private float _fingerRadius;
        [SerializeField, Min(0f)] private float _releaseGrace;
        [SerializeField, Min(1f)] private float _curlSharpness;
        [SerializeField, Min(1f)] private float _velocitySharpness;

        public float FollowStrength => _followStrength;
        public float SnapDistance => _snapDistance;
        public float FingerRadius => _fingerRadius;
        public float ReleaseGrace => _releaseGrace;
        public float CurlSharpness => _curlSharpness;
        public float VelocitySharpness => _velocitySharpness;
    }
}
