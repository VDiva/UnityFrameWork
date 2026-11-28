using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FrameWork;
using GameData;
using Google.Protobuf;
using FrameWork.Script.Mrg;
using FrameWork.Script.Role;
using UnityEngine;
using UnityWebSocket;
using FrameWork.Net;
using FrameWork.Net.Transport;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// 负责解析 GameNet 大厅协议，以及按照服务端 ObjectId 生成和销毁大厅玩家。
    /// 集中驱动所有活动 NetworkTransform 与 NetworkSyncVars，消除大量对象的独立生命周期跨界开销。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class NetworkManager : MonoBehaviour
    {
        static readonly List<NetworkTransform> tickableTransforms = new List<NetworkTransform>(128);
        static readonly List<NetworkSyncVars> tickableSyncVars = new List<NetworkSyncVars>(64);
        static readonly List<NetworkAnimator> tickableAnimators = new List<NetworkAnimator>(64);
        static readonly List<NetworkSpineAnimator> tickableSpineAnimators = new List<NetworkSpineAnimator>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTickRegistries()
        {
            tickableTransforms.Clear();
            tickableSyncVars.Clear();
            tickableAnimators.Clear();
            tickableSpineAnimators.Clear();
            Instance = null;
        }

        internal static void RegisterTickableTransform(NetworkTransform item)
        {
            if (item == null || item.tickIndex >= 0) return;
            item.tickIndex = tickableTransforms.Count;
            tickableTransforms.Add(item);
        }

        internal static void UnregisterTickableTransform(NetworkTransform item)
        {
            if (item == null || item.tickIndex < 0 || item.tickIndex >= tickableTransforms.Count)
                return;
            int idx = item.tickIndex;
            int last = tickableTransforms.Count - 1;
            if (idx != last)
            {
                NetworkTransform moved = tickableTransforms[last];
                tickableTransforms[idx] = moved;
                if (moved != null) moved.tickIndex = idx;
            }
            tickableTransforms.RemoveAt(last);
            item.tickIndex = -1;
        }

        internal static void RegisterTickableSyncVars(NetworkSyncVars item)
        {
            if (item == null || item.tickIndex >= 0) return;
            item.tickIndex = tickableSyncVars.Count;
            tickableSyncVars.Add(item);
        }

        internal static void UnregisterTickableSyncVars(NetworkSyncVars item)
        {
            if (item == null || item.tickIndex < 0 || item.tickIndex >= tickableSyncVars.Count)
                return;
            int idx = item.tickIndex;
            int last = tickableSyncVars.Count - 1;
            if (idx != last)
            {
                NetworkSyncVars moved = tickableSyncVars[last];
                tickableSyncVars[idx] = moved;
                if (moved != null) moved.tickIndex = idx;
            }
            tickableSyncVars.RemoveAt(last);
            item.tickIndex = -1;
        }

        internal static void RegisterTickableAnimator(NetworkAnimator item)
        {
            if (item == null || item.tickIndex >= 0) return;
            item.tickIndex = tickableAnimators.Count;
            tickableAnimators.Add(item);
        }

        internal static void UnregisterTickableAnimator(NetworkAnimator item)
        {
            if (item == null || item.tickIndex < 0 || item.tickIndex >= tickableAnimators.Count)
                return;
            int idx = item.tickIndex;
            int last = tickableAnimators.Count - 1;
            if (idx != last)
            {
                NetworkAnimator moved = tickableAnimators[last];
                tickableAnimators[idx] = moved;
                if (moved != null) moved.tickIndex = idx;
            }
            tickableAnimators.RemoveAt(last);
            item.tickIndex = -1;
        }

        internal static void RegisterTickableSpineAnimator(NetworkSpineAnimator item)
        {
            if (item == null || item.tickIndex >= 0) return;
            item.tickIndex = tickableSpineAnimators.Count;
            tickableSpineAnimators.Add(item);
        }

        internal static void UnregisterTickableSpineAnimator(NetworkSpineAnimator item)
        {
            if (item == null || item.tickIndex < 0 || item.tickIndex >= tickableSpineAnimators.Count)
                return;
            int idx = item.tickIndex;
            int last = tickableSpineAnimators.Count - 1;
            if (idx != last)
            {
                NetworkSpineAnimator moved = tickableSpineAnimators[last];
                tickableSpineAnimators[idx] = moved;
                if (moved != null) moved.tickIndex = idx;
            }
            tickableSpineAnimators.RemoveAt(last);
            item.tickIndex = -1;
        }
        [Serializable]
        public sealed class NetworkPrefabEntry
        {
            public string prefabId;
            public GameObject prefab;
        }

        [SerializeField] bool persistAcrossScenes = true;
        [SerializeField] NetworkIdentity localPlayer;
        [SerializeField] GameObject remotePlayerPrefab;
        [SerializeField] Transform remotePlayerRoot;
        [SerializeField] NetworkPrefabEntry[] networkPrefabs = Array.Empty<NetworkPrefabEntry>();

        readonly Dictionary<uint, NetworkIdentity> objects = new Dictionary<uint, NetworkIdentity>();
        readonly Dictionary<uint, NetworkTransformData> queuedTransforms =
            new Dictionary<uint, NetworkTransformData>();
        readonly Dictionary<uint, SpawnRecord> spawnRecords = new Dictionary<uint, SpawnRecord>();
        readonly Dictionary<string, UniTaskCompletionSource<NetworkIdentity>> pendingSpawns =
            new Dictionary<string, UniTaskCompletionSource<NetworkIdentity>>();
        readonly Dictionary<string, string> pendingSpawnTriggers = new Dictionary<string, string>();
        readonly Dictionary<string, NetworkTransformData> pendingSpawnInitialTransforms =
            new Dictionary<string, NetworkTransformData>();
        // 切图后玩家对象通过对象池异步创建。位置包可能先到达，暂存每个对象的最新快照，
        // 等 RegisterRoomPlayer 完成后立即派发，避免必须等待下一次网络心跳。
        readonly Dictionary<uint, NetworkTransformData> pendingTransformSnapshots =
            new Dictionary<uint, NetworkTransformData>();
        // 动画可能在角色异步创建完成前到达。按对象和 Track 保存最新一帧，
        // 注册完成后立即派发，避免短动画永久丢失。
        readonly Dictionary<uint, Dictionary<int, NetworkAnimationData>> pendingAnimationSnapshots =
            new Dictionary<uint, Dictionary<int, NetworkAnimationData>>();
        // 异步加载预制体期间只保留每个字段的最新版本，防止晚加入时初始状态丢失。
        readonly Dictionary<uint, Dictionary<ulong, NetworkSyncVarData>> pendingSyncVarSnapshots =
            new Dictionary<uint, Dictionary<ulong, NetworkSyncVarData>>();
        // RPC 可能在 Addressables/对象池异步创建网络对象期间先到达。按 ObjectId 暂存，
        // 等身份注册及业务出生处理完成后再按原始顺序执行。
        readonly Dictionary<uint, Queue<NetworkRpcData>> pendingRpcs =
            new Dictionary<uint, Queue<NetworkRpcData>>();
        const int MaxPendingRpcsPerObject = 32;
        const int MaxPendingObjects = 256;
        readonly Dictionary<uint, List<NetworkTransformData>> transformBatchScratch =
            new Dictionary<uint, List<NetworkTransformData>>();
        readonly List<NetworkTransformData> decodedCompactTransforms = new List<NetworkTransformData>(64);
        uint pendingLocalObjectId;
        bool backgroundQualityProbing;

        sealed class SpawnRecord
        {
            public NetworkObjectData Data;
        }

        public static NetworkManager Instance { get; private set; }
        public NetworkIdentity LocalPlayer => localPlayer;

        public static event Action<NetworkTransformData> TransformReceived;
        public static event Action<NetworkAnimationData> AnimationReceived;
        public static event Action<NetworkSyncVarData> SyncVarReceived;
        public static event Action<NetworkIdentity,NetworkObjectData,Vector3,Quaternion> ObjectSpawned;
        public static event Action<uint> ObjectDespawned;
        public static event Action<NetworkIdentity> AuthorityChanged;
        public static event Action<Msg> ServerMessageReceived;
        /// <summary>自动装备分配结果：EquipmentRollScores 为每名玩家每轮的点数，Id 为获胜者 UserId。</summary>
        public static event Action<Msg> EquipmentRollResultReceived;
        /// <summary>掉落物ObjectId、获胜玩家ObjectId、获胜玩家UserId、本次请求是否成功。</summary>
        public static event Action<uint, uint, string, bool> ObjectClaimResultReceived;

        void DispatchTransform(NetworkTransformData snapshot)
        {
            if (snapshot == null) return;
            if (objects.TryGetValue(snapshot.ObjectId, out NetworkIdentity identity) && identity != null)
            {
                identity.EnsureComponentCache();
                NetworkTransform networkTransform = identity.CachedTransform;
                if (networkTransform != null && networkTransform.isActiveAndEnabled)
                    networkTransform.OnTransformReceived(snapshot);
            }
            // 监测面板等观察者仍可订阅；网络对象自身不再逐个订阅此事件。
            TransformReceived?.Invoke(snapshot);
        }

        void HandleTransformSnapshot(NetworkTransformData snapshot)
        {
            if (snapshot == null) return;
            NetworkRoomData room = NetworkRoomManager.Instance?.CurrentRoom;
            if (room == null || snapshot.RoomId != room.RoomId ||
                snapshot.SceneEpoch != room.SceneEpoch) return;
            if (objects.ContainsKey(snapshot.ObjectId))
                DispatchTransform(snapshot);
            else if (pendingTransformSnapshots.ContainsKey(snapshot.ObjectId) ||
                     pendingTransformSnapshots.Count < MaxPendingObjects)
                pendingTransformSnapshots[snapshot.ObjectId] = snapshot.Clone();
        }

        void DispatchAnimation(NetworkAnimationData snapshot)
        {
            if (snapshot == null) return;
            if (objects.TryGetValue(snapshot.ObjectId, out NetworkIdentity identity) && identity != null)
            {
                identity.EnsureComponentCache();
                NetworkAnimator animator = identity.CachedAnimator;
                if (animator != null && animator.isActiveAndEnabled)
                    animator.OnAnimationReceived(snapshot);
                NetworkSpineAnimator spineAnimator = identity.CachedSpineAnimator;
                if (spineAnimator != null && spineAnimator.isActiveAndEnabled)
                    spineAnimator.OnAnimationReceived(snapshot);
            }
            AnimationReceived?.Invoke(snapshot);
        }

        void DispatchSyncVar(NetworkSyncVarData snapshot)
        {
            if (snapshot == null) return;
            if (objects.TryGetValue(snapshot.ObjectId, out NetworkIdentity identity) && identity != null)
            {
                identity.EnsureComponentCache();
                NetworkSyncVars syncVars = identity.CachedSyncVars;
                if (syncVars != null && syncVars.isActiveAndEnabled)
                    syncVars.OnSyncVarReceived(snapshot);
            }
            SyncVarReceived?.Invoke(snapshot);
        }

        void HandleAnimationSnapshot(NetworkAnimationData snapshot)
        {
            if (snapshot == null) return;
            if (objects.ContainsKey(snapshot.ObjectId))
                DispatchAnimation(snapshot);
            else
                BufferPendingAnimation(snapshot);
        }

        void HandleSyncVarSnapshot(NetworkSyncVarData snapshot)
        {
            if (snapshot == null) return;
            if (objects.ContainsKey(snapshot.ObjectId))
                DispatchSyncVar(snapshot);
            else
                BufferPendingSyncVar(snapshot);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || FRAMEWORK_SHOW_LATENCY
            if (GetComponent<NetworkLatencyOverlay>() == null)
                gameObject.AddComponent<NetworkLatencyOverlay>();
#endif
        }

        void OnEnable()
        {
            GameNet.OnParsedMessage += OnWebMessage;
            GameNet.OnClose += OnConnectionClosed;
        }

        void OnDisable()
        {
            GameNet.OnParsedMessage -= OnWebMessage;
            GameNet.OnClose -= OnConnectionClosed;
            queuedTransforms.Clear();
            ClearRemoteObjects();
        }

        void OnConnectionClosed(object sender, CloseEventArgs args)
        {
            queuedTransforms.Clear();
            CancelPendingSpawns();
            pendingTransformSnapshots.Clear();
            pendingAnimationSnapshots.Clear();
            pendingSyncVarSnapshots.Clear();
            pendingRpcs.Clear();
            RoomSharedValues.Clear();
        }

        public static void SendTransform(NetworkTransformData snapshot, bool isPlayerObject = false)
        {
            if (snapshot == null || !GameNet.IsConnected) return;
            NetworkManager manager = Instance;
            // 玩家位置先发，AI 仍按帧打包，避免大量 AI 快照挡在玩家位置前。
            if (!isPlayerObject && manager != null && manager.isActiveAndEnabled)
                manager.queuedTransforms[snapshot.ObjectId] = snapshot;
            else
                GameNet.SendLatestDeferred(
                    new DeferredSingleTransformPayload(snapshot),
                    snapshot.ObjectId,
                    maxAgeMs: 250);
        }

        public static void SendAnimation(NetworkAnimationData snapshot)
        {
            if (snapshot == null || !GameNet.IsConnected) return;
            ulong key = 0x8000000000000000UL | ((ulong)snapshot.ObjectId << 31) |
                        ((uint)snapshot.TrackIndex & 0x7FFFFFFFUL);
            GameNet.SendLatest(new Msg { MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UploadNetworkAnimation, NetworkAnimation = snapshot }, key);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            NetworkRoomManager roomManager = NetworkRoomManager.Instance;
            bool soloRoom = roomManager != null && roomManager.CurrentRoom != null &&
                            roomManager.CurrentRoom.Members.Count <= 1;

            for (int i = 0; i < tickableTransforms.Count; i++)
            {
                NetworkTransform item = tickableTransforms[i];
                if (item != null && item.isActiveAndEnabled)
                    item.TickAuthority(now);
            }
            for (int i = 0; i < tickableSyncVars.Count; i++)
            {
                NetworkSyncVars item = tickableSyncVars[i];
                if (item != null && item.isActiveAndEnabled)
                    item.TickAuthority(now);
            }
            for (int i = 0; i < tickableAnimators.Count; i++)
            {
                NetworkAnimator item = tickableAnimators[i];
                if (item != null && item.isActiveAndEnabled)
                    item.TickAuthority(now, soloRoom);
            }
            for (int i = 0; i < tickableSpineAnimators.Count; i++)
            {
                NetworkSpineAnimator item = tickableSpineAnimators[i];
                if (item != null && item.isActiveAndEnabled)
                    item.TickAuthority(now, soloRoom);
            }

            // 正式包未启用调试延迟浮层时，仍每 4 秒后台探测一次以维持 NTP 时钟同步与拥塞自适应。
            if (GameNet.IsConnected && !backgroundQualityProbing &&
                Time.realtimeSinceStartupAsDouble - NetworkQualityMonitor.LastProbeTime >= 4.0)
            {
                ProbeNetworkQualityAsync().Forget();
            }
        }

        async UniTaskVoid ProbeNetworkQualityAsync()
        {
            backgroundQualityProbing = true;
            try { await GameNet.MeasureServerLatencyAsync(3f); }
            catch { }
            finally { backgroundQualityProbing = false; }
        }

        void LateUpdate()
        {
            FlushQueuedTransforms();
            GameNet.FlushRealtime();
            for (int i = 0; i < tickableTransforms.Count; i++)
            {
                NetworkTransform item = tickableTransforms[i];
                if (item != null && item.isActiveAndEnabled)
                    item.TickRemote();
            }
        }

        public static void FlushTransformsImmediately()
        {
            if (Instance != null) Instance.FlushQueuedTransforms();
        }

        void FlushQueuedTransforms()
        {
            if (queuedTransforms.Count == 0) return;
            if (!GameNet.IsConnected)
            {
                queuedTransforms.Clear();
                return;
            }
            // WebGL 的浏览器队列无法覆盖旧包；积压时把 AI 最新状态留到下一帧。
            if (GameNet.BufferedSendBytes >= 16 * 1024) return;
            // 固定对象 ID 分组，使尚未写入 socket 的批次可以按对象合并最新位置。
            // 每组最多 128 个不同 ID，符合服务端的单包处理上限。
            transformBatchScratch.Clear();
            foreach (NetworkTransformData snapshot in queuedTransforms.Values)
            {
                uint groupId = snapshot.ObjectId >> 7;
                if (!transformBatchScratch.TryGetValue(groupId, out List<NetworkTransformData> batch))
                {
                    batch = new List<NetworkTransformData>(Math.Min(queuedTransforms.Count, 128));
                    transformBatchScratch.Add(groupId, batch);
                }
                batch.Add(snapshot);
            }
            foreach (KeyValuePair<uint, List<NetworkTransformData>> batch in transformBatchScratch)
                GameNet.SendLatestDeferred(new DeferredTransformBatchPayload(batch.Value),
                    0x4000000000000000UL | batch.Key, maxAgeMs: 250);
            transformBatchScratch.Clear();
            queuedTransforms.Clear();
        }

        sealed class DeferredSingleTransformPayload : IDeferredNetPayload
        {
            readonly NetworkTransformData snapshot;

            public DeferredSingleTransformPayload(NetworkTransformData snapshot)
            {
                this.snapshot = snapshot;
            }

            public int EstimatedBytes => 32;

            public byte[] Encode()
            {
                return new Msg
                {
                    MsgType = ProtobufMsgType.Game,
                    GameMsgType = GameMsgType.UploadNetworkTransform,
                    CompactTransforms = CompactTransformCodec.EncodeSingle(snapshot)
                }.ToByteArray();
            }

            public IDeferredNetPayload MergeFrom(IDeferredNetPayload older) => this;
        }

        sealed class DeferredTransformBatchPayload : IDeferredNetPayload
        {
            readonly List<NetworkTransformData> snapshots;

            public DeferredTransformBatchPayload(List<NetworkTransformData> snapshots)
            {
                this.snapshots = snapshots;
            }

            public int EstimatedBytes => Math.Max(24, snapshots.Count * 28);

            public byte[] Encode()
            {
                return new Msg
                {
                    MsgType = ProtobufMsgType.Game,
                    GameMsgType = GameMsgType.UploadNetworkTransform,
                    CompactTransforms = CompactTransformCodec.EncodeBatch(snapshots)
                }.ToByteArray();
            }

            public IDeferredNetPayload MergeFrom(IDeferredNetPayload older)
            {
                if (!(older is DeferredTransformBatchPayload prev) || prev.snapshots.Count == 0)
                    return this;

                List<NetworkTransformData> merged = prev.snapshots;
                Dictionary<uint, int> indexes = mergeIndexScratch;
                if (indexes == null)
                {
                    indexes = new Dictionary<uint, int>(Math.Max(16, merged.Count));
                    mergeIndexScratch = indexes;
                }
                else
                {
                    indexes.Clear();
                }

                for (int i = 0; i < merged.Count; i++)
                    indexes[merged[i].ObjectId] = i;

                for (int i = 0; i < snapshots.Count; i++)
                {
                    NetworkTransformData snapshot = snapshots[i];
                    if (indexes.TryGetValue(snapshot.ObjectId, out int idx))
                        merged[idx] = snapshot;
                    else
                    {
                        indexes[snapshot.ObjectId] = merged.Count;
                        merged.Add(snapshot);
                    }
                }

                indexes.Clear();
                return prev;
            }
        }

        [ThreadStatic] static Dictionary<uint, int> mergeIndexScratch;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetLocalPlayer(NetworkIdentity identity)
        {
            if (localPlayer == identity)
            {
                if (localPlayer != null && pendingLocalObjectId != 0 &&
                    localPlayer.ObjectId != pendingLocalObjectId)
                    ConfigureLocalPlayer(pendingLocalObjectId);
                return;
            }

            if (localPlayer != null && localPlayer.ObjectId != 0)
                objects.Remove(localPlayer.ObjectId);

            localPlayer = identity;
            if (localPlayer != null && pendingLocalObjectId != 0)
                ConfigureLocalPlayer(pendingLocalObjectId);
        }

        /// <summary>
        /// 把业务层通过 Addressables、对象池或 Instantiate 创建的对象注册为本地网络玩家。
        /// 可在收到本地玩家的 RoomMemberJoined 后调用；登录时提前收到的 ObjectId 会自动绑定到该对象。
        /// </summary>
        public NetworkIdentity RegisterLocalPlayer(GameObject playerObject)
        {
            if (playerObject == null)
            {
                Debug.LogError("[Network] 注册本地玩家失败：playerObject 为空。");
                return null;
            }

            NetworkIdentity identity = playerObject.GetComponent<NetworkIdentity>();
            if (identity == null)
                identity = playerObject.AddComponent<NetworkIdentity>();

            NetworkLocalPlayer marker = playerObject.GetComponent<NetworkLocalPlayer>();
            if (marker == null)
            {
                // AddComponent 会立即执行 OnEnable，并在其中自动 Register，不能再重复调用。
                marker = playerObject.AddComponent<NetworkLocalPlayer>();
            }
            else
            {
                marker.Register();
            }
            return identity;
        }

        /// <summary>
        /// 注册由业务层在 RoomMemberJoined 中创建的玩家角色。
        /// 玩家使用什么预制体完全由客户端业务决定，服务端只提供 PlayerId 和 ObjectId。
        /// </summary>
        public NetworkIdentity RegisterRoomPlayer(GameObject playerObject, NetworkRoomMemberData member)
        {
            if (playerObject == null || member == null || member.ObjectId == 0)
            {
                Debug.LogError("[Network] 注册房间玩家失败：对象、成员数据或 ObjectId 无效。");
                return null;
            }

            bool isLocal = string.Equals(member.PlayerId, GameNet.CurrentUserId,
                StringComparison.Ordinal);
            NetworkIdentity identity = playerObject.GetComponent<NetworkIdentity>();
            if (identity == null)
                identity = playerObject.AddComponent<NetworkIdentity>();

            if (objects.TryGetValue(member.ObjectId, out NetworkIdentity oldIdentity) &&
                oldIdentity != null && oldIdentity != identity)
                GameObjectMrg.Instance.Enqueue(oldIdentity.gameObject);

            identity.Configure(member.ObjectId, member.PlayerId, isLocal,
                string.Empty, member.PlayerId, true, false);
            objects[member.ObjectId] = identity;

            if (isLocal)
            {
                pendingLocalObjectId = member.ObjectId;
                localPlayer = identity;
            }

            FlushPendingSyncVars(member.ObjectId);
            ObjectSpawned?.Invoke(identity,null,Vector3.zero,Quaternion.identity);
            if (pendingTransformSnapshots.TryGetValue(member.ObjectId, out NetworkTransformData pendingTransform))
            {
                pendingTransformSnapshots.Remove(member.ObjectId);
                DispatchTransform(pendingTransform);
            }
            FlushPendingAnimations(member.ObjectId);
            FlushPendingRpcs(member.ObjectId);
            return identity;
        }
        
        public void ClearLocalPlayer(NetworkIdentity identity)
        {
            if (identity == null || localPlayer != identity)
                return;

            if (localPlayer.ObjectId != 0)
                objects.Remove(localPlayer.ObjectId);
            localPlayer = null;
        }

        public bool TryGetObject(uint objectId, out NetworkIdentity identity)
        {
            return objects.TryGetValue(objectId, out identity);
        }

        /// <summary>
        /// 获取当前客户端场景中已注册完成的全部网络对象快照。
        /// 返回新列表，外部增删列表不会影响管理器内部对象字典。
        /// </summary>
        public List<NetworkIdentity> GetAllNetworkObjects()
        {
            var result = new List<NetworkIdentity>(objects.Count);
            foreach (NetworkIdentity identity in objects.Values)
            {
                if (identity != null)
                    result.Add(identity);
            }
            return result;
        }

        /// <summary>
        /// 重放已经生成的非玩家网络对象。场景业务晚于网络消息创建时，可用它补做挂父节点和业务初始化。
        /// </summary>
        public void ReplaySpawnedObjects(
            Action<NetworkIdentity, NetworkObjectData, Vector3, Quaternion> callback)
        {
            if (callback == null)
                return;

            foreach (KeyValuePair<uint, SpawnRecord> pair in
                     new List<KeyValuePair<uint, SpawnRecord>>(spawnRecords))
            {
                if (!objects.TryGetValue(pair.Key, out NetworkIdentity identity) || identity == null)
                    continue;

                SpawnRecord record = pair.Value;
                // 对象可能在场景业务加载前已经收到 Transform 快照，重放时采用当前世界位置，
                // 避免用最初出生点把已经同步过的位置回滚。
                callback(identity, record.Data?.Clone(), identity.transform.position,
                    identity.transform.rotation);
            }
        }

        /// <summary>请求在当前游戏房间生成网络物体，netId 由服务端分配。</summary>
        public void SpawnRoomObject(string prefabId, Vector3 position, Quaternion rotation,RoleType roleType=RoleType.Other)
        {
            if (!GameNet.IsConnected || string.IsNullOrWhiteSpace(prefabId)) return;
            GameNet.Send(new Msg { MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.SpawnNetworkRoomObject,
                NetworkObject = new NetworkObjectData { PrefabId = prefabId.Trim(),RoleType =(int)roleType },
                NetworkTransform = new NetworkTransformData { PositionX = position.x, PositionY = position.y,
                    PositionZ = position.z, RotationX = rotation.x, RotationY = rotation.y,
                    RotationZ = rotation.z, RotationW = rotation.w, Sequence = 1 } });
        }

        /// <summary>
        /// 请求生成普通房间网络对象，并在本客户端完成实例化和网络身份绑定后返回它。
        /// </summary>
        public async UniTask<NetworkIdentity> SpawnRoomObjectAsync(
            string prefabId, Vector3 position, Quaternion rotation,
            RoleType roleType = RoleType.Other, EquipData equipmentDrop = null)
        {
            if (!GameNet.IsConnected)
                throw new InvalidOperationException("尚未连接到游戏服务器，无法生成网络对象。");
            if (NetworkRoomManager.Instance == null ||
                NetworkRoomManager.Instance.CurrentRoom == null ||
                NetworkRoomManager.Instance.CurrentRoom.RoomId <= NetworkRoomManager.LobbyRoomId ||
                NetworkRoomManager.Instance.IsLoadingRoom)
                throw new InvalidOperationException(
                    "当前尚未完整进入游戏房间，服务器不允许生成房间网络对象。");
            if (string.IsNullOrWhiteSpace(prefabId))
                throw new ArgumentException("网络预制体 ID 不能为空。", nameof(prefabId));

            string requestId = Guid.NewGuid().ToString("N");
            var completion = new UniTaskCompletionSource<NetworkIdentity>();
            pendingSpawns.Add(requestId, completion);
            pendingSpawnInitialTransforms.Add(requestId, new NetworkTransformData
            {
                PositionX = position.x,
                PositionY = position.y,
                PositionZ = position.z,
                RotationX = rotation.x,
                RotationY = rotation.y,
                RotationZ = rotation.z,
                RotationW = rotation.w,
                Sequence = 1
            });

            //Debug.Log($"[Network][Spawn] 发送生成请求 prefabId={prefabId.Trim()}, requestId={requestId}");

            GameNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.SpawnNetworkRoomObject,
                NetworkObject = new NetworkObjectData
                {
                    PrefabId = prefabId.Trim(),
                    RoleType = (int)roleType,
                    SpawnRequestId = requestId,
                    EquipmentDrop = equipmentDrop
                },
                NetworkTransform = new NetworkTransformData
                {
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    RotationX = rotation.x,
                    RotationY = rotation.y,
                    RotationZ = rotation.z,
                    RotationW = rotation.w,
                    Sequence = 1
                }
            });

            try
            {
                return await GameNet.AwaitWithTimeoutAsync(completion, 10f);
            }
            finally
            {
                pendingSpawns.Remove(requestId);
                pendingSpawnInitialTransforms.Remove(requestId);
            }
        }

        public void DestroyRoomObject(uint objectId)
        {
            if (!GameNet.IsConnected || objectId == 0) return;
            GameNet.Send(new Msg { MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.DestroyNetworkRoomObject,
                NetworkObject = new NetworkObjectData { ObjectId = objectId } });
        }

        /// <summary>请求服务器验证并抢占一个 RoleType.DiaoLuo 网络对象。</summary>
        public void ClaimRoomObject(uint objectId)
        {
            if (!GameNet.IsConnected || objectId == 0) return;
            GameNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.ClaimNetworkRoomObject,
                NetworkObjectId = objectId
            });
        }

        /// <summary>仅房主可请求生成 AI；服务端会自动选择当前 AI 负载最低的玩家运行。</summary>
        public void SpawnRoomAI(string prefabId, Vector3 position, Quaternion rotation,RoleType roleType=RoleType.Other)
        {
            if (!GameNet.IsConnected || string.IsNullOrWhiteSpace(prefabId)) return;
            GameNet.Send(new Msg { MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.SpawnNetworkRoomAi,
                NetworkObject = new NetworkObjectData { PrefabId = prefabId.Trim(), AiObject = true,RoleType =(int)roleType },
                NetworkTransform = new NetworkTransformData { PositionX = position.x, PositionY = position.y,
                    PositionZ = position.z, RotationX = rotation.x, RotationY = rotation.y,
                    RotationZ = rotation.z, RotationW = rotation.w, Sequence = 1 } });
        }

        /// <summary>
        /// 请求生成房间 AI，并在本客户端完成实例化后返回它。
        /// AI 的实际权威端仍由服务器分配，返回实例不代表当前客户端拥有控制权。
        /// </summary>
        public UniTask<NetworkIdentity> SpawnRoomAIAsync(
            string prefabId, Vector3 position, Quaternion rotation,
            RoleType roleType = RoleType.Other)
        {
            return SpawnNetworkObjectAsync(
                GameMsgType.SpawnNetworkRoomAi,
                new NetworkObjectData
                {
                    PrefabId = prefabId,
                    AiObject = true,
                    RoleType = (int)roleType
                },
                position,
                rotation);
        }

        /// <summary>
        /// 任意游戏房间成员都可以请求触发生成 AI。
        /// 同一房间内相同 triggerId 只会成功一次，最终是否生成由服务端判定。
        /// </summary>
        public void SpawnRoomAIFromTrigger(string triggerId, string prefabId, Vector3 position,
            Quaternion rotation,string boxKey,int boxIndex ,RoleType roleType = RoleType.Other)
        {
            if (!GameNet.IsConnected || string.IsNullOrWhiteSpace(triggerId) ||
                string.IsNullOrWhiteSpace(prefabId))
                return;

            GameNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.SpawnNetworkRoomAifromTrigger,
                NetworkObject = new NetworkObjectData
                {
                    TriggerId = triggerId.Trim(),
                    PrefabId = prefabId.Trim(),
                    AiObject = true,
                    RoleType = (int)roleType,
                    MonsterBoxKey = boxKey,
                    BoxIndex =  boxIndex,
                },
                NetworkTransform = new NetworkTransformData
                {
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    RotationX = rotation.x,
                    RotationY = rotation.y,
                    RotationZ = rotation.z,
                    RotationW = rotation.w,
                    Sequence = 1
                }
            });
        }

        /// <summary>
        /// 请求从关卡唯一触发器生成 AI，并返回本客户端中的实例。
        /// 同一触发器的并发请求共享生成结果；已有实例时直接返回该实例。
        /// </summary>
        public UniTask<NetworkIdentity> SpawnRoomAIFromTriggerAsync(
            string triggerId, string prefabId, Vector3 position, Quaternion rotation,
            string boxKey, int boxIndex, RoleType roleType = RoleType.Other)
        {
            if (string.IsNullOrWhiteSpace(triggerId))
                throw new ArgumentException("触发器 ID 不能为空。", nameof(triggerId));

            return SpawnNetworkObjectAsync(
                GameMsgType.SpawnNetworkRoomAifromTrigger,
                new NetworkObjectData
                {
                    TriggerId = triggerId.Trim(),
                    PrefabId = prefabId,
                    AiObject = true,
                    RoleType = (int)roleType,
                    MonsterBoxKey = boxKey ?? string.Empty,
                    BoxIndex = boxIndex
                },
                position,
                rotation);
        }

        async UniTask<NetworkIdentity> SpawnNetworkObjectAsync(
            GameMsgType spawnMessageType, NetworkObjectData objectData,
            Vector3 position, Quaternion rotation)
        {
            if (!GameNet.IsConnected)
                throw new InvalidOperationException("尚未连接到游戏服务器，无法生成网络对象。");
            if (NetworkRoomManager.Instance == null ||
                NetworkRoomManager.Instance.CurrentRoom == null ||
                NetworkRoomManager.Instance.CurrentRoom.RoomId <= NetworkRoomManager.LobbyRoomId ||
                NetworkRoomManager.Instance.IsLoadingRoom)
                throw new InvalidOperationException(
                    "当前尚未完整进入游戏房间，服务器不允许生成房间网络对象。");
            if (objectData == null || string.IsNullOrWhiteSpace(objectData.PrefabId))
                throw new ArgumentException("网络预制体 ID 不能为空。", nameof(objectData));

            if (!string.IsNullOrEmpty(objectData.TriggerId))
            {
                foreach (var pair in spawnRecords)
                {
                    if (pair.Value.Data.TriggerId == objectData.TriggerId &&
                        objects.TryGetValue(pair.Key, out var existing) && existing != null)
                    {
                        TraceSpawn("ReturnExisting", pair.Value.Data, $"instance={existing.gameObject.GetInstanceID()}");
                        return existing;
                    }
                }
            }

            string requestId = Guid.NewGuid().ToString("N");
            objectData.PrefabId = objectData.PrefabId.Trim();
            objectData.SpawnRequestId = requestId;
            var completion = new UniTaskCompletionSource<NetworkIdentity>();
            pendingSpawns.Add(requestId, completion);
            if (!string.IsNullOrEmpty(objectData.TriggerId))
                pendingSpawnTriggers.Add(requestId, objectData.TriggerId);

            TraceSpawn("Send", objectData, $"message={spawnMessageType}");
            GameNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = spawnMessageType,
                NetworkObject = objectData,
                NetworkTransform = new NetworkTransformData
                {
                    PositionX = position.x,
                    PositionY = position.y,
                    PositionZ = position.z,
                    RotationX = rotation.x,
                    RotationY = rotation.y,
                    RotationZ = rotation.z,
                    RotationW = rotation.w,
                    Sequence = 1
                }
            });

            try
            {
                var spawned = await GameNet.AwaitWithTimeoutAsync(completion, 10f);
                TraceSpawn("AwaitReturned", objectData,
                    $"returnedObjectId={spawned?.ObjectId} instance={spawned?.gameObject.GetInstanceID()}");
                return spawned;
            }
            catch (Exception exception)
            {
                TraceSpawn("AwaitFailed", objectData,
                    $"error={exception.GetType().Name}: {exception.Message} completionStatus={completion.Task.Status}");
                throw;
            }
            finally
            {
                pendingSpawns.Remove(requestId);
                pendingSpawnTriggers.Remove(requestId);
            }
        }

        void OnWebMessage(Msg msg)
        {
            if (msg.MsgType != ProtobufMsgType.Server)
                return;

            if (msg.ServerMsgType == ServerMsgType.NetworkRoomEntered ||
                msg.ServerMsgType == ServerMsgType.NetworkRoomLeft)
                RoomSharedValues.Clear();

            ServerMessageReceived?.Invoke(msg);

            switch (msg.ServerMsgType)
            {
                case ServerMsgType.RoomEquipmentRollResult:
                    EquipmentRollResultReceived?.Invoke(msg);
                    // if (msg.ChannelChatMessage != null)
                    // {
                    //     string equipmentName = "高品质装备";
                    //     if (msg.RewardEquip.Count > 0)
                    //     {
                    //         var equipment = LitJson.JsonMapper.ToObject<FrameWork.Script.GameData.GameEquipData>(
                    //             msg.RewardEquip[0].Data.ToStringUtf8());
                    //         var info = Xlsx.Xlsx_Equip_Query.XlsxDataAsOneKey.ByKeyGetValue(equipment.equipKey);
                    //         if (info != null) equipmentName = LanguageMrg.GetText(info.EquipName);
                    //     }
                    //     msg.ChannelChatMessage.Content = equipmentName + "\n" + msg.TipsSrt;
                    //     EventMrg.Trigger<ChannelChatMessageData>(ProtobufMsgType.Server,
                    //         ServerMsgType.ChannelChatMessageReceived, msg.ChannelChatMessage);
                    //     string[] lines = msg.TipsSrt.Split('\n');
                    //     UiManager.ShowTips(equipmentName + "：" + lines[lines.Length - 1]).Forget();
                    // }
                    break;
                case ServerMsgType.LoginSuc:
                    if (msg.NetworkObjectId != 0)
                        ConfigureLocalPlayer(msg.NetworkObjectId);
                    break;
                case ServerMsgType.NetworkObjectSpawn:
                    if (msg.NetworkObjects.Count > 0)
                    {
                        var initialTransforms = new Dictionary<uint, NetworkTransformData>(
                            msg.NetworkTransforms.Count);
                        foreach (NetworkTransformData transform in msg.NetworkTransforms)
                            initialTransforms[transform.ObjectId] = transform;
                        foreach (NetworkObjectData objectData in msg.NetworkObjects)
                        {
                            initialTransforms.TryGetValue(objectData.ObjectId, out NetworkTransformData initial);
                            NetworkObjectData spawnData = objectData;
                            SpawnRemoteObject(spawnData, initial).Forget(exception =>
                                FailPendingSpawn(spawnData, exception));
                        }
                    }
                    else if (msg.NetworkObject != null)
                    {
                        NetworkObjectData spawnData = msg.NetworkObject;
                        SpawnRemoteObject(spawnData, msg.NetworkTransform).Forget(exception =>
                            FailPendingSpawn(spawnData, exception));
                    }
                    break;
                case ServerMsgType.NetworkObjectDespawn:
                    DespawnObject(msg.NetworkObject?.ObjectId ?? 0);
                    break;
                case ServerMsgType.NetworkTransformUpdate:
                    if (msg.CompactTransforms != null && msg.CompactTransforms.Length > 0 &&
                        CompactTransformCodec.TryDecode(msg.CompactTransforms, decodedCompactTransforms))
                    {
                        for (int i = 0; i < decodedCompactTransforms.Count; i++)
                            HandleTransformSnapshot(decodedCompactTransforms[i]);
                        decodedCompactTransforms.Clear();
                    }
                    foreach (NetworkTransformData batchSnapshot in msg.NetworkTransforms)
                        HandleTransformSnapshot(batchSnapshot);
                    if (msg.NetworkTransform != null)
                        HandleTransformSnapshot(msg.NetworkTransform);
                    break;
                case ServerMsgType.NetworkAnimationUpdate:
                    HandleAnimationSnapshot(msg.NetworkAnimation);
                    break;
                case ServerMsgType.NetworkSyncVarUpdate:
                    if (msg.NetworkSyncVar != null)
                        HandleSyncVarSnapshot(msg.NetworkSyncVar);
                    for (int i = 0; i < msg.NetworkSyncVars.Count; i++)
                        HandleSyncVarSnapshot(msg.NetworkSyncVars[i]);
                    break;
                case ServerMsgType.NetworkObjectStateBatch:
                    foreach (NetworkAnimationData animation in msg.NetworkAnimations)
                        HandleAnimationSnapshot(animation);
                    foreach (NetworkSyncVarData syncVar in msg.NetworkSyncVars)
                        HandleSyncVarSnapshot(syncVar);
                    break;
                case ServerMsgType.RoomSharedValueUpdate:
                    if (msg.RoomSharedValue != null)
                        RoomSharedValues.Apply(msg.RoomSharedValue);
                    break;
                case ServerMsgType.RoomSharedValuesReset:
                    RoomSharedValues.Clear();
                    RoomSharedValues.NotifyResetCompleted();
                    break;
                case ServerMsgType.NetworkRpcInvoke:
                    if (msg.NetworkRpc != null)
                    {
                        if (objects.ContainsKey(msg.NetworkRpc.ObjectId))
                            NetworkRpcDispatcher.Invoke(msg.NetworkRpc);
                        else
                            BufferPendingRpc(msg.NetworkRpc);
                    }
                    break;
                case ServerMsgType.NetworkObjectAuthorityChanged:
                    ApplyAuthorityChange(msg.NetworkObject);
                    break;
                case ServerMsgType.NetworkRoomObjectClaimResult:
                    ObjectClaimResultReceived?.Invoke(
                        msg.NetworkObjectId,
                        msg.NetworkObject?.ObjectId ?? 0,
                        msg.Id,
                        msg.RoleType == 1);
                    break;
            }
        }

        void ConfigureLocalPlayer(uint objectId)
        {
            // 每次登录（包括断线后的新连接）都会获得新的 ObjectId。
            // 先删除上一次会话留下的索引和远程对象，避免同一个本地对象挂在多个 ID 下。
            foreach (KeyValuePair<uint, NetworkIdentity> pair in new List<KeyValuePair<uint, NetworkIdentity>>(objects))
            {
                objects.Remove(pair.Key);
                if (pair.Value != null && pair.Value != localPlayer)
                    Destroy(pair.Value.gameObject);
            }

            pendingLocalObjectId = objectId;
            if (localPlayer == null)
                return;

            localPlayer.Configure(objectId, GameNet.CurrentUserId, true,
                string.Empty, GameNet.CurrentUserId, true);
            objects[objectId] = localPlayer;
            ObjectSpawned?.Invoke(localPlayer,null,Vector3.zero,Quaternion.identity);
        }

        async UniTask SpawnRemoteObject(
            NetworkObjectData data,
            NetworkTransformData initialTransform)
        {
            TraceSpawn("Receive", data);
            if (data == null || data.ObjectId == 0 || data.ObjectId == pendingLocalObjectId || objects.ContainsKey(data.ObjectId))
            {
                TraceSpawn("SkipExistingOrInvalid", data);
                return;
            }
            // 玩家角色由 RoomMemberJoined 交给业务层创建，不使用服务端 PrefabId 自动生成。
            // NetworkObjectSpawn 自动生成只用于 AI、宝箱、掉落物等非玩家网络对象。
            if (data.PlayerObject)
                return;
            // 请求期间仍保留本地初始坐标，保证异步返回实例前位置已经正确。
            if (initialTransform == null && !string.IsNullOrEmpty(data.SpawnRequestId) &&
                pendingSpawnInitialTransforms.TryGetValue(data.SpawnRequestId,
                    out NetworkTransformData requestedTransform))
                initialTransform = requestedTransform.Clone();
            int generation = spawnGeneration;
            object ticket = new object();
            if (spawnTickets.ContainsKey(data.ObjectId))
            {
                TraceSpawn("SkipLoading", data);
                return;
            }
            spawnTickets[data.ObjectId] = ticket;
            GameObject instance;
            TraceSpawn("LoadBegin", data);
            try { instance = await GameObjectMrg.Instance.Dequeue(data.PrefabId,(RoleType)data.RoleType, activate: false); }
            catch
            {
                if (spawnTickets.TryGetValue(data.ObjectId, out var active) && ReferenceEquals(active, ticket))
                    spawnTickets.Remove(data.ObjectId);
                throw;
            }
            TraceSpawn("LoadReturned", data, $"instance={instance?.GetInstanceID()} generation={generation}/{spawnGeneration}");
            if (this == null || generation != spawnGeneration ||
                !spawnTickets.TryGetValue(data.ObjectId, out var currentTicket) ||
                !ReferenceEquals(ticket, currentTicket))
            {
                TraceSpawn("DiscardStaleLoad", data);
                if (instance != null) GameObjectMrg.Instance.Enqueue(instance);
                return;
            }
            spawnTickets.Remove(data.ObjectId);
            // 生成包后的初始位置包可能在异步加载预制体时先到，
            // 必须在将实例返回业务层前应用，否则投射物会从对象池旧位置起飞。
            if (pendingTransformSnapshots.TryGetValue(data.ObjectId,
                    out NetworkTransformData pendingTransform))
            {
                pendingTransformSnapshots.Remove(data.ObjectId);
                if (initialTransform == null || pendingTransform.Sequence >= initialTransform.Sequence)
                    initialTransform = pendingTransform;
            }
            if (data.AiObject && instance.GetComponent<NetworkTransform>() == null)
                instance.AddComponent<NetworkTransform>();

            if (initialTransform != null)
            {
                instance.transform.SetPositionAndRotation(
                    new Vector3(
                        initialTransform.PositionX,
                        initialTransform.PositionY,
                        initialTransform.PositionZ),
                    new Quaternion(
                        initialTransform.RotationX,
                        initialTransform.RotationY,
                        initialTransform.RotationZ,
                        initialTransform.RotationW));
            }
            instance.name = $"LobbyPlayer_{data.ObjectId}_{data.PlayerId}";
            NetworkIdentity identity = instance.GetComponent<NetworkIdentity>();
            if (identity == null)
                identity = instance.AddComponent<NetworkIdentity>();
            identity.Configure(data.ObjectId, data.PlayerId, false,
                data.PrefabId, data.OwnerPlayerId, data.PlayerObject, data.AiObject);
            NetworkIntegration.ConfigureSpawnedObject?.Invoke(instance, data);
            objects.Add(data.ObjectId, identity);
            // 出生位置与网络身份准备好后才触发 OnEnable，远端不会显示对象池旧位置。
            instance.SetActive(true);
            // 首次实例化时 Awake 在激活后才执行，此时刚体和 AI 引用才已准备好。
            foreach (NetworkTransform component in instance.GetComponents<NetworkTransform>())
                component.ResetNetworkState();
            if (initialTransform != null)
                DispatchTransform(initialTransform);
            TraceSpawn("Registered", data, $"instance={instance.GetInstanceID()}");
            FlushPendingSyncVars(data.ObjectId);
            FlushPendingAnimations(data.ObjectId);

            Vector3 spawnPosition = instance.transform.position;
            Quaternion spawnRotation = instance.transform.rotation;
            if (initialTransform != null)
            {
                spawnPosition = new Vector3(
                    initialTransform.PositionX,
                    initialTransform.PositionY,
                    initialTransform.PositionZ);
                spawnRotation = new Quaternion(
                    initialTransform.RotationX,
                    initialTransform.RotationY,
                    initialTransform.RotationZ,
                    initialTransform.RotationW);
            }

            spawnRecords[data.ObjectId] = new SpawnRecord
            {
                Data = data.Clone()
            };
            TraceSpawn("NotifyBegin", data);
            ObjectSpawned?.Invoke(identity, data, spawnPosition, spawnRotation);
            TraceSpawn("NotifyEnd", data);
            FlushPendingRpcs(data.ObjectId);
            TraceSpawn("PendingRpcsFlushed", data);

            CompletePendingSpawn(data, identity);
        }

        void CompletePendingSpawn(NetworkObjectData data, NetworkIdentity identity, Exception error = null)
        {
            // 服务器对触发器去重后只广播获胜请求的 ID，其他请求通过 TriggerId 关联同一对象。
            // 完成任务会同步恢复业务调用并修改等待表，因此先收集所有匹配任务。
            var completions = new List<UniTaskCompletionSource<NetworkIdentity>>();
            foreach (var pair in pendingSpawns)
            {
                if (pair.Key == data.SpawnRequestId ||
                    (!string.IsNullOrEmpty(data.TriggerId) &&
                     pendingSpawnTriggers.TryGetValue(pair.Key, out var triggerId) &&
                     triggerId == data.TriggerId))
                {
                    TraceSpawn("Match", data, $"waitingRequestId={pair.Key} byRequest={pair.Key == data.SpawnRequestId}");
                    completions.Add(pair.Value);
                }
            }
            TraceSpawn("MatchSummary", data, $"matched={completions.Count} error={error?.GetType().Name ?? "none"}");
            foreach (var completion in completions)
            {
                bool accepted;
                if (error != null)
                    accepted = completion.TrySetException(error);
                else
                    accepted = completion.TrySetResult(identity);
                TraceSpawn("CompletionSignaled", data, $"accepted={accepted}");
            }
        }

        void FailPendingSpawn(NetworkObjectData data, Exception exception)
        {
            if (data != null && data.AiObject)
                Debug.LogError($"[NetSpawn][Failed] client={GameNet.CurrentUserId} objectId={data.ObjectId} " +
                               $"requestId={data.SpawnRequestId} triggerId={data.TriggerId} error={exception}");
            if (data != null)
                CompletePendingSpawn(data, null, exception);
        }

        // 按需启用诊断符号；怪物批量生成时逐条写 Unity Console 会阻塞主线程。
        [System.Diagnostics.Conditional("WEB_NETWORK_SPAWN_TRACE")]
        [System.Diagnostics.Conditional("NETWORK_SPAWN_TRACE")]
        void TraceSpawn(string stage, NetworkObjectData data, string detail = "")
        {
            if (data == null || !data.AiObject) return;
            Debug.Log($"[NetSpawn][{stage}] client={GameNet.CurrentUserId} " +
                      $"objectId={data.ObjectId} requestId={data.SpawnRequestId} triggerId={data.TriggerId} " +
                      $"prefab={data.PrefabId} frame={Time.frameCount} pending={pendingSpawns.Count} {detail}");
        }

        void BufferPendingAnimation(NetworkAnimationData data)
        {
            if (data == null || data.ObjectId == 0)
                return;

            if (!pendingAnimationSnapshots.TryGetValue(data.ObjectId,
                    out Dictionary<int, NetworkAnimationData> tracks))
            {
                if (pendingAnimationSnapshots.Count >= MaxPendingObjects) return;
                tracks = new Dictionary<int, NetworkAnimationData>();
                pendingAnimationSnapshots.Add(data.ObjectId, tracks);
            }

            int track = Mathf.Max(0, data.TrackIndex);
            if (!tracks.TryGetValue(track, out NetworkAnimationData current) ||
                data.Sequence >= current.Sequence)
                tracks[track] = data.Clone();
        }

        void BufferPendingSyncVar(NetworkSyncVarData data)
        {
            if (data == null || data.ObjectId == 0) return;
            if (!pendingSyncVarSnapshots.TryGetValue(data.ObjectId,
                    out Dictionary<ulong, NetworkSyncVarData> fields))
            {
                if (pendingSyncVarSnapshots.Count >= MaxPendingObjects) return;
                fields = new Dictionary<ulong, NetworkSyncVarData>();
                pendingSyncVarSnapshots.Add(data.ObjectId, fields);
            }
            ulong key = ((ulong)data.BehaviourId << 32) | data.FieldId;
            if (!fields.TryGetValue(key, out NetworkSyncVarData current) ||
                data.Sequence > current.Sequence)
                fields[key] = data.Clone();
        }

        void FlushPendingSyncVars(uint objectId)
        {
            if (!pendingSyncVarSnapshots.TryGetValue(objectId,
                    out Dictionary<ulong, NetworkSyncVarData> fields)) return;
            pendingSyncVarSnapshots.Remove(objectId);
            foreach (NetworkSyncVarData snapshot in fields.Values)
                DispatchSyncVar(snapshot);
        }

        void BufferPendingRpc(NetworkRpcData data)
        {
            if (data == null || data.ObjectId == 0)
                return;

            if (!pendingRpcs.TryGetValue(data.ObjectId, out Queue<NetworkRpcData> queue))
            {
                if (pendingRpcs.Count >= MaxPendingObjects) return;
                queue = new Queue<NetworkRpcData>();
                pendingRpcs.Add(data.ObjectId, queue);
            }

            if (queue.Count >= MaxPendingRpcsPerObject)
            {
                Debug.LogWarning($"[Network] 对象 {data.ObjectId} 的待处理 RPC 超过上限，丢弃最早一条消息");
                queue.Dequeue();
            }
            queue.Enqueue(data.Clone());
        }

        void FlushPendingRpcs(uint objectId)
        {
            if (!pendingRpcs.TryGetValue(objectId, out Queue<NetworkRpcData> queue))
                return;

            pendingRpcs.Remove(objectId);
            while (queue.Count > 0)
                NetworkRpcDispatcher.Invoke(queue.Dequeue());
        }

        void FlushPendingAnimations(uint objectId)
        {
            if (!pendingAnimationSnapshots.TryGetValue(objectId,
                    out Dictionary<int, NetworkAnimationData> tracks))
                return;

            pendingAnimationSnapshots.Remove(objectId);
            var snapshots = new List<NetworkAnimationData>(tracks.Values);
            snapshots.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            foreach (NetworkAnimationData snapshot in snapshots)
                DispatchAnimation(snapshot);
        }

        void ApplyAuthorityChange(NetworkObjectData data)
        {
            if (data == null || !objects.TryGetValue(data.ObjectId, out NetworkIdentity identity)) return;
            identity.UpdateAuthority(data.OwnerPlayerId);
            AuthorityChanged?.Invoke(identity);
        }

        GameObject ResolvePrefab(NetworkObjectData data)
        {
            if (data.PlayerObject || string.IsNullOrWhiteSpace(data.PrefabId))
                return remotePlayerPrefab;
            foreach (NetworkPrefabEntry entry in networkPrefabs)
                if (entry != null && entry.prefab != null &&
                    string.Equals(entry.prefabId, data.PrefabId, StringComparison.OrdinalIgnoreCase))
                    return entry.prefab;
            return null;
        }

        void DespawnObject(uint objectId)
        {
            if (objectId == 0)
                return;

            spawnTickets.Remove(objectId);

            pendingAnimationSnapshots.Remove(objectId);
            pendingSyncVarSnapshots.Remove(objectId);
            pendingTransformSnapshots.Remove(objectId);
            pendingRpcs.Remove(objectId);
            spawnRecords.Remove(objectId);
            if (!objects.TryGetValue(objectId, out NetworkIdentity identity))
                return;

            objects.Remove(objectId);
            // 玩家对象由 RoomMemberLeft 对应的场景业务负责回收，避免网络层和
            // LobbyScene 对同一个对象重复 Enqueue。AI 与普通网络物体仍由网络层管理。
            if (identity != null && identity != localPlayer && !identity.IsPlayerObject)
                GameObjectMrg.Instance.Enqueue(identity.gameObject);
            ObjectDespawned?.Invoke(objectId);
        }

        void ClearRemoteObjects()
        {
            CancelPendingSpawns();
            foreach (NetworkIdentity identity in objects.Values)
                if (identity != null && identity != localPlayer && !identity.IsPlayerObject)
                    GameObjectMrg.Instance.Enqueue(identity.gameObject);
            objects.Clear();
            spawnRecords.Clear();
            pendingTransformSnapshots.Clear();
            pendingAnimationSnapshots.Clear();
            pendingSyncVarSnapshots.Clear();
            pendingRpcs.Clear();
            pendingLocalObjectId = 0;
        }

        /// <summary>切换网络房间或地图前清理上一房间的远程对象。</summary>
        public void ResetForLogin()
        {
            ClearRemoteObjects();
            localPlayer = null;
        }

        /// <summary>切换网络房间或地图前清理上一房间的远程对象。</summary>
        public void ClearRemoteObjectsForSceneChange()
        {
            CancelPendingSpawns();
            foreach (NetworkIdentity identity in objects.Values)
                if (identity != null && identity != localPlayer && !identity.IsPlayerObject)
                    GameObjectMrg.Instance.Enqueue(identity.gameObject);

            objects.Clear();
            spawnRecords.Clear();
            pendingTransformSnapshots.Clear();
            pendingAnimationSnapshots.Clear();
            pendingSyncVarSnapshots.Clear();
            pendingRpcs.Clear();
            if (localPlayer != null && pendingLocalObjectId != 0)
                objects[pendingLocalObjectId] = localPlayer;
        }

        private int spawnGeneration;
        private readonly Dictionary<uint, object> spawnTickets = new();

        private void CancelPendingSpawns()
        {
            ++spawnGeneration;
            spawnTickets.Clear();
            var completions = new List<UniTaskCompletionSource<NetworkIdentity>>(pendingSpawns.Values);
            pendingSpawns.Clear();
            pendingSpawnTriggers.Clear();
            pendingSpawnInitialTransforms.Clear();
            foreach (var completion in completions) completion.TrySetCanceled();
        }
    }
}
