using System;
using System.Collections;
using System.Collections.Generic;
using FrameWork;
using GameData;
using UnityEngine;
using UnityEngine.SceneManagement;
using LobbyWebNet = FrameWork.WebSocket.WebNet;

namespace FrameWork.Script.WebNet
{
    /// <summary>
    /// WebNet 房间客户端管理器。
    /// 负责创建、匹配、准备、开始、离开房间，以及房间地图加载和房间事件分发。
    /// </summary>
    public sealed class WebNetworkRoomManager : MonoBehaviour
    {
        /// <summary>服务端长期大厅使用的固定房间 ID。</summary>
        public const uint LobbyRoomId = 1;

        [Header("生命周期")]
        [Tooltip("切换场景时是否保留此管理器。")]
        [SerializeField] bool persistAcrossScenes = true;

        [Header("场景加载")]
        [Tooltip("收到服务端的房间地图加载消息后，是否自动使用 ABMrg 加载场景。")]
        [SerializeField] bool autoLoadRoomMap = true;
        [Tooltip("地图加载完成后，是否自动注册场景中的本地玩家。")]
        [SerializeField] bool autoRegisterLocalPlayerAfterLoad = true;
        [Tooltip("预留的 Unity 场景加载模式；改回 SceneManager 加载时使用。")]
        [SerializeField] LoadSceneMode roomLoadMode = LoadSceneMode.Single;

        /// <summary>本地玩家当前已经进入的房间；大厅的 RoomId 固定为 1。</summary>
        public NetworkRoomData CurrentRoom { get; private set; }

        /// <summary>正在等待加载或等待向服务端确认就绪的目标房间。</summary>
        public NetworkRoomData PendingRoom { get; private set; }

        /// <summary>当前是否正在加载房间地图。</summary>
        public bool IsLoadingRoom { get; private set; }

        /// <summary>本地玩家是否已经正式进入游戏房间，不包含长期大厅和加载过程。</summary>
        public bool IsInGameRoom => CurrentRoom != null && CurrentRoom.RoomId > LobbyRoomId;

        /// <summary>本地玩家是否已经正式进入长期大厅。</summary>
        public bool IsInLobby => CurrentRoom != null && CurrentRoom.RoomId == LobbyRoomId;

        /// <summary>本地玩家是否已经进入或正在加载一个游戏房间。</summary>
        public bool IsInOrEnteringGameRoom => IsInGameRoom ||
            PendingRoom != null && PendingRoom.RoomId > LobbyRoomId;

        /// <summary>本地玩家是否为当前房间的房主。</summary>
        public bool IsLocalPlayerHost => CurrentRoom != null &&
                                         CurrentRoom.HostPlayerId == LobbyWebNet.CurrentUserId;

        /// <summary>当前房间是否已有成员，并且所有成员都已准备。</summary>
        public bool AreAllPlayersReady => CurrentRoom != null && CurrentRoom.Members.Count > 0 &&
                                          AllMembersReady(CurrentRoom);

        /// <summary>
        /// 获取本地玩家在当前房间中的成员数据。
        /// 场景脚本较晚订阅事件时，可用此方法立即恢复当前状态，避免漏掉已发生的进入事件。
        /// </summary>
        public bool TryGetLocalRoomMember(out NetworkRoomMemberData member)
        {
            member = FindLocalMember(CurrentRoom);
            return member != null;
        }

        /// <summary>服务端要求加载某个房间地图时触发，适合显示加载界面。</summary>
        public static event Action<NetworkRoomData> RoomMapLoadRequested;

        /// <summary>本地玩家完成进入任意房间时触发；大厅和游戏房间都会触发。</summary>
        public static event Action<NetworkRoomData> RoomEntered;

        /// <summary>本地玩家完成进入长期大厅时触发。</summary>
        public static event Action<NetworkRoomData> LobbyEntered;

        /// <summary>本地玩家完成进入游戏房间时触发。</summary>
        public static event Action<NetworkRoomData> GameRoomEntered;

        /// <summary>本地玩家离开游戏房间并返回大厅时触发，参数为刚离开的房间。</summary>
        public static event Action<NetworkRoomData> GameRoomLeft;

