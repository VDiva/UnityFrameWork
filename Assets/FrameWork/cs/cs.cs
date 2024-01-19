using System;
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                NetWorkSystem.CreateRoom("你好",10);
            }
            
            if (Input.GetKeyDown(KeyCode.E))
            {
                NetWorkSystem.LeftRoom();
            }
            
            if (Input.GetKeyDown(KeyCode.W))
            {
                NetWorkSystem.JoinRoom(1);
            }
            
            if (Input.GetKeyDown(KeyCode.R))
            {
                NetWorkSystem.MatchingRoom("你好",10);
            }
        }
    }
}