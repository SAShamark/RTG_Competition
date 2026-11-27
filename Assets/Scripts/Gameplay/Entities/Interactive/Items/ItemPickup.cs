using Gameplay.Dialogue;
using Gameplay.Entities.Interactive;
using Services.Audio;
using System;
using Gameplay.Entities.Characters.Player;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public abstract class ItemPickup : Interactable, IPullable
    {
        private Vector3 _pullHome;
        private Vector3? _pullCentre;
        private Quaternion _pullHomeRotation;

        public bool IsTaken { get; private set; }
        public override bool CanInteract => !IsTaken;
        protected abstract string DisplayName { get; }

        public virtual bool CanPull => !IsTaken && gameObject.activeInHierarchy;
        public virtual Vector3 PullPoint => transform.TransformPoint(_pullCentre ??= VisualCentre());

        public event Action<ItemPickup> Taken;

        public virtual void BeginPull()
        {
            _pullHome = transform.position;
            _pullHomeRotation = transform.rotation;
            SetPullCollidersEnabled(false);
        }

        public void MovePull(Vector3 pullPoint) => transform.position += pullPoint - PullPoint;

        public virtual void EndPull(Vector3 velocity)
        {
            transform.SetPositionAndRotation(_pullHome, _pullHomeRotation);
            SetPullCollidersEnabled(true);
        }

        public override string GetHint(Transform interactor) => _texts.Format(TextId.HintTake, DisplayName);

        protected override void OnInteract(Transform interactor)
        {
            if (IsTaken || !TryCollect(interactor))
            {
                return;
            }

            IsTaken = true;
            AudioService.Instance.Play(SoundType.ItemPickup);
            OnCollected();
            Taken?.Invoke(this);
        }

        protected abstract bool TryCollect(Transform interactor);

        protected virtual void OnCollected() => gameObject.SetActive(false);

        protected void ReturnToWorld() => IsTaken = false;

        protected virtual void OnEnable() => Pullables.All.Add(this);

        protected virtual void OnDisable() => Pullables.All.Remove(this);

        private Vector3 VisualCentre()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return Vector3.zero;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer pickupRenderer in renderers)
            {
                bounds.Encapsulate(pickupRenderer.bounds);
            }

            return transform.InverseTransformPoint(bounds.center);
        }

        private void SetPullCollidersEnabled(bool isEnabled)
        {
            foreach (Collider pickupCollider in GetComponentsInChildren<Collider>())
            {
                pickupCollider.enabled = isEnabled;
            }
        }
    }
}
