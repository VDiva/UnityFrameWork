using System;
using System.IO;
using HybridCLR.Commands;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public static class FrameworkSyncValidation
    {
        [Serializable] private sealed class LayoutResult { public bool success; public string error; }

        public static void Validate()
        {
            try
            {
                FrameworkSetup.Setup();
                FrameworkHotUpdateValidation.Run();
                var layout = JsonUtility.FromJson<LayoutResult>(File.ReadAllText("Library/FrameWork/Validation/HotUpdateLayout.json"));
                if (!layout.success) throw new InvalidOperationException(layout.error);
                string output = "Library/FrameWork/SyncValidation/" + EditorUserBuildSettings.activeBuildTarget;
                CompileDllCommand.CompileDll(output, EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development);
                if (!File.Exists(Path.Combine(output, "HotUpdate.dll")) || !File.Exists(Path.Combine(output, "FrameWork.Bootstrap.dll")))
                    throw new InvalidOperationException("The updated HotUpdate/Bootstrap assemblies were not produced.");
                Debug.Log("FRAMEWORK_SYNC_VALIDATED: " + EditorUserBuildSettings.activeBuildTarget);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
