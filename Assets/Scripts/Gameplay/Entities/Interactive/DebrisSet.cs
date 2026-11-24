using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class DebrisSet : MonoBehaviour, IChoppable
    {
        [SerializeField] private DebrisPiece[] _pieces = Array.Empty<DebrisPiece>();

        private IChoppable _owner;

        public DebrisPiece[] Pieces => _pieces;

        public void Bind(IChoppable owner) => _owner = owner;

        public void Chop(Vector3 point, Vector3 direction) => _owner?.Chop(point, direction);
    }
}
