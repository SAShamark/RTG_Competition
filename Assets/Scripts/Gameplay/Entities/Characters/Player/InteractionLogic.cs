using System;
using System.Collections.Generic;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class InteractionLogic
    {
        private const int MaxHits = 32;

        private static readonly IComparer<RaycastHit> ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        [SerializeField] private float _distance = 2.6f;
        [SerializeField] private float _radius = 0.08f;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private float _blockerTolerance = 0.08f;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
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
            int count = Physics.SphereCastNonAlloc(_head.position, _radius, _head.forward, _hits, _distance, _mask,
                QueryTriggerInteraction.Collide);
            Array.Sort(_hits, 0, count, ByDistance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
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
                    for (int j = 0; j < count; j++)
                    {
                        RaycastHit near = _hits[j];
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
