
using System.IO;
using UnityEditor;
using UnityEngine;


namespace FrameWork
{
    public class AssetBundle: UnityEditor.Editor
    {
        
        
        [MenuItem("FrameWork/AB/CreatAssetBundle")]
        public static void CreatAssetBundle()
        {

            
            var path = Application.streamingAssetsPath + GetAbDictoryPath(EditorUserBuildSettings.activeBuildTarget);
            
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.Delete(path,true);
                Directory.CreateDirectory(path);
            }
            
            BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);


            var files = Directory.GetFiles(path);
            for (int i = 0; i < files.Length; i++)
            {
                var data = Tool.Encrypt(File.ReadAllBytes(files[i]), Config.key);
                File.WriteAllBytes(files[i],data);
            }
            
            ABConfig.CreateConfig();
            AssetDatabase.Refresh();
            Debug.Log("AB Create Finish!");
        }
        
 
        [MenuItem("Assets/FrameWork/AB/UpdateAb")]
        public static void UpdateAssetBundle()
        {
            var path = Application.streamingAssetsPath + GetAbDictoryPath(EditorUserBuildSettings.activeBuildTarget);
            AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            if (Directory.Exists(path))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                var file = directoryInfo.GetFiles();
                for (int i = 0; i < file.Length; i++)
                {
                    if (Path.GetFileNameWithoutExtension(file[i].FullName).IndexOf(ai.assetBundleName)!=-1)
                    {
                        File.Delete(file[i].FullName);
                    }
                }
            }
            BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
            DirectoryInfo dir = new DirectoryInfo(path);
            var f = dir.GetFiles();
            for (int i = 0; i < f.Length; i++)
            {
                if (Path.GetFileNameWithoutExtension(f[i].FullName).IndexOf(ai.assetBundleName)!=-1)
                {
                    var data = Tool.Encrypt(File.ReadAllBytes(f[i].FullName), Config.key);
                    File.WriteAllBytes(f[i].FullName,data);
                }
            }
            
            ABConfig.CreateConfig();
            AssetDatabase.Refresh();
            Debug.Log("AB Update Finish!");
        }
        
        public static void UpdateAssetBundle(string abPackName)
        {
            var path = Application.streamingAssetsPath + GetAbDictoryPath(EditorUserBuildSettings.activeBuildTarget);
            //AssetImporter ai=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.activeGameObject));
            if (Directory.Exists(path))
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                var file = directoryInfo.GetFiles();
                for (int i = 0; i < file.Length; i++)
                {
                    if (Path.GetFileNameWithoutExtension(file[i].FullName).IndexOf(abPackName)!=-1)
                    {
                        File.Delete(file[i].FullName);
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(path);
            }
            BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
            DirectoryInfo dir = new DirectoryInfo(path);
            var f = dir.GetFiles();
            for (int i = 0; i < f.Length; i++)
            {
                if (Path.GetFileNameWithoutExtension(f[i].FullName).IndexOf(abPackName)!=-1)
                {
                    var data = Tool.Encrypt(File.ReadAllBytes(f[i].FullName), Config.key);
                    File.WriteAllBytes(f[i].FullName,data);
                }
            }
            
            ABConfig.CreateConfig();
            AssetDatabase.Refresh();
            Debug.Log("AB Update Finish!");
        }
        
        public static string GetAbDictoryPath(BuildTarget platform)
        {
            string path = "";
            //RuntimePlatform platform = platform;
            switch (platform)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    path = "/StandaloneWindows64/";
                    break;
                case BuildTarget.Android:
                    path =  "/Android/";
                    break;
                case BuildTarget.iOS:
                    path ="/Ios/";
                    break;
                case BuildTarget.WebGL:
                    path = "/WebGl/";
                    break;
            }

            return path;
        }

                
    }
}