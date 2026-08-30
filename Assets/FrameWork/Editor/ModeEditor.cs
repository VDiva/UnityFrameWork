// using System.Collections.Generic;
// using UnityEditor;
// using UnityEditor.PackageManager;
// using UnityEditor.PackageManager.Requests;
// using UnityEngine;
//
// namespace FrameWork.Editor
// {
//     public class ModeEditor: UnityEditor.Editor
//     {
//         
//         private const string SERVER_SYMBOL = "ServerMode";
//         private const string CLIENT_SYMBOL = "ClientMode";
//         
//         
//         [MenuItem("FrameWork/模式/服务器")]
//         public static void AddServerDefine()
//         {
//             AddDefine(SERVER_SYMBOL);
//             RemoveDefine(CLIENT_SYMBOL);
//
//             Debug.Log("已添加服务器宏 ServerMode");
//         }
//
//
//         [MenuItem("FrameWork/模式/客户端")]
//         public static void AddClientDefine()
//         {
//             AddDefine(CLIENT_SYMBOL);
//             RemoveDefine(SERVER_SYMBOL);
//
//             Debug.Log("已添加客户端宏 ClientMode");
//         }
//
//
//         private static void AddDefine(string symbol)
//         {
//             BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
//
//             string defines =
//                 PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
//
//
//             if (!defines.Contains(symbol))
//             {
//                 if (!string.IsNullOrEmpty(defines))
//                     defines += ";";
//
//                 defines += symbol;
//             }
//
//
//             PlayerSettings.SetScriptingDefineSymbolsForGroup(
//                 group,
//                 defines
//             );
//         }
//
//
//         private static void RemoveDefine(string symbol)
//         {
//             BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
//
//             string defines =
//                 PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
//
//
//             defines = defines.Replace(symbol, "")
//                 .Replace(";;", ";");
//
//
//             PlayerSettings.SetScriptingDefineSymbolsForGroup(
//                 group,
//                 defines
//             );
//         }
//         
//     }
// }