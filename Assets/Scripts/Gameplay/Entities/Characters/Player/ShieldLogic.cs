using System;
using System.Collections.Generic;
using Gameplay.Entities.Interactive;
using Gameplay.Entities.Interactive.Items;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class ShieldLogic
    {
        private const float MinDuration = 0.5f;

        [SerializeField] private ShieldEffect _effect;
        [SerializeField, Min(MinDuration)] private float _duration = 10f;
        [SerializeField, Min(0f)] private float _cooldown = 15f;

        private readonly List<Collider> _passedFires = new();
        private InventoryLogic _items;
        private Collider _body;
        private ShieldRemoteItem _remote;
        private float _remaining;
        private float _cooldownLeft;

        public bool IsActive => _remaining > 0f;
        public bool IsReady => _items.HeldItem == ItemType.ShieldRemote && !IsActive && !_effect.IsVisible && _cooldownLeft <= 0f;
        public bool CanToggle => _items.HeldItem == ItemType.ShieldRemote && (IsActive || IsReady);
        public float Remaining => _remaining;

        public event Action Activated;
        public event Action Expired;

        public void Init(InventoryLogic items, Collider body)
        {
            _items = items;
            _body = body;
            _effect.Init();
            _effect.Detached += HandleDetached;
        }

        public void Dispose() => _effect.Detached -= HandleDetached;

        public void Toggle()
        {
            if (IsActive)
            {
                Deactivate(0f);
                return;
            }

            Activate();
        }

        private void Activate()
        {
            _remaining = _duration;
            _remote = _items.HeldTool.GetComponent<ShieldRemoteItem>();
            _remote.SetPressed(true);
            _effect.Show(_remote.Emitter);
            AudioService.Instance.Play(SoundType.ElevatorClick);
            Activated?.Invoke();
        }

        public void Tick(float deltaTime)
        {
            if (!IsActive)
            {
                _cooldownLeft = Mathf.Max(0f, _cooldownLeft - deltaTime);
                return;
            }

            PassThroughFires();
            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                Deactivate(_cooldown);
            }
        }

        private void Deactivate(float cooldown)
        {
            _remaining = 0f;
            _cooldownLeft = cooldown;
            BlockFires();
            _remote.SetPressed(false);
            _effect.Hide(_remote.Emitter);
            AudioService.Instance.Play(SoundType.ElevatorClick);
            Expired?.Invoke();
        }

        private void HandleDetached(bool isDetached) => _remote.SetEmitting(isDetached);

        private void PassThroughFires()
        {
            IReadOnlyList<FireSource> fires = FireSource.Active;
            for (int i = 0; i < fires.Count; i++)
            {
                Collider blocker = fires[i].Blocker;
                if (!_passedFires.Contains(blocker))
                {
                    Physics.IgnoreCollision(_body, blocker, true);
                    _passedFires.Add(blocker);
                }
            }
        }

        private void BlockFires()
        {
            foreach (Collider blocker in _passedFires)
            {
                Physics.IgnoreCollision(_body, blocker, false);
            }

            _passedFires.Clear();
        }
    }
}
