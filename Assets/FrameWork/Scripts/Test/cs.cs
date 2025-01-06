using System;
using System.Collections;
using System.Collections.Generic;
using FrameWork;
using Library.Msg;
using NetWorkClient;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using UnityEngine.UI;

public class cs : MonoBehaviour
{
    public Image _image;
    private void Start()
    {
        NetClient.RunTime = () => Time.time;
        RiptideLogger.Initialize(Debug.Log,true);
        NetClient.Start("127.0.0.1",8888);
        Debug.Log("1");
        //GetImg();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            var msg=NetClient.GetMessageHasRoomId(MessageSendMode.Reliable, RoomType.Retransmission);
            msg.AddString("你好啊");
            NetClient.Retransmission(msg);
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            NetClient.CreateRoom("爱吃大嘴巴子!",5);
        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            NetClient.LeaveRoom();
        }
    }

    private void FixedUpdate()
    {
        NetClient.Update();
    }


    public async void GetImg()
    {
        var url = "https://w.wallhaven.cc/full/rr/wallhaven-rrmg11.jpg";
        var texture=await RequestTool.Create(url, Methods.Get).SendTaskAsTexture();
        _image.sprite = texture;
        _image.SetNativeSize();
    }
    
    
    [MessageHandler((ushort)RoomType.Retransmission)]
    private static void Retransmission(Message message)
    {
        Debug.Log(message.GetString());
    }
    
    
}
