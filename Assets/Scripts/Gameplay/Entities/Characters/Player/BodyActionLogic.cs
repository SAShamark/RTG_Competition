using System;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class BodyActionLogic
    {
        [SerializeField, Range(0f, 1f)] private float _reachReleaseShare = 0.6f;

        private PlayerBodyIK _bodyIK;
        private Action _contact;
        private float _time;
        private float _contactTime;
        private float _duration;
        private Vector3 _reachPoint;
        private float _reachStrength;

        public bool IsBusy => _duration > 0f;
        public bool LocksMovement { get; private set; }

        public event Action Finished;

        public void Init(PlayerBodyIK bodyIK) => _bodyIK = bodyIK;

        public void Start(float contactTime, float duration, bool locksMovement, Action onContact, Vector3 reachPoint = default, float reachStrength = 0f)
        {
            _reachPoint = reachPoint;
            _reachStrength = reachStrength;
            _time = 0f;
            _contactTime = contactTime;
            _duration = duration;
            LocksMovement = locksMovement;
            _contact = onContact;
        }

        public void Extend(float extraDuration) => _duration = _time + extraDuration;

        public void ClearReach() => _bodyIK.SetReach(_reachPoint, 0f);

        public void Tick(float deltaTime, bool isPaused)
        {
            if (!IsBusy || isPaused)
            {
                return;
            }

            _time += deltaTime;
            _bodyIK.SetReach(_reachPoint, ReachWeight() * _reachStrength);
            if (_contact != null && _time >= _contactTime)
            {
                Action contact = _contact;
                _contact = null;
                contact();
            }

            if (_time >= _duration)
            {
                _duration = 0f;
                LocksMovement = false;
                ClearReach();
                Finished?.Invoke();
            }
        }

        private float ReachWeight()
        {
            if (_time <= _contactTime)
            {
                return Mathf.SmoothStep(0f, 1f, _time / _contactTime);
            }

            float release = (_duration - _contactTime) * _reachReleaseShare;
            return Mathf.SmoothStep(1f, 0f, (_time - _contactTime) / release);
        }
    }
}
