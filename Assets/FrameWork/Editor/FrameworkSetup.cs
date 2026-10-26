using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FrameWork.Editor
{
    public static class FrameworkSetup
    {
        [MenuItem("FrameWork/初始化抽离框架")]
        public static void Setup()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';')
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "ADDRESSABLESCN_INSTALLED").ToList();
            foreach (string symbol in new[] { "CECIL_INSTALLED", "YOOASSET_3" })
                if (!defines.Contains(symbol)) defines.Add(symbol);
            string next = string.Join(";", defines);
            if (PlayerSettings.GetScriptingDefineSymbolsForGroup(group) != next)
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, next);
            EditorBuildSettings.RemoveConfigObject("com.unity.addressableassets");
            Directory.CreateDirectory("Assets/FrameWork/Xlsx");
            Directory.CreateDirectory("Assets/FrameWork/Asset/Xlsx");
            Directory.CreateDirectory("Assets/FrameWork/Scripts/Xlsx");
            YooResourceEditor.Configure();
            FrameworkIntegrationEditor.Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("框架初始化完成：YooAsset / UiRoot。Assets 下只需 FrameWork。");
        }

        public static void ValidateCompilation()
        {
            Setup();
            AssertLayout();
            Debug.Log("FRAMEWORK_COMPILE_OK");
            EditorApplication.Exit(0);
        }

        internal static void AssertLayout()
        {
            foreach (string directory in Directory.GetDirectories("Assets"))
                if (Path.GetFileName(directory) != "FrameWork")
                    throw new InvalidOperationException("发现 FrameWork 外的资产目录：" + directory);
        }
    }
}
