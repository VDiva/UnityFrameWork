using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FrameWork.Editor
{
    /// <summary>切换 WebGL Player 是否使用微信小游戏专用 SDK 分支。</summary>
    public static class WeChatMiniGameDefineMenu
    {
        private const string Define = "WEIXINMINIGAME";
        private const string WeChatMenu = "FrameWork/打包/微信小游戏";
        private const string NormalMenu = "FrameWork/打包/编辑器与普通 WebGL";

        [MenuItem(WeChatMenu, false, 100)]
        private static void EnableWeChatMiniGame()
        {
            SetEnabled(true);
        }

        [MenuItem(NormalMenu, false, 101)]
        private static void DisableWeChatMiniGame()
        {
            SetEnabled(false);
        }

        [MenuItem(WeChatMenu, true)]
        private static bool ValidateWeChatMiniGame()
        {
            bool enabled = IsEnabled();
            Menu.SetChecked(WeChatMenu, enabled);
            return true;
        }

        [MenuItem(NormalMenu, true)]
        private static bool ValidateNormalWebGl()
        {
            Menu.SetChecked(NormalMenu, !IsEnabled());
            return true;
        }

        private static bool IsEnabled()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            return Array.Exists(defines.Split(';'), value => value == Define);
        }

        private static void SetEnabled(bool enabled)
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            var values = new List<string>(defines.Split(';'));
            values.RemoveAll(string.IsNullOrWhiteSpace);
            values.RemoveAll(value => value == Define);
            if (enabled)
                values.Add(Define);

            PlayerSettings.SetScriptingDefineSymbols(
                NamedBuildTarget.WebGL, string.Join(";", values));
            AssetDatabase.SaveAssets();
            Debug.Log(enabled
                ? "已切换到微信小游戏模式：Player 构建使用微信登录和微信录音 SDK；Editor 仍使用机器码。"
                : "已切换到编辑器/普通 WebGL 模式：已移除 WEIXINMINIGAME 宏。");
        }
    }
}
