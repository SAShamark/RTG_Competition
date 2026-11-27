using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Entities.Characters.Player
{
    [Serializable]
    public sealed class TargetHighlightLogic
    {
        [SerializeField] private Material _material;

        private readonly List<Renderer> _renderers = new();
        private readonly List<Material> _materials = new();

        public TargetHighlightLogic()
        {
        }

        public TargetHighlightLogic(Material material) => _material = material;

        public void Set(Component target)
        {
            Clear();
            if (target == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>())
            {
                if (targetRenderer is not MeshRenderer and not SkinnedMeshRenderer)
                {
                    continue;
                }

                targetRenderer.GetSharedMaterials(_materials);
                _materials.Add(_material);
                targetRenderer.SetSharedMaterials(_materials);
                _renderers.Add(targetRenderer);
            }
        }

        public void Clear()
        {
            foreach (Renderer targetRenderer in _renderers)
            {
                // A highlighted door or window may have been smashed and destroyed while glowing.
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetSharedMaterials(_materials);
                _materials.Remove(_material);
                targetRenderer.SetSharedMaterials(_materials);
            }

            _renderers.Clear();
        }
    }
}
