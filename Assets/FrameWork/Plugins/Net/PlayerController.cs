using System.Collections.Generic;
using TrueSync;
using UnityEngine;

namespace FrameWork
{
    [RequireComponent(typeof(TSTransform))]
    public class PlayerController : TrueSyncBehaviour,ISyncable
    {
        [SerializeField] private int playerId;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpForce = 7f;
        
        // 逻辑位置（用于计算）
        private TSVector logicPosition;
        // 逻辑旋转
        private TSQuaternion logicRotation;
        // 逻辑速度
        private TSVector logicVelocity;
        
        // 玩家ID属性
        public int PlayerId => playerId;

        private TSTransform _tsTransform;
        private void Awake()
        {
            _tsTransform = GetComponent<TSTransform>();
            // 初始化逻辑位置和旋转（与渲染位置一致）
            logicPosition = _tsTransform.position;
            logicRotation = _tsTransform.rotation;
            
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

        public void SetPlayerId(int id)
        {
            playerId = id;
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
            _tsTransform.position = TSVector.Lerp(_tsTransform.position, logicPosition, Time.deltaTime / FrameSyncManager.FrameInterval * 2f);
            
            // 更新旋转
            if (logicVelocity.sqrMagnitude > 0.1f)
            {
                TSQuaternion targetRotation = TSQuaternion.LookRotation(logicVelocity);
                logicRotation = TSQuaternion.Lerp(logicRotation, targetRotation, deltaTime * 10f);
            }
            _tsTransform.rotation = TSQuaternion.Lerp(_tsTransform.rotation, logicRotation, Time.deltaTime / FrameSyncManager.FrameInterval * 2f);
        }
        
        // 处理移动
        private void HandleMovement(InputData input, float deltaTime)
        {
            // 计算移动方向
            TSVector moveDir = new TSVector(input.moveX, 0, input.moveZ).normalized;
            
            // 计算目标速度
            TSVector targetVelocity = moveDir * moveSpeed;
            targetVelocity.y = logicVelocity.y; // 保留Y方向速度（重力/跳跃）
            
            // 平滑过渡到目标速度
            logicVelocity = TSVector.Lerp(logicVelocity, targetVelocity, deltaTime * 10f);
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
            return TSPhysics.Raycast(logicPosition, TSVector.down, out var hit,0.15f);
        }
        
        // 处理碰撞（简化版）
        private void HandleCollision(float deltaTime)
        {
            // 简单的射线检测处理碰撞
            TSVector moveDir = logicVelocity.normalized;
            FP moveDistance = logicVelocity.magnitude * deltaTime;
            
            
            if (TSPhysics.Raycast(logicPosition, moveDir, out var hit, moveDistance))
            {
                // 碰到障碍物，沿法线方向滑动
                TSVector slideDir = TSVector.ProjectOnPlane(moveDir, hit.normal);
                logicVelocity = slideDir * logicVelocity.magnitude;
            }
        }
        
        // 设置初始位置（确保所有客户端初始位置一致）
        public void SetInitialPosition(Vector3 position)
        {
            var tsVer=new TSVector(position.x, position.y, position.z);
            logicPosition = tsVer;
            _tsTransform.position = tsVer;
        }
        
    }
}