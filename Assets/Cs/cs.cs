using System;
using FrameWork;
using Riptide;
using UnityEngine;

namespace Cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            NetWork.InitLog(Debug.Log);
            NetWorkAsClient.Connect("127.0.0.1",8888);
        }
    }
}