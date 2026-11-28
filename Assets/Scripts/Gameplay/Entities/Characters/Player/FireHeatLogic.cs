using System;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class FireHeatLogic
    {
        [SerializeField] private float _riseSpeed = 3f;
        [SerializeField] private float _fallSpeed = 1.2f;

        [Header("Burning")]
        [SerializeField, Range(0f, 1f)] private float _burnThreshold = 0.12f;
        [SerializeField, Min(0.1f)] private float _burnTime = 7f;
        [SerializeField, Min(0.1f)] private float _recoverTime = 5f;

        private Transform _target;
        private ShieldLogic _shield;

        public float Heat { get; private set; }
        public float Burn { get; private set; }
        public bool IsBurnedOut { get; private set; }

        public event Action<float> HeatChanged;
        public event Action<float> BurnChanged;
        public event Action BurnedOut;

        public void Init(Transform target, ShieldLogic shield)
        {
            _target = target;
            _shield = shield;
        }

        public void Tick(float deltaTime, bool isExposed)
        {
            TickHeat(deltaTime);
            if (!IsBurnedOut)
            {
                TickBurn(deltaTime, isExposed);
            }
        }

        public void Recover()
        {
            IsBurnedOut = false;
            Heat = 0f;
            Burn = 0f;
            HeatChanged?.Invoke(Heat);
            BurnChanged?.Invoke(Burn);
        }

        private void TickBurn(float deltaTime, bool isExposed)
        {
            bool isBurning = isExposed && Heat > _burnThreshold;
            float rate = isBurning ? Heat / _burnTime : -1f / _recoverTime;
            float next = Mathf.Clamp01(Burn + rate * deltaTime);
            if (Mathf.Approximately(next, Burn))
            {
                return;
            }

            Burn = next;
            BurnChanged?.Invoke(Burn);
            if (Burn >= 1f)
            {
                IsBurnedOut = true;
                BurnedOut?.Invoke();
            }
        }

        private void TickHeat(float deltaTime)
        {
            float wanted = 0f;
            var fires = FireSource.Active;
            for (int i = 0; i < fires.Count && !_shield.IsActive; i++)
            {
                wanted = Mathf.Max(wanted, fires[i].HeatAt(_target.position));
            }

            float speed = wanted > Heat ? _riseSpeed : _fallSpeed;
            float next = Mathf.MoveTowards(Heat, wanted, speed * deltaTime);
            if (!Mathf.Approximately(next, Heat))
            {
                Heat = next;
                HeatChanged?.Invoke(Heat);
            }
        }
    }
}
