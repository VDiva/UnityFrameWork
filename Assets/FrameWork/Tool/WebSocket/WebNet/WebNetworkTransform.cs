using System;
using System.Collections.Generic;
using GameData;
using UnityEngine;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// 同步网络对象根节点的位置、旋转、缩放，以及指定子节点的本地变换。
    /// 有控制权的一端上传，其他客户端平滑插值到收到的目标状态。
    /// </summary>
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

        [Header("远端插值")]
        [Tooltip("缓存少量快照吸收网络抖动；默认增加约150ms远端显示延迟。")]
        [SerializeField, Range(0.05f, 0.3f)] float snapshotBufferTime = 0.15f;
        [SerializeField, Range(2, 10)] int maxBufferedSnapshots = 6;
        [SerializeField, Min(0.1f)] float interpolationSpeed = 12f;
        [SerializeField, Min(0.1f)] float rotationInterpolationSpeed = 15f;
        [Tooltip("开启时平滑同步根节点和子节点缩放；关闭时收到数据后立即设置缩放。")]
        [SerializeField] bool interpolateScale = true;
        [SerializeField, Min(0.1f)] float scaleInterpolationSpeed = 12f;

        Vector3 targetPosition;
        Quaternion targetRotation;
        Vector3 targetScale;
        Vector3 lastSentPosition;
        Quaternion lastSentRotation;
        Vector3 lastSentScale;
        float nextSendTime;
        float lastSendTime;
        ulong sendSequence;
        ulong receiveSequence;
        bool receivedFirstSnapshot;
        bool sentFirstSnapshot;

        readonly List<RemoteSnapshot> remoteSnapshots = new List<RemoteSnapshot>(6);

        struct RemoteSnapshot
        {
            public float receivedTime;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public bool hasScale;
        }

        void Awake()
        {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            targetScale = transform.localScale;
            InitializeChildren();
        }

        void OnEnable()
        {
            WebNetworkManager.TransformReceived += OnTransformReceived;
            WebNetworkManager.AuthorityChanged += OnAuthorityChanged;
        }

        void OnDisable()
        {
            WebNetworkManager.TransformReceived -= OnTransformReceived;
            WebNetworkManager.AuthorityChanged -= OnAuthorityChanged;
        }

        void Update()
        {
            if (HasAuthority)
                UpdateAuthorityObject();
            else
                UpdateRemoteObject();
        }

        void UpdateAuthorityObject()
        {
            if (!LobbyWebNet.IsConnected || NetId == 0 || Time.unscaledTime < nextSendTime)
                return;

            nextSendTime = Time.unscaledTime + sendInterval;
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            Vector3 scale = transform.localScale;

            bool changed = !sentFirstSnapshot ||
                           syncPosition && PositionChanged(position, lastSentPosition) ||
                           syncRotation && RotationChanged(rotation, lastSentRotation) ||
                           syncScale && ScaleChanged(scale, lastSentScale) ||
                           HaveChildrenChanged();

            if (!changed && Time.unscaledTime - lastSendTime < heartbeatInterval)
                return;

            var snapshot = new NetworkTransformData
            {
                ObjectId = NetId,
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
                Sequence = ++sendSequence
            };

            AddChildSnapshots(snapshot);
            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkTransform,
                NetworkTransform = snapshot
            });

            lastSentPosition = position;
            lastSentRotation = rotation;
            lastSentScale = scale;
            SaveSentChildren();
            lastSendTime = Time.unscaledTime;
            sentFirstSnapshot = true;
        }

        void UpdateRemoteObject()
        {
            if (!receivedFirstSnapshot)
                return;

            float positionT = ExpLerpFactor(interpolationSpeed);
            float rotationT = ExpLerpFactor(rotationInterpolationSpeed);
            float scaleT = ExpLerpFactor(scaleInterpolationSpeed);

            if (!ApplyBufferedRootSnapshot())
            {
                // 首帧或缓冲耗尽时，平滑收敛到最后的位置，不无限预测。
                if (syncPosition)
                    transform.position = Vector3.Lerp(transform.position, targetPosition, positionT);
                if (syncRotation)
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationT);
                if (syncScale)
                    transform.localScale = interpolateScale
                        ? Vector3.Lerp(transform.localScale, targetScale, scaleT)
                        : targetScale;
            }

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

        bool ApplyBufferedRootSnapshot()
        {
            float renderTime = Time.unscaledTime - snapshotBufferTime;
            while (remoteSnapshots.Count >= 2 && remoteSnapshots[1].receivedTime <= renderTime)
                remoteSnapshots.RemoveAt(0);
            if (remoteSnapshots.Count < 2) return false;

            RemoteSnapshot from = remoteSnapshots[0];
            RemoteSnapshot to = remoteSnapshots[1];
            float duration = Mathf.Max(0.001f, to.receivedTime - from.receivedTime);
            float t = Mathf.Clamp01((renderTime - from.receivedTime) / duration);
            if (syncPosition)
                transform.position = Vector3.Lerp(from.position, to.position, t);
            if (syncRotation)
                transform.rotation = Quaternion.Slerp(from.rotation, to.rotation, t);
            if (syncScale && to.hasScale)
                transform.localScale = interpolateScale ? Vector3.Lerp(from.scale, to.scale, t) : to.scale;
            return true;
        }

        void OnTransformReceived(NetworkTransformData data)
        {
            if (HasAuthority || data == null || data.ObjectId != NetId || data.Sequence <= receiveSequence)
                return;

            receiveSequence = data.Sequence;
            targetPosition = new Vector3(data.PositionX, data.PositionY, data.PositionZ);
            targetRotation = NormalizeRotation(new Quaternion(
                data.RotationX, data.RotationY, data.RotationZ, data.RotationW));
            if (data.HasScale)
                targetScale = new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ);

            remoteSnapshots.Add(new RemoteSnapshot
            {
                receivedTime = Time.unscaledTime,
                position = targetPosition,
                rotation = targetRotation,
                scale = targetScale,
                hasScale = data.HasScale
            });
            while (remoteSnapshots.Count > Mathf.Clamp(maxBufferedSnapshots, 2, 10))
                remoteSnapshots.RemoveAt(0);

            ReadChildSnapshots(data);

            if (!receivedFirstSnapshot)
            {
                if (syncPosition)
                    transform.position = targetPosition;
                if (syncRotation)
                    transform.rotation = targetRotation;
                if (syncScale && data.HasScale)
                    transform.localScale = targetScale;
                ApplyFirstChildSnapshots();
                receivedFirstSnapshot = true;
            }
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
                if (child?.target == null)
                    continue;

                Vector3 position = child.target.localPosition;
                Quaternion rotation = child.target.localRotation;
                Vector3 scale = child.target.localScale;
                snapshot.Children.Add(new NetworkChildTransformData
                {
                    SyncIndex = i,
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    RotationX = rotation.x,
                    RotationY = rotation.y,
                    RotationZ = rotation.z,
                    RotationW = rotation.w,
                    ScaleX = scale.x,
                    ScaleY = scale.y,
                    ScaleZ = scale.z
                });
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
        }

        internal void ResetNetworkState()
        {
            sendSequence = 0;
            receiveSequence = 0;
            sentFirstSnapshot = false;
            receivedFirstSnapshot = false;
            remoteSnapshots.Clear();
            nextSendTime = 0f;
            lastSendTime = 0f;
            targetPosition = transform.position;
            targetRotation = transform.rotation;
            targetScale = transform.localScale;
            lastSentPosition = transform.position;
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
