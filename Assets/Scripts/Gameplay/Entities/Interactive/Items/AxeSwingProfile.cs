using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class AxeSwingProfile
    {
        [SerializeField] private float _contact = 0.38f;
        [SerializeField] private float _duration = 0.6f;
        [SerializeField, Range(0.1f, 0.9f)] private float _windupShare = 0.6f;
        [SerializeField] private float _swingSoundLead = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _moveFactor = 0.7f;

        public float Contact => _contact;
        public float Duration => _duration;
        public float Windup => _contact * _windupShare;
        public float SwingSoundTime => Mathf.Max(0f, _contact - _swingSoundLead);
        public float MoveFactor => _moveFactor;
    }
}
