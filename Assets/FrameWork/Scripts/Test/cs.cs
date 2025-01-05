using System;
using System.Collections;
using System.Collections.Generic;
using FrameWork;
using NetWorkClient;
using Riptide.Utils;
using UnityEngine;

public class cs : MonoBehaviour
{
    private void Start()
    {
        RiptideLogger.Initialize(Debug.Log,true);
        NetClient.Start("127.0.0.1",8888);
    }


    private void FixedUpdate()
    {
        NetClient.Update();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            NetClient.CreateRoom("爱吃大嘴巴子",4);
        }
        
        if (Input.GetKeyDown(KeyCode.S))
        {
            NetClient.LeaveRoom();
        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            NetClient.MatchingRoom();
        }
    }


    public void Log()
    {
        Debug.Log("Hello World!");
        
    }
}
