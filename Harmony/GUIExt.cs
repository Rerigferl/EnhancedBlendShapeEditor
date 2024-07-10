using UnityEditor;
using UnityEngine;

namespace Numeira
{
    internal static class GUIExt
    {
        private static readonly GUIContent sharedGUIContent = new GUIContent();

        public static GUIContent ToGUIContent(this string value, string tooltip = null)
        {
            sharedGUIContent.text = value;
            sharedGUIContent.tooltip = tooltip;
            return sharedGUIContent;
        }

        public static void DrawSeparator()
        {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight / 2));
            rect.y += rect.height / 2;
            rect.height = 1;
            var color = EditorStyles.label.normal.textColor;
            color.a = 0.25f;
            EditorGUI.DrawRect(rect, color);
        }
    }

    internal readonly ref struct Indent
    {
        private readonly int value;

        public Indent(int value)
        {
            this.value = value;
            EditorGUI.indentLevel += value;
        }

        public void Dispose() => EditorGUI.indentLevel -= value;

        public static Indent Increment() => new(1);
        public static Indent Decrement() => new(-1);
    }
}
