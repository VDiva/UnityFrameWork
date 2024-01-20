using System;
using System.Collections.Concurrent;
using FrameWork.NetWork.Component;
using NetWork.System;
using Riptide;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {

        private void Start()
        {
            NetWorkSystem.Start("127.0.0.1:8888");
        }

        private void OnEnable()
        {
            NetWorkSystem.OnPlayerJoinRoom += OnJoin;
         
        }

        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                NetWorkSystem.CreateRoom("你好",10);
            }
            
            if (Input.GetKeyDown("2"))
            {
                NetWorkSystem.LeftRoom();
            }
            
            if (Input.GetKeyDown("3"))
            {
                NetWorkSystem.JoinRoom(1);
            }
            
            if (Input.GetKeyDown("4"))
            {
                NetWorkSystem.MatchingRoom("你好",10);
            }


            if (Input.GetKeyDown(KeyCode.P))
            {
                NetWorkSystem.Instantiate("Cube",Vector3.zero,Vector3.zero,true);
            }
        }
        
        private void OnJoin(ushort id,int roomId)
        {
            
        }
        
        
        

    }
}