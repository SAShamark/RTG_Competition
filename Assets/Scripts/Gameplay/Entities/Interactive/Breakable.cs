using System;
using Services.Audio;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class Breakable : MonoBehaviour, IChoppable
    {
        [SerializeField, Min(1)] private int _hitsToBreak = 1;
        [SerializeField] private GameObject[] _intactParts = Array.Empty<GameObject>();
        [SerializeField] private Collider[] _intactColliders = Array.Empty<Collider>();
        [SerializeField] private Rigidbody[] _pieces = Array.Empty<Rigidbody>();

        [Header("Feedback")]
        [SerializeField] private GameObject _breakEffect;
        [SerializeField] private float _effectLifetime = 3f;
        [SerializeField] private SoundType _breakSound = SoundType.BlockerSmash;
        [SerializeField] private float _breakPitch = 1f;
        [SerializeField] private float _pieceImpulse = 2.5f;
        [SerializeField] private float _pieceScatter = 1.2f;
        [SerializeField] private float _pieceTorque = 3f;

        private int _hits;
        private Vector3[] _piecePositions;
        private Quaternion[] _pieceRotations;

        public bool IsBroken { get; private set; }

        public event Action<Breakable> Broken;

        private void Awake()
        {
            _piecePositions = new Vector3[_pieces.Length];
            _pieceRotations = new Quaternion[_pieces.Length];
            for (int i = 0; i < _pieces.Length; i++)
            {
                _piecePositions[i] = _pieces[i].transform.localPosition;
                _pieceRotations[i] = _pieces[i].transform.localRotation;
                SetPiecePhysics(_pieces[i], false);
            }
        }

        public void Restore()
        {
            IsBroken = false;
            _hits = 0;
            foreach (GameObject part in _intactParts)
            {
                part.SetActive(true);
            }

            foreach (Collider intact in _intactColliders)
            {
                intact.enabled = true;
            }

            for (int i = 0; i < _pieces.Length; i++)
            {
                SetPiecePhysics(_pieces[i], false);
                _pieces[i].transform.SetLocalPositionAndRotation(_piecePositions[i], _pieceRotations[i]);
                _pieces[i].position = _pieces[i].transform.position;
                _pieces[i].rotation = _pieces[i].transform.rotation;
            }
        }

        public void Chop(Vector3 point, Vector3 direction)
        {
            if (IsBroken || ++_hits < _hitsToBreak)
            {
                return;
            }

            IsBroken = true;
            foreach (GameObject part in _intactParts)
            {
                part.SetActive(false);
            }

            foreach (Collider intact in _intactColliders)
            {
                intact.enabled = false;
            }

            foreach (Rigidbody piece in _pieces)
            {
                SetPiecePhysics(piece, true);
                Vector3 scatter = UnityEngine.Random.insideUnitSphere * _pieceScatter;
                piece.AddForceAtPosition((direction * _pieceImpulse) + scatter, point, ForceMode.Impulse);
                piece.AddTorque(UnityEngine.Random.insideUnitSphere * _pieceTorque, ForceMode.Impulse);
            }

            AudioService.Instance.PlayAt(_breakSound, point, _breakPitch);
            if (_breakEffect != null)
            {
                GameObject effect = Instantiate(_breakEffect, point, Quaternion.LookRotation(-direction));
                Destroy(effect, _effectLifetime);
            }

            Broken?.Invoke(this);
        }

        private static void SetPiecePhysics(Rigidbody piece, bool isSimulated)
        {
            piece.isKinematic = !isSimulated;
            foreach (Collider pieceCollider in piece.GetComponents<Collider>())
            {
                pieceCollider.enabled = isSimulated;
            }
        }
    }
}
