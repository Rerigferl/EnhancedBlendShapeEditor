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

        private static Editor currentEditor;
        private static SerializedObject extendData;
        private static SerializedProperty faceBlendShapeDelimiterProperty;
        private static SerializedProperty searchProperty;
        private static SerializedProperty displayCountProperty;
        private static SerializedProperty showNonZeroValueProperty;
        private static Vector2 scrollPosition;

        private static (string Key, BlendShapeData[] Array)[] categorizedBlendShapes;
        private static (string Key, BlendShapeData[] Array)[] searchedCategorizedBlendshapes;
        private static Dictionary<string, (bool IsExpanded, Vector2 ScrollPosition)> folderStatus = new();

        public static void OnEnable(Editor __instance)
        {
            var target = __instance.target as SkinnedMeshRenderer;
            //if (target.name is not FaceObjectName)
            //    return;

            if (!target.TryGetComponent<ExtendDataHolder>(out var extendDataHolder))
            {
                //extendDataHolder = target.gameObject.AddComponent<ExtendDataHolder>();
                //extendDataHolder.hideFlags = HideFlags.HideInInspector;
                return;
            }
            currentEditor = __instance;
            extendData = new(extendDataHolder);
            faceBlendShapeDelimiterProperty = extendData.FindProperty(nameof(ExtendDataHolder.FaceBlendShapeDelimiter));
            searchProperty = extendData.FindProperty(nameof(ExtendDataHolder.Search));
            displayCountProperty = extendData.FindProperty(nameof(ExtendDataHolder.DisplayCount));
            showNonZeroValueProperty = extendData.FindProperty(nameof(ExtendDataHolder.ShowNonZeroValueOnly));
            scrollPosition = Vector2.zero;
            categorizedBlendShapes = GetCategorizedBlendShapes(target.sharedMesh, faceBlendShapeDelimiterProperty.stringValue, searchProperty.stringValue).Select(x => (x.Key, x.ToArray())).ToArray();
            folderStatus.Clear();
        }

        public static bool OnBlendShapeUI(Editor __instance, ref SerializedProperty ___m_BlendShapeWeights)
        {
            //if (__instance.targets.Length != 1 || __instance.target.name is not FaceObjectName)
            if ((__instance.target as SkinnedMeshRenderer)?.TryGetComponent<ExtendDataHolder>(out var e) != true || e.DisableAlternativeEditor)
                return true; // Do nothing

            if (currentEditor != __instance)
                OnEnable(__instance);

            OnBlendShapeGUIInternal(__instance, ___m_BlendShapeWeights);

            return false; // Prevent original method
        }

        private static void OnBlendShapeGUIInternal(Editor @this, SerializedProperty m_BlendShapeWeights)
        {
            var so = @this.serializedObject;
            var smr = @this.target as SkinnedMeshRenderer;
            var mesh = smr.sharedMesh;
            var blendShapeCount = mesh.blendShapeCount;

            if (blendShapeCount == 0)
                return;

            EditorGUILayout.PropertyField(m_BlendShapeWeights, "BlendShapes".ToGUIContent(), false);
            if (!m_BlendShapeWeights.isExpanded)
                return;

            using var __indent = Indent.Increment();

            extendData.Update();

            bool optionsHasChanged = false;
            if (faceBlendShapeDelimiterProperty.isExpanded = EditorGUILayout.Foldout(faceBlendShapeDelimiterProperty.isExpanded, "Options"))
            {
                using var __indent_ = Indent.Increment();
                EditorGUILayout.PropertyField(displayCountProperty);
                EditorGUILayout.PropertyField(showNonZeroValueProperty);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(faceBlendShapeDelimiterProperty, "Delimiter".ToGUIContent());
                optionsHasChanged |= EditorGUI.EndChangeCheck();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(searchProperty);
            optionsHasChanged |= EditorGUI.EndChangeCheck();
            if (optionsHasChanged)
            {
                categorizedBlendShapes = GetCategorizedBlendShapes(mesh, faceBlendShapeDelimiterProperty.stringValue, searchProperty.stringValue).Select(x => (x.Key, x.ToArray())).ToArray();
            }

            extendData.ApplyModifiedProperties();

            GUIExt.DrawSeparator();

            if (categorizedBlendShapes.Length == 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Blendshapes not found!");
                EditorGUILayout.Space();
                GUIExt.DrawSeparator();
                return;
            }

            int displayCount = displayCountProperty.intValue;
            var delimiter = faceBlendShapeDelimiterProperty.stringValue;
            const float LineHeight = 20;
            int arraySize = m_BlendShapeWeights.arraySize;
            bool nonZeroValueOnly = showNonZeroValueProperty.boolValue;

            foreach (var c in categorizedBlendShapes)
            {
                var (isOpen, scrollPosition) = folderStatus.GetOrAdd(c.Key, _ => (false, Vector2.zero));
                Indent indent = default;
                if (categorizedBlendShapes.Length != 1 || categorizedBlendShapes[0].Key != "")
                {
                    var _isOpen = EditorGUILayout.Foldout(isOpen, string.IsNullOrEmpty(c.Key) ? "Uncategorized" : c.Key);
                    if (_isOpen != isOpen)
                    {
                        folderStatus[c.Key] = (_isOpen, scrollPosition);
                        isOpen = _isOpen;
                    }

                    if (!isOpen)
                        continue;

                    indent = Indent.Increment();
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(LineHeight * Math.Min(displayCount, c.Array.Length)));
                scrollPosition = new Vector2(0, Mathf.Ceil(scrollPosition.y / LineHeight) * LineHeight);

                var start = Mathf.RoundToInt(scrollPosition.y / LineHeight) - 1;
                var end = start + displayCount + 2;

                int displayed = 0;

                foreach (var data in c.Array)
                {
                    if ((uint)(displayed - start) > (end - start))
                    {
                        EditorGUILayout.Space(LineHeight);
                        displayed++;
                        continue;
                    }

                    var name = data.Name;
                    var weight = m_BlendShapeWeights.GetArrayElementAtIndex(data.Index);
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
                    displayed++;
                }
                EditorGUILayout.EndScrollView();
                indent.Dispose();
                folderStatus[c.Key] = (isOpen, scrollPosition);
            }

            GUIExt.DrawSeparator();

        }

        private static IEnumerable<IGrouping<string, BlendShapeData>> GetCategorizedBlendShapes(Mesh mesh, string delimiter, string search = null)
        {
            Regex regex;
            try
            {
                regex = string.IsNullOrEmpty(search) ? null : new Regex(search, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
            }
            catch
            {
                regex = null;
            }
            if (string.IsNullOrEmpty(delimiter))
            {
                var a = Enumerable.Range(0, mesh.blendShapeCount).Select(i =>
                {
                    return new BlendShapeData()
                    {
                        Index = i,
                        Name = mesh.GetBlendShapeName(i),
                        Weights = GetBlendShapeWeights(i),
                    };
                });
                if (regex is not null)
                {
                    a = a.Where(x => regex.IsMatch(x.Name));
                }
                return a.GroupBy(x => "");
            }

            return WithGroup().GroupBy(x => x.Item1, x => x.Item2);

            IEnumerable<(string, BlendShapeData)> WithGroup()
            {
                var current = "Uncategorized";
                int count = mesh.blendShapeCount;

                for (int i = 0; i < count; i++)
                {
                    var name = mesh.GetBlendShapeName(i);

                    if (name.Contains(delimiter, StringComparison.OrdinalIgnoreCase))
                    {
                        current = name.Replace(delimiter, "");
                        continue;
                    }

                    if (regex is not null && !regex.IsMatch(name))
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

        private struct BlendShapeData
        {
            public int Index;
            public string Name;
            public (float Min, float Max) Weights;
        }

        private const string MenuPath = "CONTEXT/SkinnedMeshRenderer/Toggle blendshape editor mode";

        [MenuItem(MenuPath, false, 301)]
        public static void ToggleEditorMode(MenuCommand command)
        {
            var extendData = (command.context as SkinnedMeshRenderer).GetComponent<ExtendDataHolder>();
            extendData.DisableAlternativeEditor = !extendData.DisableAlternativeEditor;
        }

        [MenuItem(MenuPath, true, 301)]
        public static bool ToggleEditorModeValidator(MenuCommand command) => (command.context is SkinnedMeshRenderer smr) && smr.TryGetComponent<ExtendDataHolder>(out _);
    }
}
