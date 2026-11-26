using System;
using Gameplay.Entities.Characters;
using UnityEngine;

namespace UI.Popups.Variants.Results
{
    [Serializable]
    public sealed class ResultsPhotoRig
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private GameObject _set;
        [SerializeField] private CharacterControl _actor;

        public Camera Camera => _camera;
        public GameObject Set => _set;
        public CharacterControl Actor => _actor;
    }
}
