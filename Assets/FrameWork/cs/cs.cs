using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using FrameWork.AssetBundles;
using FrameWork.Global;
using FrameWork.NetManager.Convert;
using FrameWork.Type;
using GameData;
using UnityEngine;
using NetWork;
using Random = UnityEngine.Random;

namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {
        
        private void Start()
        {

            
            VersionDetection.Detection(((abPack,config) =>
            {
                long lenght = 0;
                foreach (var item in abPack)
                {
                    lenght += item.Size;
                }
                Debug.Log("检测到更新大小为:"+DownLoad.GetFileSize(lenght));
                
                DownLoadAbPack.AddPackDownTack(abPack,((progress, speed, curLenght, len) =>
                {
                    Debug.Log("下载进度:"+progress+"-下载速度:"+speed+"-"+curLenght+"/"+len);
                } ),(list =>
                {
                    Debug.Log("下载成功 更新文件为"+DownLoad.GetFileSize(config.Length));
                    File.WriteAllBytes(Application.persistentDataPath+"/ABConfig.txt",config);
                    foreach (var item in list)
                    {
                        Debug.Log(item.Name+" 下载大小为:"+DownLoad.GetFileSize(item.PackData.Length));
                        File.WriteAllBytes(Application.persistentDataPath+"/"+item.Name,item.PackData);
                    }
                    
                } ));
                
                
            }));
            
        }


        private void Update()
        {
            
        }

        private void FixedUpdate()
        {
            
        }

    }
    
    
    
}






// [NetFile("a")]
// public int a = 1;
// [NetFile("b")]
// public int b = 5;
// [NetFile("v")]
// public int v = 2;
// [NetFile("c")]
// public int c = 3;
// [NetFile("g")]
// public int g = 4;

// FieldInfo[] fieldInfo = this.GetType().GetFields(BindingFlags.Instance| BindingFlags.Public| BindingFlags.NonPublic);
// // var properties=this.GetType().GetProperties(BindingFlags.Instance| BindingFlags.Public| BindingFlags.NonPublic);
// foreach (var item in fieldInfo)
// {
//     bool isD = item.IsDefined(typeof(NetFile));
//     if (isD)
//     {
//         Debug.Log((int)item.GetValue(this));
//     }
// }



// Type dome=typeof(dome);
// var method=dome.GetMethod("Message");
// object obj=Activator.CreateInstance(dome);
// object[] parameters = new object[]{};
// method.Invoke(obj, parameters);




// public class HttpId
// {
//     //示例，给id标记api
//     [HttpApiKey("Register")]
//     public const int registerId = 10001;
//     [HttpApiKey("Login")]
//     public const int loginId = 10002;
//  
//  
//     public static void GetHttpApi()
//     {
//         //反射获取字段
//         System.Reflection.FieldInfo[] fields = typeof(HttpId).GetFields();
//         System.Type attType = typeof(HttpApiKey);
//         for(int i = 0; i < fields.Length; i++)
//         {
//             if(fields[i].IsDefined(attType,false))
//             {
//                 //获取id
//                 int httpId = (int)fields[i].GetValue(null);
//                 //获取api，读取字段的自定义Attribute
//                 object attribute = fields[i].GetCustomAttributes(typeof(HttpApiKey),false)[0];
//                 string httpApi = (attribute as HttpApiKey).httpApi;
//             }
//         }
//     }
// }