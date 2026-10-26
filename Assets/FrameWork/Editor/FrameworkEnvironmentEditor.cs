using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public static class FrameworkEnvironmentEditor
    {
        private const string TestDefine = "FRAMEWORK_TEST";
        private const string TestMenu = "FrameWork/运行环境/切换测试版";
        private const string ReleaseMenu = "FrameWork/运行环境/切换正式版";

        [MenuItem(TestMenu)]
        public static void UseTest() => SetEnvironment(true);

        [MenuItem(ReleaseMenu)]
        public static void UseRelease() => SetEnvironment(false);

        [MenuItem(TestMenu, true)]
        private static bool ValidateTest() => ValidateMenu(TestMenu, true);

        [MenuItem(ReleaseMenu, true)]
        private static bool ValidateRelease() => ValidateMenu(ReleaseMenu, false);

        private static bool ValidateMenu(string menu, bool test)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            bool supported = group != BuildTargetGroup.Unknown;
            bool activeTest = supported && GetDefines(group).Contains(TestDefine);
            Menu.SetChecked(menu, supported && activeTest == test);
            return supported && !EditorApplication.isPlayingOrWillChangePlaymode &&
                !EditorApplication.isCompiling && !EditorApplication.isUpdating && !BuildPipeline.isBuildingPlayer;
        }

        private static string[] GetDefines(BuildTargetGroup group) =>
            PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        private static void SetEnvironment(bool test)
        {
            if (!ValidateMenu(test ? TestMenu : ReleaseMenu, test)) return;
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = GetDefines(group);
            if (defines.Contains(TestDefine) == test) return;
            var next = defines.Where(value => value != TestDefine);
            if (test) next = next.Concat(new[] { TestDefine });
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", next));
            AssetDatabase.SaveAssets();
            Debug.Log($"已将 {group} 切换为{(test ? "测试版（testRemoteUrl）" : "正式版（remoteUrl）")}，等待 Unity 编译完成后再构建或发布。");
        }
    }
}
