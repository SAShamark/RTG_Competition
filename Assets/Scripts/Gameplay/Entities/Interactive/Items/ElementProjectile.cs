using System.Collections.Generic;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class ElementProjectile : MonoBehaviour
    {
        private const int MaxShocked = 24;
        private const float Instant = 1f;
        private const float MinSpeedSqr = 0.01f;

        private static readonly Collider[] Shocked = new Collider[MaxShocked];

        [SerializeField] private ElementType _element;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private Collider _collider;
        [SerializeField] private Transform _model;
        [SerializeField, Min(0f)] private float _lifetime = 6f;
        [SerializeField, Min(0f)] private float _rollSpeed = 720f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float _radius = 1.6f;
        [SerializeField, Min(0f)] private float _power = 0.5f;
        [SerializeField, Min(0f)] private float _shockLift = 0.3f;
        [SerializeField, Min(0f)] private float _frostOffset = 0.1f;
        [SerializeField] private SoundType _impactSound = SoundType.PropImpact;
        [SerializeField] private FrostField _frost;
        [SerializeField] private GameObject _impactEffect;
        [SerializeField, Min(0f)] private float _effectOffset = 0.15f;

        private bool _hasHit;
        private float _roll;

        public void Launch(Vector3 velocity, Collider owner)
        {
            Physics.IgnoreCollision(_collider, owner, true);
            _body.linearVelocity = velocity;
            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            Vector3 velocity = _body.linearVelocity;
            if (velocity.sqrMagnitude < MinSpeedSqr)
            {
                return;
            }

            _roll += _rollSpeed * Time.deltaTime;
            _model.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, 0f, _roll);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasHit)
            {
                return;
            }

            _hasHit = true;
            ContactPoint contact = collision.GetContact(0);
            Vector3 point = contact.point;
            Apply(point);
            if (_frost != null)
            {
                Instantiate(_frost, point + contact.normal * _frostOffset, Quaternion.identity);
            }

            if (_impactEffect != null)
            {
                Instantiate(_impactEffect, point + contact.normal * _effectOffset, Quaternion.LookRotation(contact.normal));
            }

            AudioService.Instance.PlayAt(_impactSound, point);
            Destroy(gameObject);
        }

        private void Apply(Vector3 point)
        {
            if (_element == ElementType.Lightning)
            {
                Shock(point);
                return;
            }

            IReadOnlyList<FireSource> fires = FireSource.Active;
            float reach = _radius * _radius;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                FireSource fire = fires[i];
                if ((fire.transform.position - point).sqrMagnitude > reach)
                {
                    continue;
                }

                switch (_element)
                {
                    case ElementType.Water:
                        fire.Douse(_power, Instant);
                        break;
                    case ElementType.Ice:
                        fire.Extinguish(_power, Instant);
                        break;
                    case ElementType.Fire:
                        fire.Grow(_power);
                        break;
                }
            }
        }

        private void Shock(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, _radius, Shocked, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody pushed = Shocked[i].attachedRigidbody;
                if (pushed != null && pushed != _body && !pushed.isKinematic)
                {
                    pushed.AddExplosionForce(_power, point, _radius, _shockLift, ForceMode.Impulse);
                }
            }
        }
    }
}
