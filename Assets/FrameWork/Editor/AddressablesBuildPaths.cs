using System.IO;
using UnityEditor;
using UnityEngine;

namespace FrameWork
{
    /// <summary>
    /// Addressables profile variables resolved from the WeChat mini-game export settings.
    /// </summary>
    public static class AddressablesBuildPaths
    {
        private const string WeChatConfigPath = "Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset";

        public static string WeChatStreamingAssetsPath
        {
            get
            {
                string destination = ReadWeChatDestination();
                return Path.Combine(destination, "webgl", "StreamingAssets", "aa")
                    .Replace('\\', '/');
            }
        }

        private static string ReadWeChatDestination()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(WeChatConfigPath);
            if (config != null)
            {
                var serializedConfig = new SerializedObject(config);
                var projectConfig = serializedConfig.FindProperty("ProjectConf");
                var destination = projectConfig?.FindPropertyRelative("DST")?.stringValue;
                if (!string.IsNullOrWhiteSpace(destination))
                    return Path.GetFullPath(destination);
            }

            // Same default used by the current WeChat SDK configuration.
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "WXOUT"));
        }
    }

    [InitializeOnLoad]
    public static class WeChatCdnSynchronizer
    {
        private const string GameConfigPath = "Assets/FrameWork/Resources/ConfigData.asset";
        private const string WeChatConfigPath = "Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset";

        static WeChatCdnSynchronizer()
        {
            EditorApplication.delayCall += Sync;
        }

        [MenuItem("Tools/Build Mode/刷新微信小游戏 CDN")]
        public static void Sync()
        {
            var gameConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GameConfigPath);
            var weChatConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(WeChatConfigPath);
            if (gameConfig == null || weChatConfig == null)
                return;

            var gameSerialized = new SerializedObject(gameConfig);
            string root = gameSerialized.FindProperty("cdnUrl")?.stringValue;
            string version = gameSerialized.FindProperty("versions")?.stringValue;
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(version))
            {
                Debug.LogWarning("[WX] ConfigData 的 cdnUrl 或 versions 为空，未同步微信 CDN。");
                return;
            }

            string expectedCdn = root.TrimEnd('/') + "/" + version.Trim('/') + "/";
            var weChatSerialized = new SerializedObject(weChatConfig);
            var cdnProperty = weChatSerialized.FindProperty("ProjectConf")?.FindPropertyRelative("CDN");
            if (cdnProperty == null || cdnProperty.stringValue == expectedCdn)
                return;

            cdnProperty.stringValue = expectedCdn;
            weChatSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weChatConfig);
            AssetDatabase.SaveAssetIfDirty(weChatConfig);
            Debug.Log("[WX] 微信小游戏 CDN 已同步为: " + expectedCdn);
        }
    }

    public sealed class WeChatCdnConfigPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (path == "Assets/FrameWork/Resources/ConfigData.asset")
                {
                    EditorApplication.delayCall += WeChatCdnSynchronizer.Sync;
                    break;
                }
            }
        }
    }
}
