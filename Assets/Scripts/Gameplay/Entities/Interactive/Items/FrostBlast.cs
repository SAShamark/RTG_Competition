using System.Collections.Generic;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class FrostBlast : MonoBehaviour
    {
        private const int MaxHits = 96;
        private const float Instant = 1f;

        private static readonly Collider[] Hits = new Collider[MaxHits];
        private static readonly HashSet<BreakableGlass> Panes = new();

        [SerializeField, Min(0f)] private float _radius = 5f;
        [SerializeField, Min(0f)] private float _height = 2f;
        [SerializeField, Min(0f)] private float _power = 100f;
        [SerializeField] private LayerMask _mask = ~0;
        [SerializeField] private FrostField _frost;
        [SerializeField, Min(0f)] private float _freezeRadius = 3.5f;

        [Header("Shockwave")]
        [SerializeField, Min(0f)] private float _pushForce = 4f;
        [SerializeField, Min(0f)] private float _pushLift = 0.3f;

        [Header("Feedback")]
        [SerializeField] private SoundType _sound = SoundType.FrostBlast;
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField] private Light _flash;
        [SerializeField, Min(0.01f)] private float _flashDuration = 0.4f;
        [SerializeField, Min(0f)] private float _lifetime = 6f;

        private float _flashPeak;
        private float _time;

        private void Start()
        {
            Vector3 center = transform.position;
            _flashPeak = _flash.intensity;
            PutOutFires(center);
            Shock(center);
            Instantiate(_frost, center, Quaternion.identity);
            AudioService.Instance.PlayAt(_sound, center);
            _impulse.GenerateImpulse();
            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            _flash.intensity = _flashPeak * (1f - Mathf.Clamp01(_time / _flashDuration));
        }

        private void PutOutFires(Vector3 center)
        {
            IReadOnlyList<FireSource> fires = FireSource.Active;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                if (IsInReach(fires[i].transform.position, center))
                {
                    fires[i].Extinguish(_power, Instant);
                }
            }
        }

        private void Shock(Vector3 center)
        {
            int count = Physics.OverlapSphereNonAlloc(center, _radius, Hits, _mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i];
                Vector3 point = hit.ClosestPoint(center);
                if (!IsInReach(point, center))
                {
                    continue;
                }

                if (hit.TryGetComponent(out IFreezable target))
                {
                    if ((point - center).sqrMagnitude <= _freezeRadius * _freezeRadius)
                    {
                        target.Freeze(center);
                    }

                    continue;
                }

                BreakableGlass glass = hit.GetComponentInParent<BreakableGlass>();
                if (glass != null)
                {
                    if (Panes.Add(glass))
                    {
                        glass.Smash(point, (point - center).normalized, false);
                    }

                    continue;
                }

                Rigidbody pushed = hit.attachedRigidbody;
                if (pushed != null && !pushed.isKinematic)
                {
                    pushed.AddExplosionForce(_pushForce, center, _radius, _pushLift, ForceMode.Impulse);
                }
            }

            Panes.Clear();
        }

        private bool IsInReach(Vector3 point, Vector3 center)
        {
            Vector3 offset = point - center;
            float height = offset.y;
            offset.y = 0f;
            return Mathf.Abs(height) <= _height && offset.sqrMagnitude <= _radius * _radius;
        }
    }
}
