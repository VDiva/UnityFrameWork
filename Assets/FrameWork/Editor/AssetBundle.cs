
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
            AssetDatabase.Refresh();
            Debug.Log("AB Finish!");
        }
        
 

                
    }
}