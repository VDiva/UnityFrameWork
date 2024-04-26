
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

            
            var path = Application.streamingAssetsPath + Tool.GetAbDictoryPath();
            
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
            Debug.Log("AB Finish!");
        }
        
 

                
    }
}