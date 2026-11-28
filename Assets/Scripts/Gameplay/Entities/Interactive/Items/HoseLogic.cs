using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive.Items
{
    [Serializable]
    public sealed class HoseLogic
    {
        [SerializeField] private WaterJetPath _path = new();
        [SerializeField] private float _convergeDistance = 12f;
        [SerializeField, Range(0f, 1f)] private float _minSupply = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _tautTension = 0.5f;

        [Header("Dousing")]
        [SerializeField] private float _power = 1.5f;
        [SerializeField] private float _splashRadius = 1.6f;
        [SerializeField] private float _streamRadius = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _edgePowerFactor = 0.35f;
        [SerializeField] private float _fireCentreHeight = 0.6f;
        [SerializeField] private float _sightLift = 0.1f;
        [SerializeField] private float _sightSlack = 0.5f;

        [Header("Runoff")]
        [SerializeField, Min(0f)] private float _flowRate = 6f;
        [SerializeField] private WaterTraceLayer _traces = new();

        [Header("Push")]
        [SerializeField, Min(0f)] private float _pushForce = 60f;

        [Header("Recoil")]
        [SerializeField] private float _recoilPush = 2.4f;
        [SerializeField] private float _recoilJump = 2.5f;

        [Header("Aim kick")]
        [SerializeField] private float _kickClimb = 9f;
        [SerializeField] private float _kickSway = 16f;
        [SerializeField] private float _kickFrequency = 1.7f;
        [SerializeField, Range(0f, 1f)] private float _kickPitchShare = 0.5f;

        private Transform _aim;
        private Transform _owner;
        private InventoryLogic _items;
        private HoseNozzleItem _current;
        private float _reach;
        private float _cut;
        private bool _wasWanted;
        private bool _wasTaut;

        public bool IsSpraying { get; private set; }
        // Degrees of look the jet adds this frame: a steady climb plus a slow wander the player has to hold against.
        public Vector2 AimKick(float deltaTime)
        {
            if (!IsSpraying)
            {
                return Vector2.zero;
            }

            float time = Time.time * _kickFrequency;
            float yaw = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * _kickSway;
            float pitch = _kickClimb + (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * _kickSway * _kickPitchShare;
            return new Vector2(yaw, pitch) * (_current.Supply * deltaTime);
        }

        public float JumpBoost => IsSpraying ? Mathf.Max(0f, -_current.Nozzle.forward.y) * (_recoilJump * _current.Supply) : 0f;

        // The jet never shoves a standing player; it only speeds up a step taken the way it is already pushing.
        public Vector3 RecoilPush(Vector3 wish)
        {
            if (!IsSpraying)
            {
                return Vector3.zero;
            }

            Vector3 back = Vector3.ProjectOnPlane(-_current.Nozzle.forward, Vector3.up).normalized;
            float along = Mathf.Max(0f, Vector3.Dot(wish.normalized, back));
            return back * (_recoilPush * _current.Supply * along);
        }

        public event Action<FireSource> FireHit;
        public event Action RanOut;

        public void Init(Transform aim, InventoryLogic items)
        {
            _aim = aim;
            _owner = aim.GetComponentInParent<CharacterController>().transform;
            _items = items;
            _path.Init();
            _items.ItemChanged += HandleItemChanged;
        }

        public void Dispose()
        {
            _items.ItemChanged -= HandleItemChanged;
        }

        public void Tick(bool wantsToSpray, float deltaTime)
        {
            if (_current == null)
            {
                return;
            }

            _current.AimAt(_aim.position + _aim.forward * _convergeDistance, deltaTime);
            bool isTaut = _current.TetherTension >= _tautTension;
            if (isTaut && !_wasTaut)
            {
                _current.Tug();
                RanOut?.Invoke();
            }

            _wasTaut = isTaut;
            float supply = _current.Supply;
            if (wantsToSpray && !_wasWanted && supply < _minSupply)
            {
                _current.Sputter();
            }

            _wasWanted = wantsToSpray;
            wantsToSpray &= supply >= _minSupply;
            if (wantsToSpray != IsSpraying)
            {
                IsSpraying = wantsToSpray;
                _current.SetSpraying(wantsToSpray);
                if (wantsToSpray)
                {
                    _reach = 0f;
                    _cut = 0f;
                }
            }

            float travel = _path.Speed * deltaTime;
            if (IsSpraying)
            {
                Transform nozzle = _current.Nozzle;
                _path.Trace(nozzle.position, nozzle.forward, supply, _owner);
                _reach = Mathf.Min(_reach + travel, _path.Length);
            }
            else
            {
                _cut = Mathf.Min(_cut + travel, _reach);
            }

            bool isLanded = _reach > _cut && _reach >= _path.Length;
            _current.Show(_path, _cut, _reach, isLanded, deltaTime);
            if (isLanded && IsSpraying)
            {
                HitFires(deltaTime);
                if (_path.HasImpact)
                {
                    _traces.Wet(_path.ImpactCollider, _path.ImpactPoint, _path.ImpactNormal, _flowRate * supply * deltaTime);
                    ToolImpact.Apply(_path.ImpactCollider,
                        new ToolHit(ToolHitKind.Water, _path.ImpactPoint, _path.ImpactDirection, _pushForce * supply * deltaTime));
                }
            }
        }

        public Vector3 LimitMove(Vector3 velocity)
        {
            if (_current == null || _current.TetherTension <= 0f)
            {
                return velocity;
            }

            Vector3 pull = Vector3.ProjectOnPlane(_current.TetherPull, Vector3.up).normalized;
            float away = -Vector3.Dot(velocity, pull);
            return away > 0f ? velocity + pull * (away * _current.TetherTension) : velocity;
        }

        private void HitFires(float deltaTime)
        {
            var fires = FireSource.Active;
            for (int i = fires.Count - 1; i >= 0; i--)
            {
                FireSource fire = fires[i];
                if (fire.IsOut)
                {
                    continue;
                }

                Vector3 centre = fire.transform.position + Vector3.up * _fireCentreHeight;
                float factor = Mathf.Max(SplashFactor(centre), Falloff(_path.DistanceTo(centre, _reach), _streamRadius));
                if (factor <= 0f)
                {
                    continue;
                }

                fire.Douse(_power * factor * _current.Supply, deltaTime);
                FireHit?.Invoke(fire);
            }
        }

        private float SplashFactor(Vector3 centre)
        {
            if (!_path.HasImpact)
            {
                return 0f;
            }

            float factor = Falloff(Vector3.Distance(centre, _path.ImpactPoint), _splashRadius);
            return factor > 0f && CanSplash(centre) ? factor : 0f;
        }

        private float Falloff(float distance, float radius) =>
            distance > radius ? 0f : Mathf.Lerp(1f, _edgePowerFactor, distance / radius);

        private bool CanSplash(Vector3 centre)
        {
            Vector3 from = _path.ImpactPoint + _path.ImpactNormal * _sightLift;
            Vector3 toFire = centre - from;
            float distance = toFire.magnitude;
            return distance <= _sightSlack
                || !Physics.Raycast(from, toFire / distance, out RaycastHit hit, distance - _sightSlack, _path.ObstacleMask, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<FireSource>() != null;
        }

        private void HandleItemChanged(ItemType type)
        {
            if (_current != null)
            {
                _current.SetSpraying(false);
                _current.Hide();
            }

            IsSpraying = false;
            _reach = 0f;
            _cut = 0f;
            ToolItem held = _items.HeldTool;
            _current = held != null ? held.GetComponent<HoseNozzleItem>() : null;
        }
    }
}
