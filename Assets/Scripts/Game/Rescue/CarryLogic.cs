using System;
using UnityEngine;

namespace Game.Rescue
{
    [Serializable]
    public class CarryLogic
    {
        [SerializeField] private Transform _socket;
        [SerializeField] private float _speedMultiplier = 0.7f;
        [SerializeField] private float _putDownDistance = 0.9f;
        [SerializeField] private LayerMask _groundMask = ~0;

        private Transform _owner;
        private Rescuable _carried;

        public bool IsCarrying => _carried != null;
        public Rescuable Carried => _carried;
        public float SpeedMultiplier => _speedMultiplier;

        public event Action<Rescuable> OnCarryChanged;

        public void Init(Transform owner)
        {
            _owner = owner;
        }

        public bool PickUp(Rescuable resident)
        {
            if (_carried != null)
            {
                return false;
            }

            _carried = resident;
            resident.AttachTo(_socket);
            OnCarryChanged?.Invoke(resident);
            return true;
        }

        public Rescuable Release()
        {
            Rescuable resident = _carried;
            _carried = null;
            if (resident != null)
            {
                OnCarryChanged?.Invoke(null);
            }

            return resident;
        }

        public bool PutDown()
        {
            if (_carried == null)
            {
                return false;
            }

            Vector3 chest = _owner.position + Vector3.up * 1.1f;
            float distance = _putDownDistance;
            if (Physics.Raycast(chest, _owner.forward, out RaycastHit wallHit, _putDownDistance + 0.3f, _groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(0.3f, wallHit.distance - 0.35f);
            }

            Vector3 probe = chest + _owner.forward * distance;
            Vector3 point = _owner.position + _owner.forward * distance;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit groundHit, 2.5f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                point = groundHit.point;
            }

            Rescuable resident = Release();
            resident.PlaceAt(point, Quaternion.LookRotation(-_owner.forward, Vector3.up));
            return true;
        }
    }
}
