using System;
using Gameplay.Entities.Characters.Residents;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class CarryLogic
    {
        [SerializeField] private Transform _socket;
        [SerializeField] private float _speedMultiplier = 0.7f;
        [SerializeField] private float _putDownDistance = 0.9f;
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private float _chestHeight = 1.1f;
        [SerializeField] private float _wallPadding = 0.3f;
        [SerializeField] private float _minPutDownDistance = 0.3f;
        [SerializeField] private float _wallClearance = 0.35f;
        [SerializeField] private float _groundProbeDistance = 2.5f;

        private Transform _owner;
        private Rescuable _carried;

        public bool IsCarrying => _carried != null;
        public Rescuable Carried => _carried;
        public float SpeedMultiplier => _speedMultiplier;

        public event Action<Rescuable> CarryChanged;

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
            CarryChanged?.Invoke(resident);
            return true;
        }

        public Rescuable Release()
        {
            Rescuable resident = _carried;
            _carried = null;
            if (resident != null)
            {
                CarryChanged?.Invoke(null);
            }

            return resident;
        }

        public bool PutDown()
        {
            if (_carried == null)
            {
                return false;
            }

            Vector3 chest = _owner.position + Vector3.up * _chestHeight;
            float distance = _putDownDistance;
            if (Physics.Raycast(chest, _owner.forward, out RaycastHit wallHit, _putDownDistance + _wallPadding, _groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Max(_minPutDownDistance, wallHit.distance - _wallClearance);
            }

            Vector3 probe = chest + _owner.forward * distance;
            Vector3 point = _owner.position + _owner.forward * distance;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit groundHit, _groundProbeDistance, _groundMask, QueryTriggerInteraction.Ignore))
            {
                point = groundHit.point;
            }

            Rescuable resident = Release();
            resident.PlaceAt(point, Quaternion.LookRotation(-_owner.forward, Vector3.up));
            return true;
        }
    }
}
