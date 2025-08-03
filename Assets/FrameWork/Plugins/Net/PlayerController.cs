using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    public class PlayerController : MonoBehaviour,ISyncable
    {
        [SerializeField] private int playerId;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 7f;
        
        // 逻辑位置（用于计算）
        private Vector3 logicPosition;
        // 逻辑旋转
        private Quaternion logicRotation;
        // 逻辑速度
        private Vector3 logicVelocity;
        
        // 玩家ID属性
        public int PlayerId => playerId;
        
        private void Awake()
        {
            // 初始化逻辑位置和旋转（与渲染位置一致）
            logicPosition = transform.position;
            logicRotation = transform.rotation;
            
            // 注册到帧同步管理器
            if (FrameSyncManager.Instance != null)
            {
                FrameSyncManager.Instance.RegisterSyncObject(this);
            }
        }
        
        private void OnDestroy()
        {
            // 从帧同步管理器注销
            if (FrameSyncManager.Instance != null)
            {
                FrameSyncManager.Instance.UnregisterSyncObject(this);
            }
        }
        
        // 逻辑帧更新（所有客户端在同一帧执行相同计算）
        public void OnLogicFrameUpdate(List<InputData> inputs, float deltaTime)
        {
            // 找到当前玩家的输入
            InputData playerInput = inputs.Find(input => input.playerId == playerId);
            
            if (playerInput != null)
            {
                // 处理移动
                HandleMovement(playerInput, deltaTime);
                
                // 处理跳跃
                if (playerInput.isJump)
                {
                    HandleJump();
                }
            }
            
            // 应用重力
            ApplyGravity(deltaTime);
            
            // 处理碰撞（简化版）
            HandleCollision(deltaTime);
            
            // 更新逻辑位置
            logicPosition += logicVelocity * deltaTime;
            
            // 平滑过渡到逻辑位置（渲染插值）
            transform.position = Vector3.Lerp(transform.position, logicPosition, 
                Time.deltaTime / FrameSyncManager.FrameInterval * 2f);
            
            // 更新旋转
            if (logicVelocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(logicVelocity);
                logicRotation = Quaternion.Lerp(logicRotation, targetRotation, deltaTime * 10f);
            }
            transform.rotation = Quaternion.Lerp(transform.rotation, logicRotation, 
                Time.deltaTime / FrameSyncManager.FrameInterval * 2f);
        }
        
        // 处理移动
        private void HandleMovement(InputData input, float deltaTime)
        {
            // 计算移动方向
            Vector3 moveDir = new Vector3(input.moveX, 0, input.moveZ).normalized;
            
            // 计算目标速度
            Vector3 targetVelocity = moveDir * moveSpeed;
            targetVelocity.y = logicVelocity.y; // 保留Y方向速度（重力/跳跃）
            
            // 平滑过渡到目标速度
            logicVelocity = Vector3.Lerp(logicVelocity, targetVelocity, deltaTime * 10f);
        }
        
        // 处理跳跃
        private void HandleJump()
        {
            // 简单检测是否在地面上
            if (IsGrounded())
            {
                logicVelocity.y = jumpForce;
            }
        }
        
        // 应用重力
        private void ApplyGravity(float deltaTime)
        {
            if (!IsGrounded())
            {
                logicVelocity.y -= 9.81f * 2f * deltaTime;
            }
            else if (logicVelocity.y < 0)
            {
                // 着地时重置Y速度
                logicVelocity.y = 0;
            }
        }
        
        // 检测是否在地面上
        private bool IsGrounded()
        {
            return Physics.Raycast(logicPosition, Vector3.down, 0.15f);
        }
        
        // 处理碰撞（简化版）
        private void HandleCollision(float deltaTime)
        {
            // 简单的射线检测处理碰撞
            Vector3 moveDir = logicVelocity.normalized;
            float moveDistance = logicVelocity.magnitude * deltaTime;
            
            if (Physics.Raycast(logicPosition, moveDir, out RaycastHit hit, moveDistance))
            {
                // 碰到障碍物，沿法线方向滑动
                Vector3 slideDir = Vector3.ProjectOnPlane(moveDir, hit.normal);
                logicVelocity = slideDir * logicVelocity.magnitude;
            }
        }
        
        // 设置初始位置（确保所有客户端初始位置一致）
        public void SetInitialPosition(Vector3 position)
        {
            logicPosition = position;
            transform.position = position;
        }
        
    }
}