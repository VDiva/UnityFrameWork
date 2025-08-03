using System;
using UnityEngine;

namespace FrameWork
{
    [Serializable]
    public class InputData
    {
        // 玩家ID
        public int playerId;
    
        // 目标帧号（该输入应该在哪个逻辑帧执行）
        public int targetFrame;
    
        // 移动输入
        public float moveX;
        public float moveZ;
    
        // 跳跃输入
        public bool isJump;
    
        // 序列化用于网络传输
        public string Serialize()
        {
            try
            {
                return JsonUtility.ToJson(this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"InputData序列化失败: {ex.Message}");
                return string.Empty;
            }
        }
    
        // 反序列化
        public static InputData Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("尝试反序列化空的InputData JSON字符串");
                return new InputData();
            }
        
            try
            {
                return JsonUtility.FromJson<InputData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"InputData反序列化失败: {ex.Message}，JSON: {json}");
                return new InputData();
            }
        }
    }
}