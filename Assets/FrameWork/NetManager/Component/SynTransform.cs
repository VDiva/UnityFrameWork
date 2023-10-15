using System;
using FrameWork.NetManager.Convert;
using GameData;
using NetWork;
using UnityEngine;

namespace FrameWork.NetManager.Component
{
    
    public class SynTransform : MonoBehaviour
    {
        [Header("是否为帧同步是的话会在物理帧里每帧发送数据")]
        public bool isFrameSyn;
        
        [Header("不是帧同步会以每time发送一次数据")]
        public float time=0.2f;

        private void Start()
        {
            if (!isFrameSyn)
            {
                InvokeRepeating("SendTransForm",time,time);
            }
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

        private void FixedUpdate()
        {
            if (isFrameSyn)
            {
                SendTransForm();
            }
        }

        private void SendTransForm()
        {
            NetWorkSystem.client.SendMessage(new Data(){TransfromData = transform.ToTransformData()});
        }
    }
}