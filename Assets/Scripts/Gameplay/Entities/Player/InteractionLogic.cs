using Gameplay.Entities.Interactive;
using System;
using UnityEngine;

namespace Gameplay.Entities.Player
{
    [Serializable]
    public sealed class InteractionLogic
    {
        [SerializeField] private float _distance = 2.6f;
        [SerializeField] private float _radius = 0.08f;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private float _blockerTolerance = 0.08f;

        private Transform _head;
        private Transform _owner;
        private IInteractable _current;
        private string _currentHint;
        private Func<IInteractable, bool> _filter;

        public IInteractable Current => _current;

        public void SetFilter(Func<IInteractable, bool> filter) => _filter = filter;

        public void SetOverride(IInteractable target) => _override = target;

        private IInteractable _override;

        public event Action<string> HintChanged;
        public event Action<Component> TargetChanged;

        public void Init(Transform owner, Transform head)
        {
            _owner = owner;
            _head = head;
        }

        public void Tick()
        {
            IInteractable target = _override ?? FindTarget();
            if (_override == null && target != null && !_filter(target))
            {
                target = null;
            }

            string hint = target != null && target.CanInteract ? target.GetHint(_owner) : null;
            if (!ReferenceEquals(target, _current))
            {
                TargetChanged?.Invoke(target != null && target.CanInteract ? target as Component : null);
            }

            _current = target;

            if (hint != _currentHint)
            {
                _currentHint = hint;
                HintChanged?.Invoke(hint);
            }
        }

        public void TryInteract() => Interact(_current);

        public void Interact(IInteractable target)
        {
            if (target != null && target.CanInteract)
            {
                target.Interact(_owner);
            }
        }

        public void Clear()
        {
            if (_current != null)
            {
                TargetChanged?.Invoke(null);
            }

            _current = null;
            if (_currentHint != null)
            {
                _currentHint = null;
                HintChanged?.Invoke(null);
            }
        }

        private IInteractable FindTarget()
        {
            RaycastHit[] hits = Physics.SphereCastAll(_head.position, _radius, _head.forward, _distance, _mask,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(_owner))
                {
                    continue;
                }

                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    return interactable;
                }

                if (!hit.collider.isTrigger)
                {
                    foreach (RaycastHit near in hits)
                    {
                        if (near.distance > hit.distance && (near.distance - hit.distance < _blockerTolerance || hit.collider.bounds.Contains(near.collider.bounds.center)))
                        {
                            IInteractable item = near.collider.GetComponentInParent<IInteractable>();
                            if (item != null)
                            {
                                return item;
                            }
                        }
                    }

                    return null;
                }
            }

            return null;
        }
    }
}
