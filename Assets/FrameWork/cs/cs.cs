using System;
using System.IO;
using System.Net.Sockets;
using FrameWork.AssetBundles;
using FrameWork.Global;
using FrameWork.NetManager.Convert;
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
           DownLoad.DownLoadAsset("https://download-cdn.jetbrains.com/rider/JetBrains.Rider-2023.2.2.exe",((progress,speed,curDown,lenght) =>
           {
               Debug.Log("下载进度:"+progress+"-下载速度:"+speed+"-"+curDown+"/"+lenght);
           } ),((bytes,fileName) =>
           {
               File.WriteAllBytes(Application.persistentDataPath+"/"+fileName,bytes);
               Debug.Log("下载完成");
           } ));
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