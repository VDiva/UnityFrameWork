using System;
using GameData;
using Pathfinding;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// 同步网络对象根节点的位置、旋转、缩放，以及指定子节点的本地变换。
    /// 有控制权的一端上传位置和速度，其他客户端短时预测并平滑校正。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(WebNetworkIdentity))]
    public sealed class WebNetworkTransform : WebNetworkBehaviour
    {
        [Serializable]
        public sealed class ChildTransformSync
        {
            [Tooltip("需要同步的子节点。所有客户端相同角色的配置顺序必须一致。")]
            public Transform target;
            public bool syncPosition = true;
            public bool syncRotation;
            public bool syncScale = true;

            [NonSerialized] public Vector3 targetPosition;
            [NonSerialized] public Quaternion targetRotation;
            [NonSerialized] public Vector3 targetScale;
            [NonSerialized] public Vector3 lastSentPosition;
            [NonSerialized] public Quaternion lastSentRotation;
            [NonSerialized] public Vector3 lastSentScale;
            [NonSerialized] public bool receivedSnapshot;
            [NonSerialized] public bool sentSnapshot;
        }

        [Header("根节点同步")]
        [SerializeField] bool syncPosition = true;
        [SerializeField] bool syncRotation = true;
        [SerializeField] bool syncScale = true;

        [Header("指定子节点同步")]
        [Tooltip("子节点使用 localPosition、localRotation、localScale 同步。")]
        [SerializeField] ChildTransformSync[] childTransforms = Array.Empty<ChildTransformSync>();

        [Header("发送设置")]
        [SerializeField, Min(0.02f)] float sendInterval = 0.1f;
        [SerializeField, Min(0f)] float positionThreshold = 0.01f;
        [SerializeField, Min(0f)] float rotationThreshold = 0.5f;
        [SerializeField, Min(0f)] float scaleThreshold = 0.01f;
        [SerializeField, Min(0.2f)] float heartbeatInterval = 1f;

        [Header("远端快照")]
        [Tooltip("按发送周期保留的插值时间。增大可抵抗网络抖动，但会增加远端显示延迟。")]
        [SerializeField, Range(1f, 3f)] float snapshotBufferMultiplier = 1.5f;
        [Tooltip("网络抖动时平滑修正远端位置的时间，单位秒。")]
        [SerializeField, Range(0.01f, 0.15f)] float positionCorrectionTime = 0.04f;
        [Tooltip("短暂丢包时沿最后速度继续移动的最长时间，单位秒。")]
        [SerializeField, Range(0.05f, 0.5f)] float maxPredictionTime = 0.2f;
        [Tooltip("位置误差超过此距离时按传送处理，不让物体穿越整张地图。")]
        [SerializeField, Min(0.1f)] float snapDistance = 8f;
        [SerializeField, Min(0.1f)] float interpolationSpeed = 12f;
        [SerializeField, Min(0.1f)] float rotationInterpolationSpeed = 15f;
        [Tooltip("开启时平滑同步根节点和子节点缩放；关闭时收到数据后立即设置缩放。")]
        [SerializeField] bool interpolateScale = true;
        [SerializeField, Min(0.1f)] float scaleInterpolationSpeed = 12f;

        Vector3 targetPosition;
        Quaternion targetRotation;
        Vector3 targetScale;
        Vector3 lastSentPosition;
        Vector3 lastSentVelocity;
        Vector3 lastVelocitySamplePosition;
        Vector3 targetVelocity;
        Quaternion lastSentRotation;
        Vector3 lastSentScale;
        float nextSendTime;
        float lastSendTime;
        float repeatPositionUntil;
        float lastVelocitySampleTime;
        double lastRemoteSampleTime;
        double lastRemoteSentTime;
        ulong sendSequence;
        ulong receiveSequence;
        bool receivedFirstSnapshot;
        bool sentFirstSnapshot;
        bool wasSoloAi;
        bool hasSenderClock;
        double senderToLocalTimeOffset;
        Rigidbody2D body;
        RigidbodyType2D originalBodyType;
        RigidbodyInterpolation2D originalBodyInterpolation;
        AILerp aiLerp;
        Vector3 renderVelocity;
        readonly SnapshotTimeline<RemoteSnapshot> remoteSnapshots =
            new SnapshotTimeline<RemoteSnapshot>(8);

        struct RemoteSnapshot
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Vector3 velocity;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                originalBodyType = body.bodyType;
                originalBodyInterpolation = body.interpolation;
            }
            aiLerp = GetComponent<AILerp>();
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            targetScale = transform.localScale;
            InitializeChildren();
        }

        void OnEnable()
        {
            WebNetworkManager.AuthorityChanged += OnAuthorityChanged;
        }

        void OnDisable()
        {
            WebNetworkManager.AuthorityChanged -= OnAuthorityChanged;
        }

        void Update()
        {
            if (HasAuthority)
                UpdateAuthorityObject();
        }

        void LateUpdate()
        {
            if (!HasAuthority)
                UpdateRemoteObject();
        }

        void UpdateAuthorityObject()
        {
            float now = Time.unscaledTime;
            float interval = sendInterval;
            // 单人房间没有远端观众，AI 只需让服务端保留近期状态供后来加入的玩家使用。
            WebNetworkRoomManager roomManager = WebNetworkRoomManager.Instance;
            bool soloAi = Identity.IsAiObject && roomManager != null &&
                          roomManager.CurrentRoom != null && roomManager.CurrentRoom.Members.Count <= 1;
            if (soloAi)
                interval = Mathf.Max(interval, 1f);
            // 新观众进房时立即上传当前 AI 状态，避免沿用单人模式的下次发送时间。
            if (wasSoloAi && !soloAi)
                nextSendTime = now;
            wasSoloAi = soloAi;
            if (!LobbyWebNet.IsConnected || NetId == 0 || now < nextSendTime)
                return;

            // 保持平均发送周期稳定；直接用 now + interval 会反复累积渲染帧误差。
            nextSendTime = nextSendTime > 0f && now - nextSendTime < interval
                ? nextSendTime + interval : now + interval;
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Vector3 scale = transform.localScale;
            Vector3 velocity = sentFirstSnapshot && lastVelocitySampleTime > 0f
                ? (position - lastVelocitySamplePosition) / Mathf.Max(0.001f, now - lastVelocitySampleTime)
                : Vector3.zero;
            lastVelocitySamplePosition = position;
            lastVelocitySampleTime = now;
            bool moving = syncPosition && velocity.sqrMagnitude >= 0.0004f;
            if (moving && !soloAi)
                repeatPositionUntil = now + Mathf.Max(0.2f, interval * 3f);

            bool changed = !sentFirstSnapshot ||
                           syncPosition && PositionChanged(position, lastSentPosition) ||
                           syncPosition && (velocity - lastSentVelocity).sqrMagnitude >= 0.01f ||
                           syncRotation && RotationChanged(rotation, lastSentRotation) ||
                           syncScale && ScaleChanged(scale, lastSentScale) ||
                           HaveChildrenChanged();

            if (!changed && now >= repeatPositionUntil && now - lastSendTime < heartbeatInterval)
                return;

            var snapshot = new NetworkTransformData
            {
                ObjectId = NetId,
                RoomId = roomManager?.CurrentRoom?.RoomId ?? 0,
                SceneEpoch = roomManager?.CurrentRoom?.SceneEpoch ?? 0,
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                RotationX = rotation.x,
                RotationY = rotation.y,
                RotationZ = rotation.z,
                RotationW = rotation.w,
                ScaleX = scale.x,
                ScaleY = scale.y,
                ScaleZ = scale.z,
                HasScale = syncScale,
                Sequence = ++sendSequence,
                SentTime = Time.unscaledTimeAsDouble,
                VelocityX = velocity.x,
                VelocityY = velocity.y,
                VelocityZ = velocity.z,
                HasVelocity = syncPosition
            };

            AddChildSnapshots(snapshot);
            WebNetworkManager.SendTransform(snapshot, Identity.IsPlayerObject);

            lastSentPosition = position;
            lastSentVelocity = velocity;
            lastSentRotation = rotation;
            lastSentScale = scale;
            SaveSentChildren();
            lastSendTime = now;
            sentFirstSnapshot = true;
        }

        void UpdateRemoteObject()
        {
            if (!receivedFirstSnapshot)
                return;

            float positionT = ExpLerpFactor(interpolationSpeed);
            float rotationT = ExpLerpFactor(rotationInterpolationSpeed);
            float scaleT = ExpLerpFactor(scaleInterpolationSpeed);
            Vector3 renderPosition = targetPosition;
            Quaternion renderRotation = targetRotation;
            Vector3 renderScale = targetScale;
            if (!SampleRemoteSnapshot(out renderPosition, out renderRotation, out renderScale))
            {
                float age = Mathf.Clamp((float)(Time.unscaledTimeAsDouble - lastRemoteSampleTime),
                    0f, maxPredictionTime > 0f ? maxPredictionTime : 0.2f);
                renderPosition = targetPosition + targetVelocity * age;
            }

            if (syncPosition)
            {
                float snapSqr = Mathf.Max(snapDistance > 0f ? snapDistance : 8f,
                    targetVelocity.magnitude * (maxPredictionTime > 0f ? maxPredictionTime : 0.2f) * 2f);
                snapSqr *= snapSqr;
                if ((renderPosition - transform.position).sqrMagnitude > snapSqr)
                {
                    transform.position = renderPosition;
                    renderVelocity = Vector3.zero;
                }
                else
                {
                    transform.position = Vector3.SmoothDamp(transform.position, renderPosition,
                        ref renderVelocity, positionCorrectionTime > 0f ? positionCorrectionTime : 0.04f,
                        Mathf.Infinity, Time.unscaledDeltaTime);
                }
            }
            if (syncRotation)
                transform.rotation = Quaternion.Slerp(transform.rotation, renderRotation, rotationT);
            if (syncScale)
                transform.localScale = interpolateScale
                    ? Vector3.Lerp(transform.localScale, renderScale, scaleT)
                    : renderScale;

            foreach (ChildTransformSync child in childTransforms)
            {
                if (child?.target == null || !child.receivedSnapshot)
                    continue;

                if (child.syncPosition)
                    child.target.localPosition = Vector3.Lerp(
                        child.target.localPosition, child.targetPosition, positionT);
                if (child.syncRotation)
                    child.target.localRotation = Quaternion.Slerp(
                        child.target.localRotation, child.targetRotation, rotationT);
                if (child.syncScale)
                    child.target.localScale = interpolateScale
                        ? Vector3.Lerp(child.target.localScale, child.targetScale, scaleT)
                        : child.targetScale;
            }
        }

        internal void OnTransformReceived(NetworkTransformData data)
        {
            if (HasAuthority || data == null || data.ObjectId != NetId)
                return;

            bool hasValidSentTime = data.SentTime > 0 &&
                                    !double.IsNaN(data.SentTime) && !double.IsInfinity(data.SentTime);
            // 生成消息和第一帧移动消息可能使用同一个序号；后者有更晚的发送时间。
            if (receivedFirstSnapshot && (data.Sequence < receiveSequence ||
                data.Sequence == receiveSequence &&
                (!hasValidSentTime || data.SentTime <= lastRemoteSentTime)))
                return;

            Vector3 previousPosition = targetPosition;
            double previousSampleTime = lastRemoteSampleTime;
            receiveSequence = data.Sequence;
            lastRemoteSentTime = hasValidSentTime ? data.SentTime : 0;
            targetPosition = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
            targetRotation = NormalizeRotation(new Quaternion(
                data.RotationX, data.RotationY, data.RotationZ, data.RotationW));
            if (data.HasScale)
                targetScale = new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ);

            double now = Time.unscaledTimeAsDouble;
            double sampleTime = now;
            if (hasValidSentTime)
            {
                if (!hasSenderClock)
                {
                    senderToLocalTimeOffset = now - data.SentTime;
                    hasSenderClock = true;
                }
                sampleTime = data.SentTime + senderToLocalTimeOffset;
            }
            if (receivedFirstSnapshot)
                sampleTime = Math.Max(sampleTime, previousSampleTime + 0.001);
            if (data.HasVelocity &&
                !float.IsNaN(data.VelocityX) && !float.IsInfinity(data.VelocityX) &&
                !float.IsNaN(data.VelocityY) && !float.IsInfinity(data.VelocityY) &&
                !float.IsNaN(data.VelocityZ) && !float.IsInfinity(data.VelocityZ))
            {
                targetVelocity = new Vector3(data.VelocityX, data.VelocityY, data.VelocityZ);
            }
            else
            {
                // 生成时的初始快照可能没有速度，从相邻位置估算。
                targetVelocity = receivedFirstSnapshot
                    ? (targetPosition - previousPosition) /
                      Mathf.Max(0.02f, (float)(sampleTime - previousSampleTime))
                    : Vector3.zero;
                targetVelocity = Vector3.ClampMagnitude(targetVelocity, 50f);
            }
            lastRemoteSampleTime = sampleTime;
            if (hasValidSentTime)
                AddRemoteSnapshot(data.SentTime);

            ReadChildSnapshots(data);

            if (!receivedFirstSnapshot)
            {
                if (syncPosition)
                    transform.position = targetPosition;
                renderVelocity = Vector3.zero;
                if (syncRotation)
                    transform.rotation = targetRotation;
                if (syncScale && data.HasScale)
                    transform.localScale = targetScale;
                ApplyFirstChildSnapshots();
                receivedFirstSnapshot = true;
            }
        }

        void AddRemoteSnapshot(double sentTime)
        {
            float bufferTime = Mathf.Max(0.02f, sendInterval) *
                               Mathf.Clamp(snapshotBufferMultiplier, 1f, 3f);
            if (remoteSnapshots.TryGetLatest(out RemoteSnapshot latest) &&
                (targetPosition - latest.position).sqrMagnitude > snapDistance * snapDistance)
                remoteSnapshots.Clear();
            remoteSnapshots.Add(sentTime, new RemoteSnapshot
            {
                position = targetPosition,
                rotation = targetRotation,
                scale = targetScale,
                velocity = targetVelocity
            }, bufferTime);
        }

        bool SampleRemoteSnapshot(out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = targetPosition;
            rotation = targetRotation;
            scale = targetScale;
            if (!remoteSnapshots.TrySample(Time.unscaledDeltaTime, maxPredictionTime,
                    out RemoteSnapshot first, out RemoteSnapshot second,
                    out float t, out float extrapolation)) return false;
            position = Vector3.LerpUnclamped(first.position, second.position, t) +
                       second.velocity * extrapolation;
            rotation = Quaternion.Slerp(first.rotation, second.rotation, t);
            scale = Vector3.Lerp(first.scale, second.scale, t);
            return true;
        }

        void InitializeChildren()
        {
            foreach (ChildTransformSync child in childTransforms)
            {
                if (child?.target == null)
                    continue;

                child.targetPosition = child.target.localPosition;
                child.targetRotation = child.target.localRotation;
                child.targetScale = child.target.localScale;
            }
        }

        bool HaveChildrenChanged()
        {
            foreach (ChildTransformSync child in childTransforms)
            {
                if (child?.target == null)
                    continue;

                if (!child.sentSnapshot ||
                    child.syncPosition && PositionChanged(child.target.localPosition, child.lastSentPosition) ||
                    child.syncRotation && RotationChanged(child.target.localRotation, child.lastSentRotation) ||
                    child.syncScale && ScaleChanged(child.target.localScale, child.lastSentScale))
                    return true;
            }

            return false;
        }

        void AddChildSnapshots(NetworkTransformData snapshot)
        {
            for (int i = 0; i < childTransforms.Length; i++)
            {
                ChildTransformSync child = childTransforms[i];
                if (child?.target == null ||
                    !child.syncPosition && !child.syncRotation && !child.syncScale)
                    continue;
                var data = new NetworkChildTransformData { SyncIndex = i };
                if (child.syncPosition)
                {
                    Vector3 position = child.target.localPosition;
                    data.PositionX = position.x;
                    data.PositionY = position.y;
                    data.PositionZ = position.z;
                }
                if (child.syncRotation)
                {
                    Quaternion rotation = child.target.localRotation;
                    data.RotationX = rotation.x;
                    data.RotationY = rotation.y;
                    data.RotationZ = rotation.z;
                    data.RotationW = rotation.w;
                }
                if (child.syncScale)
                {
                    Vector3 scale = child.target.localScale;
                    data.ScaleX = scale.x;
                    data.ScaleY = scale.y;
                    data.ScaleZ = scale.z;
                }
                snapshot.Children.Add(data);
            }
        }

        void SaveSentChildren()
        {
            foreach (ChildTransformSync child in childTransforms)
            {
                if (child?.target == null)
                    continue;

                child.lastSentPosition = child.target.localPosition;
                child.lastSentRotation = child.target.localRotation;
                child.lastSentScale = child.target.localScale;
                child.sentSnapshot = true;
            }
        }

        void ReadChildSnapshots(NetworkTransformData snapshot)
        {
            foreach (NetworkChildTransformData data in snapshot.Children)
            {
                if (data.SyncIndex < 0 || data.SyncIndex >= childTransforms.Length)
                    continue;

                ChildTransformSync child = childTransforms[data.SyncIndex];
                if (child?.target == null)
                    continue;

                child.targetPosition = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
                child.targetRotation = NormalizeRotation(new Quaternion(
                    data.RotationX, data.RotationY, data.RotationZ, data.RotationW));
                child.targetScale = new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ);
                child.receivedSnapshot = true;
            }
        }

        void ApplyFirstChildSnapshots()
        {
            foreach (ChildTransformSync child in childTransforms)
            {
                if (child?.target == null || !child.receivedSnapshot)
                    continue;

                if (child.syncPosition)
                    child.target.localPosition = child.targetPosition;
                if (child.syncRotation)
                    child.target.localRotation = child.targetRotation;
                if (child.syncScale)
                    child.target.localScale = child.targetScale;
            }
        }

        void OnAuthorityChanged(WebNetworkIdentity changed)
        {
            if (changed != Identity)
                return;

            ResetNetworkState();
            if (HasAuthority && aiLerp != null)
                aiLerp.Teleport(transform.position);
        }

        internal void ResetNetworkState()
        {
            sendSequence = 0;
            receiveSequence = 0;
            sentFirstSnapshot = false;
            receivedFirstSnapshot = false;
            wasSoloAi = false;
            hasSenderClock = false;
            senderToLocalTimeOffset = 0;
            lastRemoteSampleTime = 0;
            lastRemoteSentTime = 0;
            renderVelocity = Vector3.zero;
            remoteSnapshots.Clear();
            if (body != null)
            {
                // 远端物体只接受网络驱动，避免动态刚体在物理步进中推回显示位置。
                body.bodyType = HasAuthority ? originalBodyType : RigidbodyType2D.Kinematic;
                body.interpolation = HasAuthority
                    ? (Identity.IsPlayerObject ? RigidbodyInterpolation2D.Interpolate : originalBodyInterpolation)
                    : RigidbodyInterpolation2D.None;
                if (!HasAuthority)
                    body.velocity = Vector2.zero;
            }
            if (aiLerp != null)
                aiLerp.canMove = HasAuthority;
            // 同批生成的 AI 错开发送，避免每隔固定时间集中序列化大量快照。
            nextSendTime = Identity.IsAiObject && NetId != 0
                ? Time.unscaledTime + (NetId % 10) * 0.01f : 0f;
            lastSendTime = 0f;
            repeatPositionUntil = 0f;
            lastVelocitySampleTime = 0f;
            targetPosition = transform.position;
            targetVelocity = Vector3.zero;
            targetRotation = transform.rotation;
            targetScale = transform.localScale;
            lastSentPosition = transform.position;
            lastSentVelocity = Vector3.zero;
            lastVelocitySamplePosition = transform.position;
            lastSentRotation = transform.rotation;
            lastSentScale = transform.localScale;
            foreach (ChildTransformSync child in childTransforms)
            {
                if (child?.target == null)
                    continue;

                child.targetPosition = child.target.localPosition;
                child.targetRotation = child.target.localRotation;
                child.targetScale = child.target.localScale;
                child.lastSentPosition = child.target.localPosition;
                child.lastSentRotation = child.target.localRotation;
                child.lastSentScale = child.target.localScale;
                child.sentSnapshot = false;
                child.receivedSnapshot = false;
            }
        }

        /// <summary>
        /// 立即发送当前 Transform 快照。用于切图后设置出生点，避免等待常规发送间隔。
        /// </summary>
        public void SendImmediately()
        {
            if (!HasAuthority)
                return;

            sentFirstSnapshot = false;
            nextSendTime = 0f;
            UpdateAuthorityObject();
            WebNetworkManager.FlushTransformsImmediately();
        }

        bool PositionChanged(Vector3 current, Vector3 previous)
        {
            return Vector3.SqrMagnitude(current - previous) >= positionThreshold * positionThreshold;
        }

        bool ScaleChanged(Vector3 current, Vector3 previous)
        {
            return Vector3.SqrMagnitude(current - previous) >= scaleThreshold * scaleThreshold;
        }

        bool RotationChanged(Quaternion current, Quaternion previous)
        {
            return Quaternion.Angle(current, previous) >= rotationThreshold;
        }

        float ExpLerpFactor(float speed)
        {
            return 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        }

        static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float lengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                  rotation.z * rotation.z + rotation.w * rotation.w;
            return lengthSquared > 0.0001f ? rotation.normalized : Quaternion.identity;
        }
    }
}
