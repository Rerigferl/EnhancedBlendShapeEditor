using UnityEngine;

namespace Numeira
{
    [AddComponentMenu("Miscellaneous/BlendShape Editor Enhancer")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class ExtendDataHolder : MonoBehaviour,
#if VRC_SDK_BASE
        VRC.SDKBase.IEditorOnly
#endif
    {
        public string FaceBlendShapeDelimiter;
        [Range(16, 64)]
        public int DisplayCount = 32;
        public bool DisableAlternativeEditor;
        public bool ShowNonZeroValueOnly;
        public string Search;
    }
}
