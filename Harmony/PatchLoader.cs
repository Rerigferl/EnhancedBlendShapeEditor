using HarmonyLib;
using UnityEditor;

namespace Numeira
{
    [InitializeOnLoad]
    internal static partial class PatchLoader
    {
        static PatchLoader()
        {
            var harmony = new Harmony("numeira.improve-blendshape-list");

            SkinnedMeshRendererEditorPatcher.Patch(harmony);
        }
    }
}
