using System;
using Gameplay.Entities.Interactive;
using Services;
using UnityEngine;

namespace Gameplay.Entities.Characters
{
    [Serializable]
    public sealed class CharacterReactionLogic
    {
        private const int PowderGroup = 0;
        private const int WaterGroup = 1;
        private const int StrikeGroup = 2;
        private const int GroupCount = 3;
        private const float MinLean = 0.001f;
        private const float MinDirectionSqr = 0.0001f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [Header("Coat")]
        [SerializeField] private Material _coatMaterial;
        [SerializeField] private Color _powderColor = new(0.95f, 0.95f, 0.97f);
        [SerializeField] private Color _wetColor = new(0.05f, 0.08f, 0.12f);
        [SerializeField, Range(0f, 1f)] private float _powderOpacity = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _wetOpacity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _wetSmoothness = 0.85f;
        [SerializeField, Min(0f)] private float _powderPerImpulse = 0.05f;
        [SerializeField, Min(0f)] private float _wetPerImpulse = 0.02f;
        [SerializeField, Min(0f)] private float _washPerImpulse = 0.03f;
        [SerializeField, Min(0.01f)] private float _dryTime = 25f;

        [Header("Flinch")]
        [SerializeField] private float _leanAngle = 16f;
        [SerializeField, Range(0f, 1f)] private float _powderLean = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _waterLean = 1f;
        [SerializeField, Range(0f, 1f)] private float _strikeLean = 0.8f;
        [SerializeField] private float _leanHold = 0.3f;
        [SerializeField] private float _leanSharpness = 8f;
        [SerializeField] private float _shakeAngle = 3f;
        [SerializeField] private float _shakeFrequency = 24f;

        [Header("Complaints")]
        [SerializeField, Min(0.01f)] private float _powderPatience = 4f;
        [SerializeField, Min(0.01f)] private float _waterPatience = 20f;
        [SerializeField] private float _cooldown = 2f;
        [SerializeField] private string[] _powderLines = Array.Empty<string>();
        [SerializeField] private string[] _waterLines = Array.Empty<string>();
        [SerializeField] private string[] _strikeLines = Array.Empty<string>();

        private readonly int[] _counts = new int[GroupCount];
        private Transform _spine;
        private Renderer[] _renderers;
        private Material[][] _plain;
        private Material[][] _coated;
        private MaterialPropertyBlock _block;
        private Vector3 _leanDirection = Vector3.forward;
        private float _lean;
        private float _leanGoal;
        private float _sinceHit = float.MaxValue;
        private float _sinceComplaint = float.MaxValue;
        private float _annoyance;
        private float _powder;
        private float _wet;
        private float _shownPowder;
        private float _shownWet;
        private bool _isCoated;
        private bool _holdsPose;
        private Quaternion _restRotation;
        private Quaternion _unleantRotation;
        private Quaternion _leantRotation;
        private bool _isLeant;

        public void Init(Animator animator, Renderer[] renderers) => Init(animator.GetBoneTransform(HumanBodyBones.Spine), renderers, false);

        public void Init(Transform spine, Renderer[] renderers, bool holdsPose)
        {
            _spine = spine;
            _holdsPose = holdsPose;
            _restRotation = spine.rotation;
            _renderers = renderers;
            _block = new MaterialPropertyBlock();
            _plain = new Material[renderers.Length][];
            _coated = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] plain = renderers[i].sharedMaterials;
                var coated = new Material[plain.Length + 1];
                plain.CopyTo(coated, 0);
                coated[plain.Length] = _coatMaterial;
                _plain[i] = plain;
                _coated[i] = coated;
            }
        }

        public bool Hit(in ToolHit hit, out string line)
        {
            line = null;
            Vector3 planar = Vector3.ProjectOnPlane(hit.Direction, Vector3.up);
            if (planar.sqrMagnitude > MinDirectionSqr)
            {
                _leanDirection = planar.normalized;
            }

            _sinceHit = 0f;
            int group;
            switch (hit.Kind)
            {
                case ToolHitKind.Powder:
                    group = PowderGroup;
                    _powder = Mathf.Min(1f, _powder + hit.Impulse * _powderPerImpulse);
                    _leanGoal = _powderLean;
                    _annoyance += hit.Impulse / _powderPatience;
                    break;
                case ToolHitKind.Water:
                    group = WaterGroup;
                    _wet = Mathf.Min(1f, _wet + hit.Impulse * _wetPerImpulse);
                    _powder = Mathf.Max(0f, _powder - hit.Impulse * _washPerImpulse);
                    _leanGoal = _waterLean;
                    _annoyance += hit.Impulse / _waterPatience;
                    break;
                default:
                    group = StrikeGroup;
                    _leanGoal = _strikeLean;
                    _lean = _strikeLean;
                    _annoyance = 1f;
                    break;
            }

            string[] lines = Lines(group);
            if (_annoyance < 1f || _sinceComplaint < _cooldown || lines.Length == 0)
            {
                return false;
            }

            _annoyance = 0f;
            _sinceComplaint = 0f;
            line = lines[Mathf.Min(_counts[group]++, lines.Length - 1)];
            return true;
        }

        public void Tick(float deltaTime, bool canLean)
        {
            _sinceHit += deltaTime;
            _sinceComplaint += deltaTime;
            if (_sinceHit > _leanHold)
            {
                _leanGoal = 0f;
                _annoyance = 0f;
            }

            _lean = Mathf.Lerp(_lean, _leanGoal, Easing.Damp(_leanSharpness, deltaTime));
            _wet = Mathf.Max(0f, _wet - deltaTime / _dryTime);
            ShowCoat();
            if (_isLeant && _spine.localRotation.Equals(_leantRotation))
            {
                _spine.localRotation = _unleantRotation;
            }

            Quaternion basis = _holdsPose ? _restRotation : _spine.rotation;
            _isLeant = canLean && _lean >= MinLean;
            if (!_isLeant)
            {
                _spine.rotation = basis;
                return;
            }

            Quaternion lean = Quaternion.AngleAxis(_leanAngle * _lean, Vector3.Cross(Vector3.up, _leanDirection));
            Quaternion shake = Quaternion.AngleAxis(Mathf.Sin(Time.time * _shakeFrequency) * _shakeAngle * _lean, Vector3.up);
            _unleantRotation = _spine.localRotation;
            _spine.rotation = lean * shake * basis;
            _leantRotation = _spine.localRotation;
        }

        private void ShowCoat()
        {
            float powder = _powder * _powderOpacity;
            float wet = _wet * _wetOpacity;
            if (Mathf.Approximately(powder, _shownPowder) && Mathf.Approximately(wet, _shownWet))
            {
                return;
            }

            _shownPowder = powder;
            _shownWet = wet;
            float opacity = Mathf.Max(powder, wet);
            bool isCoated = opacity > 0f;
            if (isCoated != _isCoated)
            {
                _isCoated = isCoated;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    _renderers[i].sharedMaterials = isCoated ? _coated[i] : _plain[i];
                }
            }

            if (!isCoated)
            {
                return;
            }

            float powderShare = powder / (powder + wet);
            Color color = Color.Lerp(_wetColor, _powderColor, powderShare);
            color.a = opacity;
            _block.SetColor(BaseColorId, color);
            _block.SetFloat(SmoothnessId, (1f - powderShare) * _wetSmoothness);
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].SetPropertyBlock(_block, _plain[i].Length);
            }
        }

        private string[] Lines(int group) => group switch
        {
            PowderGroup => _powderLines,
            WaterGroup => _waterLines,
            _ => _strikeLines,
        };
    }
}
