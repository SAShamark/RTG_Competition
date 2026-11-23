using System;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public struct DriftCameraRig
    {
        [SerializeField] private float _lead;
        [SerializeField] private float _side;
        [SerializeField] private float _height;
        [SerializeField] private float _lookHeight;
        [SerializeField] private float _fov;
        [SerializeField] private float _roll;

        public DriftCameraRig(float lead, float side, float height, float lookHeight, float fov, float roll)
        {
            _lead = lead;
            _side = side;
            _height = height;
            _lookHeight = lookHeight;
            _fov = fov;
            _roll = roll;
        }

        public float Lead => _lead;
        public float Side => _side;
        public float Height => _height;
        public float LookHeight => _lookHeight;
        public float Fov => _fov;
        public float Roll => _roll;

        public static DriftCameraRig Lerp(DriftCameraRig a, DriftCameraRig b, float t) => new(
            Mathf.Lerp(a._lead, b._lead, t),
            Mathf.Lerp(a._side, b._side, t),
            Mathf.Lerp(a._height, b._height, t),
            Mathf.Lerp(a._lookHeight, b._lookHeight, t),
            Mathf.Lerp(a._fov, b._fov, t),
            Mathf.Lerp(a._roll, b._roll, t));
    }
}
