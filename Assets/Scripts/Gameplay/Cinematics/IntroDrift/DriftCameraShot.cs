using System;
using Services;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftCameraShot
    {
        [SerializeField] private CinemachineCamera _camera;
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private Transform _corner;
        [SerializeField] private int _priority = 50;

        [Header("Opening hero shot → tracking")]
        [SerializeField] private DriftCameraRig _opening = new(4.2f, 2.2f, 0.35f, 1.9f, 70f, 8f);
        [SerializeField] private DriftCameraRig _tracking = new(11f, 2.9f, 1.5f, 1.3f, 42f, 1.5f);
        [SerializeField, Min(0.01f)] private float _openingDistance = 30f;

        [Header("Drift framing")]
        [SerializeField] private float _driftFov = 36f;
        [SerializeField] private float _driftAimYaw = 9f;
        [SerializeField] private float _driftRoll = -3f;
        [SerializeField] private float _driftLookHeight = 1.3f;

        [Header("Swing to corner")]
        [SerializeField] private float _swingFrom = -8f;
        [SerializeField] private float _swingTo = 6f;

        [Header("Shake")]
        [SerializeField] private float _driftKickForce = 0.8f;

        public void Begin()
        {
            _camera.Priority.Value = _priority;
            _camera.gameObject.SetActive(true);
        }

        public void End() => _camera.gameObject.SetActive(false);

        public void Kick() => _impulse.GenerateImpulseWithForce(_driftKickForce);

        public void Tick(DriftPose pose, float fromTurnIn, float distance)
        {
            DriftCameraRig rig = DriftCameraRig.Lerp(_opening, _tracking, Easing.Smooth(distance / _openingDistance));
            float swing = Easing.Smooth(Mathf.InverseLerp(_swingFrom, _swingTo, fromTurnIn));
            Vector3 right = Vector3.Cross(Vector3.up, pose.Heading);
            Vector3 tracking = pose.Position + pose.Heading * rig.Lead + right * rig.Side + Vector3.up * rig.Height;
            Vector3 position = Vector3.Lerp(tracking, _corner.position, swing);
            Vector3 look = pose.Position + Vector3.up * Mathf.Lerp(rig.LookHeight, _driftLookHeight, swing);
            Quaternion framing = Quaternion.Euler(0f, _driftAimYaw * swing, Mathf.Lerp(rig.Roll, _driftRoll, swing));
            _camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position) * framing);
            _camera.Lens.FieldOfView = Mathf.Lerp(rig.Fov, _driftFov, swing);
        }
    }
}
