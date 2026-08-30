#if UNITY_EDITOR
using FancyScrollView;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CommonInfiniteScrollView))]
public sealed class CommonInfiniteScrollViewEditor : Editor
{
    CommonInfiniteScrollView ScrollView => (CommonInfiniteScrollView)target;

    void OnEnable()
    {
        EditorApplication.delayCall += RefreshPreview;
    }

    void OnDisable()
    {
        EditorApplication.delayCall -= RefreshPreview;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Infinite Preview"))
            ScrollView.RefreshEditorPreview();
        if (GUILayout.Button("Clear Infinite Preview"))
            ScrollView.ClearEditorPreview();

        if (changed)
            EditorApplication.delayCall += RefreshPreview;
    }

    void RefreshPreview()
    {
        if (ScrollView != null)
            ScrollView.RefreshEditorPreview();
    }
}
#endif
