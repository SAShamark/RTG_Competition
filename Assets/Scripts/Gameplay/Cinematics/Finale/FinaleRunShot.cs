using System;
using Services;
using UnityEngine;

namespace Gameplay.Cinematics.Finale
{
    [Serializable]
    public sealed class FinaleRunShot
    {
        [SerializeField] private float _runStopDistance = 0.9f;
        [SerializeField] private float _runLookDown = 0.35f;
        [SerializeField] private float _runBobFrequency = 11f;
        [SerializeField] private float _runBobHeight = 0.04f;
        [SerializeField] private float _runTurnSharpness = 5f;

        private Vector3 _from;
        private Vector3 _to;
        private Quaternion _rotation;

        public void Begin(Transform head, Vector3 launch, Vector3 outward)
        {
            _from = head.position;
            _to = new Vector3(launch.x, _from.y, launch.z) - outward * _runStopDistance;
            _rotation = Quaternion.LookRotation(outward + Vector3.down * _runLookDown);
        }

        public void Tick(Transform cam, float time, float duration)
        {
            float u = Easing.Smooth(time / duration);
            Vector3 bob = Vector3.up * Mathf.Abs(Mathf.Sin(time * _runBobFrequency)) * _runBobHeight * (1f - u);
            Quaternion rotation = Quaternion.Slerp(cam.rotation, _rotation, Easing.Damp(_runTurnSharpness, Time.deltaTime));
            cam.SetPositionAndRotation(Vector3.Lerp(_from, _to, u) + bob, rotation);
        }
    }
}
