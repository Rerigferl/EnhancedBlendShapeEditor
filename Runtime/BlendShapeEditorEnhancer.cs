using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("numeira.enhanced-blendshape-editor.harmony")]

namespace Numeira
{
    [AddComponentMenu("Miscellaneous/BlendShape Editor Enhancer")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class BlendShapeEditorEnhancer : MonoBehaviour,
#if VRC_SDK_BASE
        VRC.SDKBase.IEditorOnly
#endif
    {
        [HideInInspector]
        public bool Initialized = false;
        [HideInInspector]
        public string FaceBlendShapeDelimiter = "[-=]{2,}";
        [HideInInspector]
        [Range(16, 64)]
        public int DisplayCount = 32;
        [HideInInspector]
        public bool DisableAlternativeEditor;
        [HideInInspector]
        public bool ShowNonZeroValueOnly;
        [HideInInspector]
        public string Search;

        // Non-serialized, fields for Editor class
#if UNITY_EDITOR
        [NonSerialized]
        internal SerializedObject SerializedObject;
        [NonSerialized]
        internal SerializedProperty FaceBlendShapeDelimiterProperty;
        [NonSerialized]
        internal SerializedProperty DisplayCountProperty;
        [NonSerialized]
        internal SerializedProperty DisableAlternativeEditorProperty;
        [NonSerialized]
        internal SerializedProperty ShowNonZeroValueOnlyProperty;
        [NonSerialized]
        internal SerializedProperty SearchProperty;
        [NonSerialized]
        internal (string Key, BlendShapeData[] Array)[] CategorizedBlendShapes;
        [NonSerialized]
        internal Dictionary<string, FolderStatus> FolderStatus = new();
        [NonSerialized]
        internal Mesh PreviousSharedMesh;
#endif
    }

    internal struct BlendShapeData
    {
        public int Index;
        public string Name;
        public (float Min, float Max) Weights;
    }

    internal class FolderStatus
    {
        public bool IsExpanded;
        public Vector2 ScrollPosition;

        public static implicit operator FolderStatus(ValueTuple<bool, Vector2> tuple) => new() { IsExpanded = tuple.Item1, ScrollPosition = tuple.Item2 };

        public void Deconstruct(out bool isExpanded, out Vector2 scrollPosition) => (isExpanded, scrollPosition) = (IsExpanded, ScrollPosition);
    }
}
