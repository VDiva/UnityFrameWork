
using System;
using FrameWork.WebSocket;
using UnityEngine;

namespace FrameWork.Test
{
    public class Test : MonoBehaviour
    {
        private async void Start()
        {
           WebNet.Connect("ws://localhost:5100");
        }
    }
}