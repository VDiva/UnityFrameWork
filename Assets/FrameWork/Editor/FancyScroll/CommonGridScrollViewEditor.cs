#if UNITY_EDITOR
using FancyScrollView;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CommonGridScrollView))]
public sealed class CommonGridScrollViewEditor : Editor
{
    private CommonGridScrollView Grid => (CommonGridScrollView)target;

    private void OnEnable()
    {
        EditorApplication.delayCall += RefreshPreview;
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= RefreshPreview;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script", "spacing", "startAxisSpacing");
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Grid Preview"))
            Grid.RefreshEditorPreview();
        if (GUILayout.Button("Clear Grid Preview"))
            Grid.ClearEditorPreview();

        if (changed)
            EditorApplication.delayCall += RefreshPreview;
    }

    private void RefreshPreview()
    {
        if (Grid != null)
            Grid.RefreshEditorPreview();
    }
}
#endif
