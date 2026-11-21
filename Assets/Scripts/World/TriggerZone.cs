using System;
using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(Collider))]
    public class TriggerZone : MonoBehaviour
    {
        private bool _fired;

        public event Action<TriggerZone> OnEntered;

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || other.GetComponent<CharacterController>() == null)
            {
                return;
            }

            _fired = true;
            OnEntered?.Invoke(this);
        }
    }
}
