using NetWorkClient;
using UnityEngine;

namespace FrameWork
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private int playerId;
        private PlayerController playerController;
    
        private void Start()
        {
            playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerId = playerController.PlayerId;
            }
        }
    
        private void Update()
        {
            // 只在帧同步运行时收集输入
            if (FrameSyncManager.Instance != null && FrameSyncManager.Instance.IsRunning)
            {
                // 收集输入
                InputData input = new InputData
                {
                    playerId = NetClient.GetClientId(),
                    // 预测输入应该应用的帧号（当前帧+1）
                    targetFrame = FrameSyncManager.Instance.CurrentFrame + 1,
                    moveX = Input.GetAxis("Horizontal"),
                    moveZ = Input.GetAxis("Vertical"),
                    isJump = Input.GetButtonDown("Jump")
                };
            
                // 本地输入直接入队
                //FrameSyncManager.Instance.ReceiveInput(input);
            
                // 发送输入到其他客户端（实际项目中这里会调用网络模块）
                NetClient.SenInputData(input.Serialize());
            }
        }
    }
}