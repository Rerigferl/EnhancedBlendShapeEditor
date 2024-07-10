using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Numeira
{
    internal static class SkinnedMeshRendererEditorPatcher
    {
        private const string FaceObjectName = "Body";

        public static void Patch(Harmony harmony)
        {
            var smrEditor = typeof(Editor).Assembly.GetType("UnityEditor.SkinnedMeshRendererEditor");
            if (smrEditor is null)
                return;

            PatchMethod(nameof(OnBlendShapeUI), smrEditor, harmony, HarmonyPatchType.Prefix);
            PatchMethod(nameof(OnEnable), smrEditor, harmony, HarmonyPatchType.Postfix);
        }

        private static void PatchMethod(string methodName, Type type, Harmony harmony, HarmonyPatchType patchType = HarmonyPatchType.Prefix)
        {
            var original = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (original is null)
                return;

            var patch = new HarmonyMethod(typeof(SkinnedMeshRendererEditorPatcher).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static));
            harmony.Patch(original,
                prefix: patchType is HarmonyPatchType.Prefix ? patch : null,
                postfix: patchType is HarmonyPatchType.Postfix ? patch : null,
                transpiler: patchType is HarmonyPatchType.Transpiler ? patch : null,
                finalizer: patchType is HarmonyPatchType.Finalizer ? patch : null);
        }

        public static void OnEnable(Editor __instance, SerializedProperty ___m_BlendShapeWeights)
        {
            var target = __instance.target as SkinnedMeshRenderer;
            if (!target.TryGetComponent<BlendShapeEditorEnhancer>(out var @this))
                return;

            var so = @this.SerializedObject = new(@this);
            @this.FaceBlendShapeDelimiterProperty = so.FindProperty(nameof(BlendShapeEditorEnhancer.FaceBlendShapeDelimiter));
            @this.SearchProperty = so.FindProperty(nameof(BlendShapeEditorEnhancer.Search));
            @this.DisplayCountProperty = so.FindProperty(nameof(BlendShapeEditorEnhancer.DisplayCount));
            @this.ShowNonZeroValueOnlyProperty = so.FindProperty(nameof(BlendShapeEditorEnhancer.ShowNonZeroValueOnly));
            @this.CategorizedBlendShapes = GetCategorizedBlendShapes(target.sharedMesh, @this, ___m_BlendShapeWeights).Select(x => (x.Key, x.ToArray())).ToArray();
            if (@this.CategorizedBlendShapes.Length == 1 && !string.IsNullOrEmpty(@this.FaceBlendShapeDelimiter))
                @this.FaceBlendShapeDelimiter = "";
            @this.FolderStatus.Clear();
        }

        public static bool OnBlendShapeUI(Editor __instance, SerializedProperty ___m_BlendShapeWeights)
        {
            if (__instance.targets.Length != 1 
                || (__instance.target as SkinnedMeshRenderer)?.TryGetComponent<BlendShapeEditorEnhancer>(out var e) != true 
                || e.DisableAlternativeEditor)
                return true; // Do nothing

            if (e.SerializedObject == null)
                OnEnable(__instance, ___m_BlendShapeWeights);

            OnBlendShapeGUIInternal(e, __instance, ___m_BlendShapeWeights);

            return false; // Prevent original method
        }

        private static void OnBlendShapeGUIInternal(BlendShapeEditorEnhancer @this, Editor instance, SerializedProperty m_BlendShapeWeights)
        {
            var so = instance.serializedObject;
            var smr = instance.target as SkinnedMeshRenderer;
            var mesh = smr.sharedMesh;
            var blendShapeCount = mesh.blendShapeCount;

            if (blendShapeCount == 0)
                return;

            EditorGUILayout.PropertyField(m_BlendShapeWeights, "BlendShapes".ToGUIContent(), false);
            if (!m_BlendShapeWeights.isExpanded)
                return;

            using var __indent = Indent.Increment();

            @this.SerializedObject.Update();

            bool optionsHasChanged = false;
            if (@this.FaceBlendShapeDelimiterProperty.isExpanded = EditorGUILayout.Foldout(@this.FaceBlendShapeDelimiterProperty.isExpanded, "Options"))
            {
                using var __indent_ = Indent.Increment();
                EditorGUILayout.PropertyField(@this.DisplayCountProperty);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(@this.ShowNonZeroValueOnlyProperty);
                EditorGUILayout.PropertyField(@this.FaceBlendShapeDelimiterProperty, "Delimiter".ToGUIContent());
                optionsHasChanged |= EditorGUI.EndChangeCheck();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(@this.SearchProperty);
            optionsHasChanged |= EditorGUI.EndChangeCheck();

            @this.SerializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (optionsHasChanged)
            {
                @this.CategorizedBlendShapes = GetCategorizedBlendShapes(mesh, @this, m_BlendShapeWeights).Select(x => (x.Key, x.ToArray())).ToArray();
            }


            GUIExt.DrawSeparator();

            if (@this.CategorizedBlendShapes.Length == 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Blendshapes not found!");
                EditorGUILayout.Space();
                GUIExt.DrawSeparator();
                return;
            }

            int displayCount = @this.DisplayCount;
            var delimiter = @this.FaceBlendShapeDelimiter;
            const float LineHeight = 20;
            int arraySize = m_BlendShapeWeights.arraySize;
            bool nonZeroValueOnly = @this.ShowNonZeroValueOnly;

            foreach (var category in @this.CategorizedBlendShapes)
            {
                var (isOpen, scrollPosition) = @this.FolderStatus.GetOrAdd(category.Key, _ => (false, Vector2.zero));
                Indent indent = default;
                if (@this.CategorizedBlendShapes.Length != 1 || @this.CategorizedBlendShapes[0].Key != "")
                {
                    var _isOpen = EditorGUILayout.Foldout(isOpen, string.IsNullOrEmpty(category.Key) ? "Uncategorized" : category.Key);
                    if (_isOpen != isOpen)
                    {
                        @this.FolderStatus[category.Key] = (_isOpen, scrollPosition);
                        isOpen = _isOpen;
                    }

                    if (!isOpen)
                        continue;

                    indent = Indent.Increment();
                }

                if (category.Array.Length > displayCount)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(LineHeight * Math.Min(displayCount, category.Array.Length)));
                    scrollPosition = new Vector2(0, Mathf.Round(scrollPosition.y / LineHeight) * LineHeight);
                }

                var start = Mathf.RoundToInt(scrollPosition.y / LineHeight) - 1;
                var end = start + displayCount + 2;

                for (int i = 0; i < category.Array.Length; i++)
                {
                    var data = category.Array[i];
                    if ((uint)(i - start) > (end - start))
                    {
                        EditorGUILayout.Space(LineHeight);
                        continue;
                    }

                    var name = data.Name;
                    if (data.Index < arraySize)
                    {
                        EditorGUILayout.Slider(m_BlendShapeWeights.GetArrayElementAtIndex(data.Index), data.Weights.Min, data.Weights.Max, name.ToGUIContent());
                    }
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        float value = EditorGUILayout.Slider(name.ToGUIContent(), 0f, data.Weights.Min, data.Weights.Max);
                        if (EditorGUI.EndChangeCheck())
                        {
                            m_BlendShapeWeights.arraySize = blendShapeCount;
                            arraySize = blendShapeCount;
                            m_BlendShapeWeights.GetArrayElementAtIndex(data.Index).floatValue = value;
                        }
                    }
                }

                if (category.Array.Length > displayCount)
                {
                    EditorGUILayout.EndScrollView();
                    @this.FolderStatus[category.Key] = (isOpen, scrollPosition);
                }

                indent.Dispose();
            }

            GUIExt.DrawSeparator();

        }

        private static IEnumerable<IGrouping<string, BlendShapeData>> GetCategorizedBlendShapes(Mesh mesh, BlendShapeEditorEnhancer @this, SerializedProperty blendShapeWeights)
        {
            Regex search;
            Regex delimiter;
            try
            {
                search = string.IsNullOrEmpty(@this.Search) ? null : new Regex(@this.Search, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
            }
            catch
            {
                search = null;
            }
            try
            {
                delimiter = string.IsNullOrEmpty(@this.FaceBlendShapeDelimiter) ? null : new Regex(@this.FaceBlendShapeDelimiter, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
            }
            catch
            {
                delimiter = null;
            }

            return WithGroup().GroupBy(x => x.Item1, x => x.Item2);

            IEnumerable<(string, BlendShapeData)> WithGroup()
            {
                var current = delimiter is null ? "" : "Uncategorized";
                int count = mesh.blendShapeCount;
                var weightCount = blendShapeWeights?.arraySize ?? 0;

                for (int i = 0; i < count; i++)
                {
                    var name = mesh.GetBlendShapeName(i);

                    //if (name.Contains(@this.FaceBlendShapeDelimiter, StringComparison.OrdinalIgnoreCase))
                    if (delimiter?.IsMatch(name) ?? false)
                    {
                        current = delimiter.Replace(name, "");
                        //current = name.Replace(@this.FaceBlendShapeDelimiter, "");
                        continue;
                    }

                    if (@this.ShowNonZeroValueOnly)
                    {
                        var weight = i >= weightCount ? 0 : blendShapeWeights.GetArrayElementAtIndex(i)?.floatValue ?? 0;
                        if (weight == 0)
                            continue;
                    }

                    if (search is not null && !search.IsMatch(name))
                        continue;

                    yield return (current, new()
                    {
                        Index = i,
                        Name = name,
                        Weights = GetBlendShapeWeights(i),
                    });
                }
            }

            (float Min, float Max) GetBlendShapeWeights(int index)
            {
                float min = 0f, max = 0f;

                int frameCount = mesh.GetBlendShapeFrameCount(index);
                for (int j = 0; j < frameCount; j++)
                {
                    float frameWeight = mesh.GetBlendShapeFrameWeight(index, j);
                    min = Mathf.Min(frameWeight, min);
                    max = Mathf.Max(frameWeight, max);
                }

                return (min, max);
            }
        }

        private const string MenuPath = "CONTEXT/SkinnedMeshRenderer/Toggle blendshape editor mode";

        [MenuItem(MenuPath, priority = 301)]
        public static void ToggleEditorMode(MenuCommand command)
        {
            var context = command.context as SkinnedMeshRenderer;
            if (!context.TryGetComponent<BlendShapeEditorEnhancer>(out var c))
            {
                context.gameObject.AddComponent<BlendShapeEditorEnhancer>();
                return;
            }

            c.DisableAlternativeEditor = !c.DisableAlternativeEditor;
        }
    }
}
