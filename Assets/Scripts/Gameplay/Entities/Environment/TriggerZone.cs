using System;
using UnityEngine;

namespace Gameplay.Entities.Environment
{
    [RequireComponent(typeof(Collider))]
    public sealed class TriggerZone : MonoBehaviour
    {
        private bool _fired;

        public event Action<TriggerZone> Entered;

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || other.GetComponent<CharacterController>() == null)
            {
                return;
            }

            _fired = true;
            Entered?.Invoke(this);
        }
    }
}
