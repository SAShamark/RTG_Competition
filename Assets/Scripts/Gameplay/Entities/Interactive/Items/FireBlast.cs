using System.Collections.Generic;
using Services.Audio;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    public sealed class FireBlast : MonoBehaviour
    {
        private const int MaxHits = 48;

        private static readonly Collider[] Hits = new Collider[MaxHits];

        [SerializeField, Min(0f)] private float _radius = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _growth = 0.5f;
        [SerializeField] private LayerMask _mask = ~0;

        [Header("Shockwave")]
        [SerializeField, Min(0f)] private float _pushForce = 5f;
        [SerializeField, Min(0f)] private float _pushLift = 0.4f;

        [Header("Lingering fire")]
        [SerializeField] private FireSource _fire;
        [SerializeField, Min(0f)] private float _dropHeight = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _minFloorNormal = 0.7f;
        [SerializeField, Min(0f)] private float _burnDuration = 20f;
        [SerializeField, Min(0f)] private float _fadeDuration = 4f;

        [Header("Feedback")]
        [SerializeField] private SoundType _sound = SoundType.FinaleBoom;
        [SerializeField] private CinemachineImpulseSource _impulse;
        [SerializeField, Min(0f)] private float _shake = 0.35f;
        [SerializeField] private Light _flash;
        [SerializeField, Min(0.01f)] private float _flashDuration = 0.35f;

        private FireSource _left;
        private float _flashPeak;
        private float _time;
        private bool _isBurnedOut;

        private void Start()
        {
            Vector3 center = transform.position;
            _flashPeak = _flash.intensity;
            FanFires(center);
            Shock(center);
            LeaveFire(center);
            AudioService.Instance.PlayAt(_sound, center);
            _impulse.GenerateImpulse(_shake);
            Destroy(gameObject, _burnDuration + _fadeDuration);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            _flash.intensity = _flashPeak * (1f - Mathf.Clamp01(_time / _flashDuration));
            if (!_isBurnedOut && _time >= _burnDuration)
            {
                _isBurnedOut = true;
                if (_left != null)
                {
                    _left.ForceOut();
                }
            }
        }

        private void FanFires(Vector3 center)
        {
            IReadOnlyList<FireSource> fires = FireSource.Active;
            float reach = _radius * _radius;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                if ((fires[i].transform.position - center).sqrMagnitude <= reach)
                {
                    fires[i].Grow(_growth);
                }
            }
        }

        private void Shock(Vector3 center)
        {
            int count = Physics.OverlapSphereNonAlloc(center, _radius, Hits, _mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody pushed = Hits[i].attachedRigidbody;
                if (pushed != null && !pushed.isKinematic)
                {
                    pushed.AddExplosionForce(_pushForce, center, _radius, _pushLift, ForceMode.Impulse);
                }
            }
        }

        private void LeaveFire(Vector3 center)
        {
            if (Physics.Raycast(center, Vector3.down, out RaycastHit floor, _dropHeight, _mask, QueryTriggerInteraction.Ignore) && floor.normal.y >= _minFloorNormal)
            {
                _left = Instantiate(_fire, floor.point, Quaternion.identity, transform);
            }
        }
    }
}
