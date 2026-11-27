using System;
using Gameplay.Entities.Interactive;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class InteractionLogic
    {
        private const int MaxHits = 24;
        private const int IgnoreRaycastLayer = 2;

        [SerializeField] private float _reachRadius = 0.18f;
        [SerializeField] private float _pressRadius = 0.035f;
        [SerializeField] private LayerMask _mask = ~0;

        private readonly Collider[] _hits = new Collider[MaxHits];
        private Transform _owner;
        private IInteractable _current;
        private string _currentHint;
        private Func<IInteractable, bool> _filter;
        private int _queryMask;

        public IInteractable Current => _current;
        public float PressRadius => _pressRadius;

        public event Action<string> HintChanged;
        public event Action<Component> TargetChanged;

        public void Init(Transform owner, Func<IInteractable, bool> filter)
        {
            _owner = owner;
            _filter = filter;
            _queryMask = _mask & ~(1 << IgnoreRaycastLayer);
        }

        public void Tick(IInteractable right, IInteractable left)
        {
            IInteractable target = right ?? left;
            string hint = target != null ? target.GetHint(_owner) : null;
            if (!ReferenceEquals(target, _current))
            {
                TargetChanged?.Invoke(target as Component);
            }

            _current = target;
            if (hint != _currentHint)
            {
                _currentHint = hint;
                HintChanged?.Invoke(hint);
            }
        }

        public IInteractable Find(Vector3 point) => Find(point, _reachRadius);

        public IInteractable Find(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, _hits, _queryMask, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];
                if (hit.transform.IsChildOf(_owner))
                {
                    continue;
                }

                IInteractable interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract || !_filter(interactable))
                {
                    continue;
                }

                float distance = (ClosestPoint(hit, point) - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = interactable;
                }
            }

            return best;
        }

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

        private static Vector3 ClosestPoint(Collider collider, Vector3 point) =>
            collider is MeshCollider { convex: false } ? collider.bounds.ClosestPoint(point) : collider.ClosestPoint(point);
    }
}
