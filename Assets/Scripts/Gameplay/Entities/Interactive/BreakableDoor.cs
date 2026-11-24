using System;
using System.Collections.Generic;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class BreakableDoor : MonoBehaviour, IChoppable
    {
        [Serializable]
        public sealed class Chunk
        {
            [SerializeField] private Rigidbody _body;
            [SerializeField] private Collider _collider;
            [SerializeField] private Vector2 _center;

            public Rigidbody Body => _body;
            public Collider Collider => _collider;
            public Vector2 Center => _center;
            public bool IsKnocked { get; set; }

            public Chunk(Rigidbody body, Collider collider, Vector2 center)
            {
                _body = body;
                _collider = collider;
                _center = center;
            }
        }

        [SerializeField] private Chunk[] _chunks;
        [SerializeField] private Collider _blocker;
        [SerializeField] private Collider[] _extraBlockers = Array.Empty<Collider>();
        [SerializeField] private ParticleSystem _splinters;
        [SerializeField] private ParticleSystem[] _impacts;
        [SerializeField] private Rect _breachArea = new(-0.8f, 0.35f, 0.6f, 1.25f);
        [SerializeField] private float _breachRatio = 0.5f;
        [SerializeField] private float _hitRadius = 0.38f;
        [SerializeField] private float _radiusJitter = 0.05f;
        [SerializeField] private float _knockSpeed = 1.8f;
        [SerializeField] private float _knockSpread = 0.5f;
        [SerializeField] private float _knockSpin = 6f;
        [SerializeField] private float _debrisLifetime = 10f;
        [SerializeField] private int _splintersPerChunk = 4;
        [SerializeField] private float _breachClearHeight = 1.55f;
        [SerializeField] private string _debrisLayer = "Debris";

        private readonly List<(Chunk chunk, float expires)> _debris = new();
        private int _breachTotal;
        private int _breachKnocked;

        public bool IsChopped { get; private set; }
        public bool IsBreached { get; private set; }

        public event Action<BreakableDoor, Vector3> Chopped;
        public event Action<BreakableDoor> Breached;

        private void Awake()
        {
            foreach (Chunk chunk in _chunks)
            {
                if (_breachArea.Contains(chunk.Center))
                {
                    _breachTotal++;
                }
            }
        }

        public void Chop(Vector3 point, Vector3 direction)
        {
            Vector2 local = transform.InverseTransformPoint(point);
            float radius = _hitRadius + UnityEngine.Random.Range(-_radiusJitter, _radiusJitter);
            Chunk nearest = null;
            float nearestDistance = float.MaxValue;
            int knocked = 0;

            foreach (Chunk chunk in _chunks)
            {
                if (chunk.IsKnocked)
                {
                    continue;
                }

                float distance = Vector2.Distance(chunk.Center, local);
                if (distance < radius)
                {
                    Knock(chunk, direction);
                    knocked++;
                }
                else if (distance < nearestDistance)
                {
                    nearest = chunk;
                    nearestDistance = distance;
                }
            }

            if (knocked == 0 && nearest != null)
            {
                Knock(nearest, direction);
                knocked++;
            }

            _splinters.transform.position = point;
            _splinters.transform.rotation = Quaternion.LookRotation(-direction);
            _splinters.Emit(_splintersPerChunk * knocked);
            foreach (ParticleSystem impact in _impacts)
            {
                impact.transform.SetPositionAndRotation(point, _splinters.transform.rotation);
                EffectPlayback.Restart(impact);
            }

            IsChopped = true;
            Chopped?.Invoke(this, point);

            if (!IsBreached && _breachKnocked >= _breachTotal * _breachRatio)
            {
                IsBreached = true;
                foreach (Chunk chunk in _chunks)
                {
                    if (!chunk.IsKnocked && chunk.Center.y < _breachClearHeight)
                    {
                        Knock(chunk, direction);
                    }
                }

                _blocker.enabled = false;
                foreach (Collider extra in _extraBlockers)
                {
                    extra.enabled = false;
                }

                Breached?.Invoke(this);
            }
        }

        private void Knock(Chunk chunk, Vector3 direction)
        {
            chunk.IsKnocked = true;
            if (_breachArea.Contains(chunk.Center))
            {
                _breachKnocked++;
            }

            Physics.IgnoreCollision(chunk.Collider, _blocker);
            foreach (Collider extra in _extraBlockers)
            {
                Physics.IgnoreCollision(chunk.Collider, extra);
            }

            chunk.Body.gameObject.layer = LayerMask.NameToLayer(_debrisLayer);
            Rigidbody body = chunk.Body;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.linearVelocity = (direction + UnityEngine.Random.insideUnitSphere * _knockSpread) * _knockSpeed;
            body.angularVelocity = UnityEngine.Random.insideUnitSphere * _knockSpin;
            _debris.Add((chunk, Time.time + _debrisLifetime));
        }

        private void Update()
        {
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                if (Time.time < _debris[i].expires)
                {
                    continue;
                }

                _debris[i].chunk.Body.gameObject.SetActive(false);
                _debris.RemoveAt(i);
            }
        }
    }
}
