using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace Numeira
{
    [InitializeOnLoad]
    internal static partial class PatchLoader
    {
        static PatchLoader()
        {
            var harmony = new Harmony("numeira.enhanced-blendshape-editor");

            SkinnedMeshRendererEditorPatcher.Patch(harmony);
            
        }
    }
}
