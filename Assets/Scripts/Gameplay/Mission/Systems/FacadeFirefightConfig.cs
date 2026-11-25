using Gameplay.Dialogue;
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
        [SerializeField, Range(0f, 1f)] private float _completeShare = 1f;
        [SerializeField] private float _finishDelay = 1.2f;

        [Header("Fire out chime")]
        [SerializeField] private SoundType _fireOutSound = SoundType.UiConfirm;
        [SerializeField] private float _basePitch = 0.8f;
        [SerializeField] private float _pitchStep = 0.04f;
        [SerializeField] private float _maxPitch = 1.6f;

        [Header("Texts")]
        [SerializeField] private TextId _objective = TextId.ObjectivePutOutFacade;
        [SerializeField] private TextId _doneToast = TextId.ToastFacadeOut;

        public float ShownAtStart => _shownAtStart;
        public float ShownAtEnd => _shownAtEnd;
        public float CompleteShare => _completeShare;
        public float FinishDelay => _finishDelay;
        public SoundType FireOutSound => _fireOutSound;
        public float BasePitch => _basePitch;
        public float PitchStep => _pitchStep;
        public float MaxPitch => _maxPitch;
        public TextId Objective => _objective;
        public TextId DoneToast => _doneToast;
    }
}
