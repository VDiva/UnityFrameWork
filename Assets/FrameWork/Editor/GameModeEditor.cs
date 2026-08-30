using System.Collections.Generic;
using UnityEditor;

namespace FrameWork.Editor
{
    public class GameModeEditor: UnityEditor.Editor
    {
        //Release

        [MenuItem("FrameWork/版本/测试版")]
        public static void Debug()
        {
            // 获取当前的宏定义
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            HashSet<string> currentDefines = new HashSet<string>(definesString.Split(';'));

            currentDefines.Remove("Release");
            currentDefines.Add("Debug");
            // 更新宏定义
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                targetGroup, 
                string.Join(";", currentDefines)
            );
        }
        
        [MenuItem("FrameWork/版本/正式版")]
        public static void Release()
        {
            // 获取当前的宏定义
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            HashSet<string> currentDefines = new HashSet<string>(definesString.Split(';'));

            currentDefines.Add("Release");
            currentDefines.Remove("Debug");
            // 更新宏定义
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                targetGroup, 
                string.Join(";", currentDefines)
            );
        }
            
    }
}