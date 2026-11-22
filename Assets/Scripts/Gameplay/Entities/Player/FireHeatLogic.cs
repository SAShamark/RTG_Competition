using Gameplay.Entities.Interactive;
using System;
using UnityEngine;

namespace Gameplay.Entities.Player
{
    [Serializable]
    public sealed class FireHeatLogic
    {
        [SerializeField] private float _riseSpeed = 3f;
        [SerializeField] private float _fallSpeed = 1.2f;

        private Transform _target;

        public float Heat { get; private set; }

        public event Action<float> HeatChanged;

        public void Init(Transform target)
        {
            _target = target;
        }

        public void Tick(float deltaTime)
        {
            float wanted = 0f;
            var fires = FireSource.Active;
            for (int i = 0; i < fires.Count; i++)
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
