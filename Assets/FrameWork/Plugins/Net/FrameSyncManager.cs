using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class FrameSyncManager : SingletonAsMono<FrameSyncManager>
    {
        // 逻辑帧率
        public const int LogicFrameRate = 30;
        // 每帧间隔时间
        public const float FrameInterval = 1f / LogicFrameRate;
        
        // 当前逻辑帧编号
        public int CurrentFrame { get; private set; }
        
        // 输入缓冲区
        private Queue<InputData> inputQueue = new Queue<InputData>();
        
        // 所有需要同步的游戏对象
        private List<ISyncable> syncObjects = new List<ISyncable>();
        
        // 是否正在运行
        public bool IsRunning { get; private set; }

        
        // 开始帧同步
        public void StartFrameSync()
        {
            CurrentFrame = 0;
            inputQueue.Clear();
            IsRunning = true;
            StartCoroutine(LogicFrameLoop());
        }
        
        // 停止帧同步
        public void StopFrameSync()
        {
            IsRunning = false;
            StopAllCoroutines();
        }
        
        // 逻辑帧循环
        private IEnumerator LogicFrameLoop()
        {
            while (IsRunning)
            {
                // 等待固定的帧间隔时间
                yield return new WaitForSeconds(FrameInterval);
                
                // 处理当前帧
                ProcessCurrentFrame();
            }
        }
        
        // 处理当前帧逻辑
        private void ProcessCurrentFrame()
        {
            CurrentFrame++;
            
            // 收集当前帧的所有输入
            List<InputData> currentFrameInputs = new List<InputData>();
            while (inputQueue.Count > 0)
            {
                var input = inputQueue.Dequeue();
                // 确保输入是针对当前帧或之前的帧（防止未来帧输入）
                if (input.targetFrame <= CurrentFrame)
                {
                    currentFrameInputs.Add(input);
                }
                else
                {
                    // 未来帧的输入放回队列
                    inputQueue.Enqueue(input);
                    break;
                }
            }
            
            // 让所有同步对象执行当前帧逻辑
            foreach (var syncObj in syncObjects)
            {
                syncObj.OnLogicFrameUpdate(currentFrameInputs, FrameInterval);
            }
        }
        
        // 接收输入数据
        public void ReceiveInput(InputData input)
        {
            if (IsRunning)
            {
                inputQueue.Enqueue(input);
            }
        }
        
        // 注册同步对象
        public void RegisterSyncObject(ISyncable syncObj)
        {
            if (!syncObjects.Contains(syncObj))
            {
                syncObjects.Add(syncObj);
            }
        }
        
        // 注销同步对象
        public void UnregisterSyncObject(ISyncable syncObj)
        {
            if (syncObjects.Contains(syncObj))
            {
                syncObjects.Remove(syncObj);
            }
        }
    }
    
    // 同步对象接口
    public interface ISyncable
    {
        void OnLogicFrameUpdate(List<InputData> inputs, float deltaTime);
    }
}