using Gameplay.Entities.Residents;
using System;
using UnityEngine;

namespace UI.Popups.Variants.Results
{
    [Serializable]
    public sealed class ResultsPhotoRig
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private GameObject _set;
        [SerializeField] private PosedActor _actor;

        public Camera Camera => _camera;
        public GameObject Set => _set;
        public PosedActor Actor => _actor;
    }
}
