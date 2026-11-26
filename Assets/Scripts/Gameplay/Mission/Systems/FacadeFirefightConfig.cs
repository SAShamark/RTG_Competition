using Gameplay.Dialogue;
using Gameplay.Entities.Environment;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Mission.Systems
{
    [CreateAssetMenu(fileName = "FacadeFirefightConfig", menuName = "Configs/Facade Firefight Config")]
    public sealed class FacadeFirefightConfig : ScriptableObject
    {
        [Header("Progress bar")]
        [SerializeField, Range(0f, 1f)] private float _shownAtStart = 0.01f;
        [SerializeField, Range(0f, 1f)] private float _shownAtEnd = 0.99f;
        [SerializeField, Range(0.01f, 1f)] private float _completeShare = 0.9f;
        [SerializeField] private float _finishDelay = 1.2f;

        [Header("Fire out chime")]
        [SerializeField] private SoundType _fireOutSound = SoundType.UiConfirm;
        [SerializeField] private float _basePitch = 0.8f;
        [SerializeField] private float _pitchStep = 0.04f;
        [SerializeField] private float _maxPitch = 1.6f;

        [Header("Texts")]
        [SerializeField] private TextId _objective = TextId.ObjectiveSprayFacade;
        [SerializeField] private TextId _takeObjective = TextId.ObjectiveTakeHose;
        [SerializeField] private TextId _connectObjective = TextId.ObjectiveConnectHose;
        [SerializeField] private TextId _valveObjective = TextId.ObjectiveOpenValve;
        [SerializeField] private TextId _lockerLabel = TextId.NavHose;
        [SerializeField] private TextId _portLabel = TextId.NavCoupling;
        [SerializeField] private TextId _valveLabel = TextId.NavValve;
        [SerializeField] private TextId _doneToast = TextId.ToastFacadeOut;

        public float ShownAtStart => _shownAtStart;
        public float ShownAtEnd => _shownAtEnd;
        public float CompleteShare => _completeShare;
        public float FinishDelay => _finishDelay;
        public SoundType FireOutSound => _fireOutSound;
        public float BasePitch => _basePitch;
        public float PitchStep => _pitchStep;
        public float MaxPitch => _maxPitch;

        public TextId ObjectiveFor(HoseStep step) => step switch
        {
            HoseStep.TakeHose => _takeObjective,
            HoseStep.Connect => _connectObjective,
            HoseStep.OpenValve => _valveObjective,
            _ => _objective,
        };

        public TextId LabelFor(HoseStep step) => step switch
        {
            HoseStep.TakeHose => _lockerLabel,
            HoseStep.Connect => _portLabel,
            _ => _valveLabel,
        };

        public TextId DoneToast => _doneToast;
    }
}
