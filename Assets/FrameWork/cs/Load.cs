using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameWork
{
    public class Load : MonoBehaviour
    {
        private void Start()
        {
            
            // DownLoad.DownLoadAsset("https://download-cdn.jetbrains.com.cn/rider/JetBrains.Rider-2023.3.2.exe",((f, f1, arg3, arg4) =>
            // {
            //     Debug.Log("进度:"+f+"-"+"速度:"+f1+"/s"+"-"+arg3+"/"+arg4);
            // }),((bytes, s) =>
            // {
            //     
            // } ));
            

            VersionDetection.Detection(((list, bytes) =>
            {
                if (list.Count>0)
                {
                    DownLoadAbPack.AddPackDownTack(list,((f, f1, arg3, arg4) =>
                    {
                        Debug.Log("下载进度:"+f+"/"+f1+"-"+arg3+"/"+arg4);
                    } ),(dates =>
                    {
                        Debug.Log("下载完成");
                        foreach (var item in dates)
                        {
                            File.WriteAllBytes(Application.persistentDataPath+"/"+item.Name,item.PackData);
                            Debug.Log(item.Name);
                        }
            
                        SceneManager.LoadScene("Scenes/SampleScene");
                    } ));
                    
                    File.WriteAllBytes(Application.persistentDataPath+"/"+GlobalVariables.Configure.ConfigName,bytes);
                }
                else
                {
                    Debug.Log("版本一致无需更新");
                    SceneManager.LoadScene("Scenes/SampleScene");
                }
            } ));
        }
    }
}