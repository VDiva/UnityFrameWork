using System;
using System.IO;
using System.Text.RegularExpressions;
using FrameWork.FPS;
using UnityEditor;

namespace FrameWork.RPG.Editor
{
    internal static class RpgActionScriptCreator
    {
        internal const string ClassPrefix = "RpgAction_";

        internal static void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建行为树动作脚本", ClassPrefix + "MyAction", "cs",
                "选择运行时脚本位置，文件名使用字母、数字和下划线。", "Assets/FrameWork/RPG");
            if (!RpgWeaponEditorActions.CanCreate(path)) return;

            string fileName = Path.GetFileNameWithoutExtension(path);
            string actionName = fileName.StartsWith(ClassPrefix, StringComparison.Ordinal)
                ? fileName.Substring(ClassPrefix.Length) : fileName;
            if (!Regex.IsMatch(actionName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                EditorUtility.DisplayDialog("动作脚本名称无效", "请使用英文字母或下划线开头，后续可包含数字。", "确定");
                return;
            }

            string className = ClassPrefix + actionName;
            foreach (var type in TypeCache.GetTypesDerivedFrom<RpgCustomBehaviorAction>())
            {
                if (type.FullName != "FrameWork.FPS." + className) continue;
                EditorUtility.DisplayDialog("动作名称已存在", "请选择其他文件名。", "确定");
                return;
            }

            string displayName = ObjectNames.NicifyVariableName(actionName);
            File.WriteAllText(path, BuildSource(className, displayName));
            AssetDatabase.ImportAsset(path);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null) return;
            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
            AssetDatabase.OpenAsset(script);
        }

        private static string BuildSource(string className, string displayName) =>
            $@"using System;
using GraphProcessor;

namespace FrameWork.FPS
{{
    [Serializable, NodeMenuItem(""行为树/自定义动作/{displayName}"", typeof(RpgBehaviorTree))]
    public sealed class {className} : RpgCustomBehaviorAction
    {{
        public override string name => ""{displayName}"";

        protected override RpgBehaviorStatus Execute(RpgEnemyAgent agent, float deltaTime)
        {{
            // 在这里编写动作逻辑。持续执行返回 Running，完成返回 Success，无法执行返回 Failure。
            return RunBuiltIn(agent, RpgEnemyAction.Idle, deltaTime);
        }}
    }}
}}
";
    }
}
