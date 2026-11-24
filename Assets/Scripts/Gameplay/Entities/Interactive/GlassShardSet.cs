using System;
using UnityEngine;

namespace Gameplay.Entities.Interactive
{
    public sealed class GlassShardSet : MonoBehaviour, IChoppable
    {
        [SerializeField] private GlassShard[] _shards = Array.Empty<GlassShard>();

        private BreakableGlass _owner;

        public GlassShard[] Shards => _shards;

        public void Bind(BreakableGlass owner) => _owner = owner;

        public void Chop(Vector3 point, Vector3 direction) => _owner.Chop(point, direction);
    }
}
