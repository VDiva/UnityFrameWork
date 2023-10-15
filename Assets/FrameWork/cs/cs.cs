using System;
using System.Net.Sockets;
using GameData;
using UnityEngine;
using NetWork;
namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {
        private NetWorkSystem netWork;
        private void Start()
        {
            netWork = new NetWorkSystem();
            netWork.NetAsClient("127.0.0.1",8888,2048);
            
            netWork.client.ReceiveSuccessAction += Receive;
        }


        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.A))
            {
                netWork.client.SendMessage(new Data(){Name = "nihao"});
            }
        }

        private void OpenServer()
        {
            Debug.Log("服务器以打开");
        }

        private void Accept(object obj, SocketAsyncEventArgs args)
        {
            Debug.Log("链接到服务器");
        }
        
        private void Receive(byte[] data,object obj, SocketAsyncEventArgs args)
        {
            Debug.Log("收到消息"+data);
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