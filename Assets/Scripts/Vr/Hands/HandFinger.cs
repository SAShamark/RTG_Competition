using System;
using UnityEngine;

namespace Vr.Hands
{
    [Serializable]
    public sealed class HandFinger
    {
        private const int FitSteps = 24;
        private const int SegmentSamples = 2;
        private const float FitBackoff = 0.5f;
        private const float TipExtension = 0.75f;
        private const float MinHoldBlend = 0.35f;
        private const float FreeIndexRest = 0.25f;

        [SerializeField] private Transform[] _joints;
        [SerializeField] private Transform _tip;
        [SerializeField] private Vector3[] _axes;
        [SerializeField] private float[] _maxCurl;
        [SerializeField] private bool _isIndex;

        private Quaternion[] _rest;
        private float _amount;
        private float _fitted = 1f;

        public void Initialize()
        {
            _rest = new Quaternion[_joints.Length];
            for (int i = 0; i < _joints.Length; i++)
            {
                _rest[i] = _joints[i].localRotation;
            }
        }

        public void FitTo(GripCapsule grip)
        {
            _fitted = 1f;
            for (int step = 0; step <= FitSteps; step++)
            {
                float amount = (float)step / FitSteps;
                Pose(amount);
                if (!Touches(grip))
                {
                    continue;
                }

                _fitted = Mathf.Max(0f, amount - FitBackoff / FitSteps);
                break;
            }

            Pose(_amount);
        }

        public void Tick(bool isHolding, bool isIndexFree, float grip, float trigger, float blend)
        {
            float target = isHolding ? HoldTarget(isIndexFree, trigger) : _isIndex ? Mathf.Max(grip, trigger) : grip;
            _amount = Mathf.Lerp(_amount, target, isHolding ? Mathf.Max(blend, MinHoldBlend) : blend);
            Pose(_amount);
        }

        private float HoldTarget(bool isIndexFree, float trigger) =>
            _isIndex && isIndexFree ? Mathf.Lerp(FreeIndexRest * _fitted, _fitted, trigger) : _fitted;

        private void Pose(float amount)
        {
            for (int i = 0; i < _joints.Length; i++)
            {
                _joints[i].localRotation = _rest[i] * Quaternion.AngleAxis(_maxCurl[i] * amount, _axes[i]);
            }
        }

        private bool Touches(GripCapsule grip)
        {
            for (int i = 0; i < _joints.Length; i++)
            {
                Vector3 start = _joints[i].position;
                Vector3 end = i + 1 < _joints.Length ? _joints[i + 1].position : TipPosition();
                for (int sample = 1; sample <= SegmentSamples; sample++)
                {
                    if (grip.Contains(Vector3.Lerp(start, end, (float)sample / SegmentSamples)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Vector3 TipPosition()
        {
            if (_tip != null)
            {
                return _tip.position;
            }

            int last = _joints.Length - 1;
            Vector3 previous = last > 0 ? _joints[last - 1].position : _joints[last].parent.position;
            return _joints[last].position + (_joints[last].position - previous) * TipExtension;
        }
    }
}