        static Action<NetworkRoomMemberData> roomMemberJoined;

        /// <summary>
        /// 当前房间内新增了一名成员时触发，也包括本地玩家自己完成进入房间。
        /// 如果订阅发生在本地玩家已经进入房间之后，会立即向这个新订阅者补发一次本地成员数据，
        /// 从而避免场景 Awake 晚于网络消息时漏掉回调。
        /// </summary>
        public static event Action<NetworkRoomMemberData> RoomMemberJoined
        {
            add
            {
                roomMemberJoined += value;

                WebNetworkRoomManager manager = Instance;
                if (manager != null && manager.hasCompletedCurrentRoomEntry && manager.CurrentRoom != null)
                {
                    // 晚订阅者需要恢复整个房间，而不只是本地玩家；否则无法创建先进入的玩家。
                    foreach (NetworkRoomMemberData member in manager.CurrentRoom.Members)
                        value?.Invoke(member);
                }
            }
            remove => roomMemberJoined -= value;
        }

        /// <summary>当前房间内有成员离开时触发，也包括本地玩家自己离开或切换房间。</summary>
        public static event Action<NetworkRoomMemberData> RoomMemberLeft;

        /// <summary>同一房间成员的数据发生变化时触发（例如装备、准备状态或角色数据）。</summary>
        public static event Action<NetworkRoomMemberData> RoomMemberUpdated;

        /// <summary>房间成员、准备状态或房主等房间数据发生变化时触发。</summary>
        public static event Action<NetworkRoomData> RoomStateChanged;

        /// <summary>服务端返回当前区服可加入的房间列表时触发。</summary>
        public static event Action<IReadOnlyList<NetworkRoomData>> RoomListReceived;

        /// <summary>服务端确认当前房间开始游戏时触发。</summary>
        public static event Action<NetworkRoomData> RoomGameStarted;

        /// <summary>主动切图后，服务端确认房间内全部玩家均已进入目标地图时触发。</summary>
        public static event Action<NetworkRoomData> AllPlayersEnteredRoom;

        /// <summary>收到好友房间邀请时触发。</summary>
        public static event Action<NetworkRoomInviteData> RoomInviteReceived;

        /// <summary>本地玩家被房主踢出时触发。</summary>
        public static event Action RoomKicked;

        /// <summary>兼容旧代码：本地玩家离开游戏房间时触发。</summary>
        public static event Action RoomLeft;

        /// <summary>房间请求失败时触发，参数为服务端返回的错误文本。</summary>
        public static event Action<string> RoomError;

        public static WebNetworkRoomManager Instance { get; private set; }

        readonly Dictionary<string, NetworkRoomMemberData> memberSnapshot =
            new Dictionary<string, NetworkRoomMemberData>(StringComparer.Ordinal);
        readonly List<NetworkRoomData> roomListBuffer = new List<NetworkRoomData>();

        // 切换房间过程中保存原房间，等真正进入目标房间后再派发本地成员离开事件。
        NetworkRoomData roomBeingLeft;

        // 登录时服务端可能在大厅场景加载完成前返回 NetworkRoomEntered，先暂存到加载结束。
        NetworkRoomData enteredRoomWaitingForScene;

        // 只有场景和服务端进入确认都完成后，晚订阅者才可以重放本地成员进入事件。
        bool hasCompletedCurrentRoomEntry;

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
        }

        void OnEnable()
        {
            WebNetworkManager.ServerMessageReceived += OnServerMessage;
        }

