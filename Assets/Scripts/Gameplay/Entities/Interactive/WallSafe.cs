using System;
using Services;
using Services.Audio;
using Services.Motion;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class WallSafe : MonoBehaviour
    {
        private const int CodeLength = 4;
        private const int Digits = 10;

        [SerializeField] private Transform _doorHinge;
        [SerializeField] private Vector3 _axis = Vector3.up;
        [SerializeField] private float _openAngle = 110f;
        [SerializeField] private float _duration = 0.8f;
        [SerializeField] private DocumentsStash _documents;
        [SerializeField] private TextMesh _note;
        [SerializeField] private string _notePrefix = "Код сейфа: ";

        private Quaternion _closedRotation;

        public string Code { get; private set; }
        public bool IsUnlocked { get; private set; }
        public bool IsOpen { get; private set; }

        public event Action<WallSafe> Opened;

        public void Unlock()
        {
            if (IsUnlocked)
            {
                return;
            }

            IsUnlocked = true;
            AudioService.Instance.PlayAt(SoundType.SafeSolved, transform.position);
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            MotionService.Instance.Play(_duration, k => _doorHinge.localRotation = _closedRotation * Quaternion.AngleAxis(_openAngle * Easing.OutCubic(k), _axis));
            AudioService.Instance.PlayAt(SoundType.SafeDoorOpen, transform.position);

            _documents.Unlock();

            Opened?.Invoke(this);
        }

        private void Awake()
        {
            _closedRotation = _doorHinge.localRotation;
            Code = string.Empty;
            for (int i = 0; i < CodeLength; i++)
            {
                Code += UnityEngine.Random.Range(0, Digits).ToString();
            }

            _note.text = _notePrefix + Code;
        }
    }
}
