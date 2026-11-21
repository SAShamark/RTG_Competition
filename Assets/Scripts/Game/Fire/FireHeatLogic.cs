using System;
using UnityEngine;

namespace Game.Fire
{
    [Serializable]
    public class FireHeatLogic
    {
        [SerializeField] private float _riseSpeed = 3f;
        [SerializeField] private float _fallSpeed = 1.2f;

        private Transform _target;
        private float _heat;

        public float Heat => _heat;

        public event Action<float> OnHeatChanged;

        public void Init(Transform target)
        {
            _target = target;
        }

        public void Tick(float deltaTime)
        {
            if (_target == null)
            {
                return;
            }

            float wanted = 0f;
            var fires = FireSource.Active;
            for (int i = 0; i < fires.Count; i++)
            {
                wanted = Mathf.Max(wanted, fires[i].HeatAt(_target.position));
            }

            float speed = wanted > _heat ? _riseSpeed : _fallSpeed;
            float next = Mathf.MoveTowards(_heat, wanted, speed * deltaTime);
            if (!Mathf.Approximately(next, _heat))
            {
                _heat = next;
                OnHeatChanged?.Invoke(_heat);
            }
        }
    }
}
