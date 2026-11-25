using System;
using UnityEngine;

namespace Gameplay.Cinematics.IntroDrift
{
    [Serializable]
    public sealed class DriftCut
    {
        [SerializeField] private string _name;
        [SerializeField] private float _from;
        [SerializeField] private float _to;
        [SerializeField] private bool _isAnchored;
        [SerializeField] private float _anchor;
        [SerializeField] private DriftCameraRig _start;
        [SerializeField] private DriftCameraRig _end;

        public DriftCut(string name, float from, float to, DriftCameraRig start, DriftCameraRig end)
        {
            _name = name;
            _from = from;
            _to = to;
            _start = start;
            _end = end;
        }

        public DriftCut(string name, float from, float to, float anchor, DriftCameraRig start, DriftCameraRig end)
            : this(name, from, to, start, end)
        {
            _isAnchored = true;
            _anchor = anchor;
        }

        public bool IsAnchored => _isAnchored;
        public float Anchor => _anchor;

        public bool Contains(float fromTurnIn) => fromTurnIn >= _from && fromTurnIn < _to;

        public DriftCameraRig RigAt(float fromTurnIn) => DriftCameraRig.Lerp(_start, _end, Mathf.InverseLerp(_from, _to, fromTurnIn));
    }
}
