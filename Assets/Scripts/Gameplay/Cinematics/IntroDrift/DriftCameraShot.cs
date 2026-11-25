using System;
using Services;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftCameraShot
    {
        private const int BaseShot = -1;
        private const int NotStarted = -2;

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

        [Header("Cuts, in metres from the turn-in; outside them the shot above plays")]
        [SerializeField] private DriftCut[] _cuts =
        {
            new("Long lens", -53f, -30f, -8f, new(0f, 1.6f, 0.8f, 1.6f, 17f, 0f), new(0f, 1.6f, 0.8f, 1.6f, 18f, 0f)),
            new("Low front", -30f, -12f, new(9f, 1.5f, 0.6f, 1.3f, 52f, 6f, 1f), new(9f, 1.5f, 0.6f, 1.3f, 48f, 6f, 1f)),
            new("Slow motion close", 7f, 13f, new(10f, 0.5f, 0.4f, 1.3f, 36f, -5f), new(10f, 0.5f, 0.4f, 1.3f, 33f, -5f)),
            new("Out of the smoke", 13f, 60f, 50f, new(0f, -2f, 0.5f, 1.5f, 20f, -2f), new(0f, -2f, 0.5f, 1.5f, 24f, -2f)),
        };

        [Header("Shake")]
        [SerializeField] private float _driftKickForce = 0.8f;
        private int _activeCut = NotStarted;

        public bool HasCut { get; private set; }

        public void Begin()
        {
            _activeCut = NotStarted;
            HasCut = false;
            _camera.Priority.Value = _priority;
            _camera.gameObject.SetActive(true);
        }

        public void End() => _camera.gameObject.SetActive(false);

        public void Kick() => _impulse.GenerateImpulseWithForce(_driftKickForce);
        public void Tick(DriftPath path, DriftPose pose, float distance)
        {
            float fromTurnIn = distance - path.TurnIn;
            int active = Array.FindIndex(_cuts, cut => cut.Contains(fromTurnIn));
            HasCut = active != _activeCut;
            _activeCut = active;
            if (active != BaseShot)
            {
                Frame(_cuts[active], path, pose, fromTurnIn);
                return;
            }

            DriftCameraRig rig = DriftCameraRig.Lerp(_opening, _tracking, Easing.Smooth(distance / _openingDistance));
            float swing = Easing.Smooth(Mathf.InverseLerp(_swingFrom, _swingTo, fromTurnIn));
            Vector3 position = Vector3.Lerp(Offset(pose, rig.Lead, rig.Side, rig.Height), _corner.position, swing);
            Vector3 look = Offset(pose, rig.LookLead, 0f, Mathf.Lerp(rig.LookHeight, _driftLookHeight, swing));
            Quaternion framing = Quaternion.Euler(0f, _driftAimYaw * swing, Mathf.Lerp(rig.Roll, _driftRoll, swing));
            _camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position) * framing);
            _camera.Lens.FieldOfView = Mathf.Lerp(rig.Fov, _driftFov, swing);
        }

        private void Frame(DriftCut cut, DriftPath path, DriftPose pose, float fromTurnIn)
        {
            DriftCameraRig rig = cut.RigAt(fromTurnIn);
            DriftPose origin = cut.IsAnchored ? path.Sample(path.TurnIn + cut.Anchor) : pose;
            Vector3 position = Offset(origin, rig.Lead, rig.Side, rig.Height);
            Vector3 look = Offset(pose, rig.LookLead, 0f, rig.LookHeight);
            _camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position) * Quaternion.Euler(0f, 0f, rig.Roll));
            _camera.Lens.FieldOfView = rig.Fov;
        }

        private static Vector3 Offset(DriftPose pose, float lead, float side, float height)
        {
            Vector3 right = Vector3.Cross(Vector3.up, pose.Heading);
            return pose.Position + pose.Heading * lead + right * side + Vector3.up * height;
        }
    }
}
