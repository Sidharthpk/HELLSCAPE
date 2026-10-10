using System;
using UnityEngine;

namespace KesslerVane
{
    public sealed class OfficeStoryState : MonoBehaviour
    {
        [Serializable] public sealed class Binding
        {
            public Renderer renderer;
            public Material[] clean;
            public Material[] burned;
        }
        public Binding[] bindings = Array.Empty<Binding>();
        [SerializeField] private bool burned;
        public bool IsBurned => burned;
        public void SetBurned(bool value)
        {
            burned = value;
            foreach (var binding in bindings)
                if (binding.renderer != null)
                    binding.renderer.sharedMaterials = value ? binding.burned : binding.clean;
        }
        [ContextMenu("Show clean office")] public void ShowClean() => SetBurned(false);
        [ContextMenu("Show burned office")] public void ShowBurned() => SetBurned(true);
    }
}