        void OnDisable()
        {
            WebNetworkManager.ServerMessageReceived -= OnServerMessage;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>创建一个游戏房间。</summary>
        public void CreateRoom(NetworkRoomData roomData)
        {
            if (!LobbyWebNet.IsConnected || roomData == null ||
                string.IsNullOrWhiteSpace(roomData.MapName) ||
                string.IsNullOrWhiteSpace(roomData.LevelKey) || IsLoadingRoom)
                return;
            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.CreateNetworkRoom,
                NetworkRoom = roomData.Clone()
            });
        }

        /// <summary>匹配同地图且未满的房间；没有可用房间时由服务端自动创建。</summary>
        public void MatchRoom(NetworkRoomData roomData)
        {
            if (!LobbyWebNet.IsConnected || roomData == null ||
                string.IsNullOrWhiteSpace(roomData.MapName) ||
                string.IsNullOrWhiteSpace(roomData.LevelKey) || IsLoadingRoom)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.MatchNetworkRoom,
                NetworkRoom = roomData.Clone()
            });
        }

        /// <summary>按房间 ID 加入一个可加入的游戏房间。</summary>
        public void JoinRoom(uint roomId)
        {
            if (roomId == 0 || roomId == LobbyRoomId || IsLoadingRoom)
                return;

            SendSimpleRoomRequest(GameMsgType.JoinNetworkRoom,
                new NetworkRoomRequest { RoomId = roomId });
        }

        /// <summary>请求当前区服中所有可加入的游戏房间。</summary>
        public void GetRoomList()
        {
            if (!LobbyWebNet.IsConnected)
                return;

            roomListBuffer.Clear();
            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.GetNetworkRoomList
            });
        }

        /// <summary>
        /// 房主设置房间语音路由。关闭 AOI 后，所有符合队伍规则的房间成员都能听见。
        /// </summary>
        public void SetVoiceRouting(bool aoiEnabled, bool teamOnly)
        {
            SendSimpleRoomRequest(GameMsgType.SetNetworkVoiceOptions,
                new NetworkRoomRequest
                {
                    Ready = aoiEnabled,
                    MaxPlayers = teamOnly ? 1u : 0u
                });
        }

        /// <summary>房主把指定玩家分配到语音队伍；同一 teamId 的玩家可以互相通话。</summary>
        public void SetPlayerVoiceTeam(string playerId, string teamId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(teamId))
                return;

            SendSimpleRoomRequest(GameMsgType.SetNetworkVoiceTeam,
                new NetworkRoomRequest
                {
                    TargetPlayerId = playerId.Trim(),
                    MapName = teamId.Trim()
                });
        }

        /// <summary>离开当前游戏房间。服务端会把玩家重新放回长期大厅。</summary>
        public void LeaveRoom()
        {
            if (!LobbyWebNet.IsConnected)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.LeaveNetworkRoom
            });
        }

        /// <summary>设置本地玩家在当前房间中的准备状态。</summary>
        public void SetReady(bool ready)
        {
            SendSimpleRoomRequest(GameMsgType.SetNetworkRoomReady,
                new NetworkRoomRequest { Ready = ready });
        }

        /// <summary>
        /// 房主在等待阶段修改当前房间的名称、人数上限和关卡标识。
        /// 修改结果通过 RoomStateChanged 广播给房间内所有成员；失败时触发 RoomError。
        /// </summary>
        public void UpdateRoomInfo(NetworkRoomData roomData)
        {
            if (roomData == null || string.IsNullOrWhiteSpace(roomData.RoomName) ||
                string.IsNullOrWhiteSpace(roomData.LevelKey) ||
                CurrentRoom == null || CurrentRoom.RoomId == LobbyRoomId || CurrentRoom.Started ||
                IsLoadingRoom)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.UpdateNetworkRoom,
                NetworkRoom = roomData.Clone()
            });
        }

        /// <summary>请求开始游戏。服务端会校验房主身份以及所有成员的准备状态。</summary>
        public void StartGame()
        {
            SendSimpleRoomRequest(GameMsgType.StartNetworkRoomGame, new NetworkRoomRequest());
        }

        /// <summary>
        /// 请求把当前房间内的全部玩家切换到指定地图。服务端只允许房主调用。
        /// 全员加载完成后会触发 AllPlayersEnteredRoom。
        /// </summary>
        public void ChangeMap(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName) || IsLoadingRoom)
                return;

            SendSimpleRoomRequest(GameMsgType.ChangeNetworkRoomMap,
                new NetworkRoomRequest { MapName = mapName.Trim() });
        }

        /// <summary>房主将指定玩家踢出当前房间。</summary>
        public void KickPlayer(string playerId)
        {
            SendSimpleRoomRequest(GameMsgType.KickNetworkRoomPlayer,
                new NetworkRoomRequest { TargetPlayerId = playerId ?? string.Empty });
        }

        /// <summary>邀请指定玩家进入当前房间。</summary>
        public void InviteFriend(string friendPlayerId)
        {
            SendSimpleRoomRequest(GameMsgType.InviteNetworkRoomPlayer,
                new NetworkRoomRequest { TargetPlayerId = friendPlayerId ?? string.Empty });
        }

        /// <summary>接受房间邀请。</summary>
        public void AcceptInvite(uint roomId)
        {
            SendSimpleRoomRequest(GameMsgType.AcceptNetworkRoomInvite,
                new NetworkRoomRequest { RoomId = roomId });
        }

        /// <summary>
        /// 通知服务端目标房间的场景已经加载完成。
        /// 关闭自动场景加载时，由业务层在场景准备完成后调用。
        /// </summary>
        public void ConfirmRoomSceneReady()
        {
            if (PendingRoom == null || PendingRoom.RoomId == 0 || !LobbyWebNet.IsConnected)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = GameMsgType.NetworkRoomSceneReady,
                NetworkRoomRequest = new NetworkRoomRequest { RoomId = PendingRoom.RoomId }
            });
        }

        void SendSimpleRoomRequest(GameMsgType type, NetworkRoomRequest request)
        {
            if (!LobbyWebNet.IsConnected)
                return;

            LobbyWebNet.Send(new Msg
            {
                MsgType = ProtobufMsgType.Game,
                GameMsgType = type,
                NetworkRoomRequest = request
            });
        }

        // WebNetworkManager 的统一消息入口，只处理房间相关的服务端消息。
        void OnServerMessage(Msg msg)
        {
            switch (msg.ServerMsgType)
            {
                case ServerMsgType.LoginSuc:
                    HandleLoginRoom(msg.NetworkRoom);
                    break;

                case ServerMsgType.NetworkRoomLoad:
                    BeginRoomTransition(msg.NetworkRoom);
                    PendingRoom = msg.NetworkRoom;
                    RoomMapLoadRequested?.Invoke(PendingRoom);
                    if (autoLoadRoomMap)
                        // 大厅和登录流程一致：服务端已经完成成员加入并会主动发送 Entered，
                        // 场景加载完直接消费该确认，不再额外上报 SceneReady。
                        LoadScene(PendingRoom, PendingRoom.RoomId != LobbyRoomId);
                    break;

                case ServerMsgType.NetworkRoomEntered:
                    if (IsLoadingRoom && PendingRoom != null && msg.NetworkRoom != null &&
                        PendingRoom.RoomId == msg.NetworkRoom.RoomId)
                    {
                        enteredRoomWaitingForScene = msg.NetworkRoom;
                    }
                    else
                    {
                        CompleteRoomEntered(msg.NetworkRoom);
                    }
                    break;

                case ServerMsgType.NetworkRoomLeft:
                    CompleteRoomLeft();
                    break;

                case ServerMsgType.NetworkRoomError:
                    IsLoadingRoom = false;
                    RoomError?.Invoke(msg.TipsSrt);
                    break;

                case ServerMsgType.NetworkRoomState:
                    // 场景加载期间可能已有其他玩家加入。
                    // 此时不能提前建立成员快照，否则加载完成后会把这些玩家误判成“已经通知过”。
                    // 保存最新状态，等 CompleteRoomEntered 统一为完整成员列表派发加入回调。
                    if (IsLoadingRoom && PendingRoom != null && msg.NetworkRoom != null &&
                        PendingRoom.RoomId == msg.NetworkRoom.RoomId)
                    {
                        enteredRoomWaitingForScene = msg.NetworkRoom;
                        RoomStateChanged?.Invoke(msg.NetworkRoom);
                        break;
                    }

                    DispatchMemberChanges(msg.NetworkRoom);
                    CurrentRoom = msg.NetworkRoom;
                    RoomStateChanged?.Invoke(CurrentRoom);
                    break;

                case ServerMsgType.NetworkRoomListItem:
                    if (msg.NetworkRoom != null && msg.NetworkRoom.RoomId != 0)
                        roomListBuffer.Add(msg.NetworkRoom);
                    break;

                case ServerMsgType.NetworkRoomListCompleted:
                    RoomListReceived?.Invoke(roomListBuffer.ToArray());
                    roomListBuffer.Clear();
                    break;

                case ServerMsgType.NetworkRoomGameStarted:
                    CurrentRoom = msg.NetworkRoom;
                    CaptureMembers(CurrentRoom);
                    RoomGameStarted?.Invoke(CurrentRoom);
                    break;

                case ServerMsgType.NetworkRoomAllPlayersEntered:
                    CurrentRoom = msg.NetworkRoom;
                    CaptureMembers(CurrentRoom);
                    RoomStateChanged?.Invoke(CurrentRoom);
                    AllPlayersEnteredRoom?.Invoke(CurrentRoom);
                    break;

                case ServerMsgType.NetworkRoomInviteReceived:
                    RoomInviteReceived?.Invoke(msg.NetworkRoomInvite);
                    break;

                case ServerMsgType.NetworkRoomKicked:
                    BeginRoomTransition(msg.NetworkRoom);
                    CurrentRoom = null;
                    memberSnapshot.Clear();
                    PendingRoom = msg.NetworkRoom;
                    RoomKicked?.Invoke();
                    RoomMapLoadRequested?.Invoke(PendingRoom);
                    if (autoLoadRoomMap)
                        LoadScene(PendingRoom, PendingRoom.RoomId != LobbyRoomId);
                    break;
            }
        }

        // 登录成功后服务端会返回玩家所在房间，通常为长期大厅。
        void HandleLoginRoom(NetworkRoomData room)
        {
            if (room == null || room.RoomId == 0)
                return;

            // LoginSuc 代表新连接上的登录，不等于旧房间状态仍可复用。
            // 即使仍在大厅，也要重新加载场景并按新 objectId 重建全部成员。
            if (CurrentRoom != null)
                Debug.Log("[WebRoom] 重新登录，重建房间场景和成员");
            BeginRoomTransition(room);

            if (!string.IsNullOrWhiteSpace(room.MapName))
            {
                hasCompletedCurrentRoomEntry = false;
                PendingRoom = room;
                RoomMapLoadRequested?.Invoke(room);
                if (autoLoadRoomMap)
                    LoadScene(room, false);
                return;
            }

            // 房间进入成功由服务端明确发送 NetworkRoomEntered，不在 LoginSuc 中重复派发。
        }

        /// <summary>
        /// 使用 ABMrg 加载房间场景。
        /// confirmReady 为 true 时，加载完会向服务端发送场景就绪；登录大厅时应传 false。
        /// </summary>
        /// <param name="room">需要加载的目标房间及地图信息。</param>
        /// <param name="confirmReady">加载后是否向服务端确认预留房间；大厅会被强制设为 false。</param>
        /// <param name="progress">可选的场景加载进度回调。</param>
        /// <param name="end">可选的场景加载完成回调。</param>
        private int loadGeneration;
        private Action pendingLoad;

        public void LoadScene(NetworkRoomData room, bool confirmReady = true,
            Action<float> progress = null, Action end = null)
        {
            if (room == null)
                return;
            ++loadGeneration;
            if (IsLoadingRoom)
            {
                // Addressables 场景加载不能直接取消；等旧加载结束后只执行最新请求。
                pendingLoad = () => LoadScene(room, confirmReady, progress, end);
                return;
            }

            // 长期大厅由服务端立即加入并主动发送 NetworkRoomEntered；即使业务层手动加载场景，
            // 也不能再发送 SceneReady，否则服务端会把它当成不存在的房间预留。
            if (room.RoomId == LobbyRoomId)
                confirmReady = false;

            StartCoroutine(LoadRoomMap(room, confirmReady, progress, end));
        }

        IEnumerator LoadRoomMap(NetworkRoomData room, bool confirmReady,
            Action<float> progress, Action end)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.MapName))
                yield break;

            IsLoadingRoom = true;
            int generation = loadGeneration;
            WebNetworkManager.Instance?.ClearRemoteObjectsForSceneChange();

            bool loadCompleted = false;
            Exception loadError = null;
            ABMrg.LoadSceneAsync(room.MapName, progress, () => loadCompleted = true,
                error => { loadError = error; loadCompleted = true; });
            float warningAt = Time.realtimeSinceStartup + 60f;
            bool warned = false;
            while (!loadCompleted)
            {
                if (!warned && Time.realtimeSinceStartup >= warningAt)
                {
                    warned = true;
                    RoomError?.Invoke("场景加载超过60秒，请检查网络；仍无响应时请重新进入小游戏。");
                }
                yield return null;
            }

            if (generation != loadGeneration)
            {
                IsLoadingRoom = false;
                var nextLoad = pendingLoad;
                pendingLoad = null;
                enteredRoomWaitingForScene = null;
                nextLoad?.Invoke();
                yield break;
            }
            if (loadError != null)
            {
                IsLoadingRoom = false;
                PendingRoom = null;
                enteredRoomWaitingForScene = null;
                RoomError?.Invoke(loadError.Message);
                yield break;
            }

            if (autoRegisterLocalPlayerAfterLoad && WebNetworkManager.Instance != null)
            {
                if (WebNetworkLocalPlayer.Active != null)
                {
                    WebNetworkLocalPlayer.Active.Register();
                }
                // 没有预放置本地玩家是合法情况：业务层可以在 RoomMemberJoined 中动态创建并注册。
            }

            if (confirmReady)
            {
                // 服务端收到确认后会返回 NetworkRoomEntered，再由该消息触发进入事件。
                // 场景已加载完成，先结束加载状态；否则 NetworkRoomEntered 只会被缓存，
                // 后续没有机会完成进入流程，RoomMemberJoined 也就不会触发。
                IsLoadingRoom = false;
                ConfirmRoomSceneReady();
            }
            else
            {
                // 登录大厅时服务端已完成 JoinLobbyNow，并会明确返回 NetworkRoomEntered。
                // 如果消息先于场景加载到达，此处再统一完成进入流程。
                NetworkRoomData enteredRoom = enteredRoomWaitingForScene ?? room;
                enteredRoomWaitingForScene = null;
                CompleteRoomEntered(enteredRoom);
            }

            end?.Invoke();
        }

        // 记录房间切换过程，避免在目标地图尚未加载完成时过早通知 UI。
        void BeginRoomTransition(NetworkRoomData targetRoom)
        {
            if (CurrentRoom != null && targetRoom != null &&
                CurrentRoom.RoomId != targetRoom.RoomId)
            {
                roomBeingLeft = CurrentRoom;
                hasCompletedCurrentRoomEntry = false;
            }
        }

        // 本地玩家真正完成房间进入后，统一更新状态并派发进入/离开事件。
        void CompleteRoomEntered(NetworkRoomData room)
        {
            if (room == null || room.RoomId == 0)
                return;

            // 登录大厅时场景加载和服务端消息是并行的，防止极快加载造成同一进入事件重复派发。
            if (CurrentRoom != null && CurrentRoom.RoomId == room.RoomId &&
                hasCompletedCurrentRoomEntry && PendingRoom == null && !IsLoadingRoom && roomBeingLeft == null)
            {
                CurrentRoom = room;
                CaptureMembers(room);
                return;
            }

            NetworkRoomData leftRoom = roomBeingLeft;
            NetworkRoomMemberData localLeftMember = FindLocalMember(leftRoom);

            CurrentRoom = room;
            PendingRoom = null;
            IsLoadingRoom = false;
            hasCompletedCurrentRoomEntry = true;
            CaptureMembers(room);

            // 在目标场景准备好之后，再按“离开旧房间 -> 进入新房间”的顺序通知业务层。
            if (localLeftMember != null)
                RoomMemberLeft?.Invoke(localLeftMember);

            if (leftRoom != null && leftRoom.RoomId != LobbyRoomId && room.RoomId == LobbyRoomId)
            {
                GameRoomLeft?.Invoke(leftRoom);
                RoomLeft?.Invoke();
            }

            roomBeingLeft = null;

            RoomEntered?.Invoke(room);
            if (room.RoomId == LobbyRoomId)
                LobbyEntered?.Invoke(room);
            else
                GameRoomEntered?.Invoke(room);

            // 新客户端进入房间时，需要为自己和所有已存在成员创建玩家角色。
            // 已在房间中的客户端则通过后续 NetworkRoomState 差异只收到新加入的成员。
            foreach (NetworkRoomMemberData member in room.Members)
                roomMemberJoined?.Invoke(member);
        }

        // 兼容服务端直接返回 NetworkRoomLeft、且没有立刻进入大厅的情况。
        void CompleteRoomLeft()
        {
            NetworkRoomData leftRoom = roomBeingLeft ?? CurrentRoom;
            NetworkRoomMemberData localLeftMember = FindLocalMember(leftRoom);
            CurrentRoom = null;
            PendingRoom = null;
            IsLoadingRoom = false;
            hasCompletedCurrentRoomEntry = false;
            roomBeingLeft = null;
            memberSnapshot.Clear();
            WebNetworkManager.Instance?.ClearRemoteObjectsForSceneChange();

            if (localLeftMember != null)
                RoomMemberLeft?.Invoke(localLeftMember);

            if (leftRoom != null && leftRoom.RoomId != LobbyRoomId)
            {
                GameRoomLeft?.Invoke(leftRoom);
                RoomLeft?.Invoke();
            }
        }

        // 对比服务端的新旧成员列表，只派发真正新增或离开的玩家。
        void DispatchMemberChanges(NetworkRoomData newRoom)
        {
            if (newRoom == null)
                return;

            // 房间发生切换时直接建立新快照，进入事件负责通知本地玩家的房间变化。
            if (CurrentRoom == null || CurrentRoom.RoomId != newRoom.RoomId)
            {
                CaptureMembers(newRoom);
                return;
            }

            var newMembers = new Dictionary<string, NetworkRoomMemberData>(StringComparer.Ordinal);
            foreach (NetworkRoomMemberData member in newRoom.Members)
            {
                if (string.IsNullOrEmpty(member.PlayerId))
                    continue;

                newMembers[member.PlayerId] = member;
                if (!memberSnapshot.ContainsKey(member.PlayerId))
                    roomMemberJoined?.Invoke(member);
                else if (!memberSnapshot[member.PlayerId].Equals(member))
                    RoomMemberUpdated?.Invoke(member);
            }

            foreach (KeyValuePair<string, NetworkRoomMemberData> pair in memberSnapshot)
            {
                if (!newMembers.ContainsKey(pair.Key))
                    RoomMemberLeft?.Invoke(pair.Value);
            }

            memberSnapshot.Clear();
            foreach (KeyValuePair<string, NetworkRoomMemberData> pair in newMembers)
                memberSnapshot.Add(pair.Key, pair.Value);
        }

        // 保存当前成员快照，供下一次 NetworkRoomState 消息做增量比较。
        void CaptureMembers(NetworkRoomData room)
        {
            memberSnapshot.Clear();
            if (room == null)
                return;

            foreach (NetworkRoomMemberData member in room.Members)
            {
                if (!string.IsNullOrEmpty(member.PlayerId))
                    memberSnapshot[member.PlayerId] = member;
            }
        }

        // 从房间数据中查找本地玩家，统一用于本地成员的进入和离开回调。
        static NetworkRoomMemberData FindLocalMember(NetworkRoomData room)
        {
            if (room == null || string.IsNullOrEmpty(LobbyWebNet.CurrentUserId))
                return null;

            foreach (NetworkRoomMemberData member in room.Members)
            {
                if (string.Equals(member.PlayerId, LobbyWebNet.CurrentUserId,
                        StringComparison.Ordinal))
                    return member;
            }

            return null;
        }

        static bool AllMembersReady(NetworkRoomData room)
        {
            foreach (NetworkRoomMemberData member in room.Members)
            {
                if (!member.Ready&& !member.Host)
                    return false;
            }

            return true;
        }
    }
}
