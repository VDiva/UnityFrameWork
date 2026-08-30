using GameData;

namespace WebSocketDemo;

/// <summary>永久大厅和游戏房间的权威状态管理。</summary>
public sealed class NetworkRoomManager
{
    public const uint LobbyRoomId = 1;
    public const string LobbyMapName = "LobbyScene";
    private const uint DefaultMaxPlayers = 8;
    private const uint MaxPlayersLimit = 100;
    private readonly object _gate = new();
    private readonly Dictionary<uint, Room> _rooms = new();
    private uint _nextRoomId = LobbyRoomId;

    public static NetworkRoomManager Instance { get; } = new();

    private NetworkRoomManager()
    {
        _rooms[LobbyRoomId] = new Room
        {
            Id = LobbyRoomId,
            Name = "GlobalLobby",
            MapName = LobbyMapName,
            MaxPlayers = uint.MaxValue,
            IsLobby = true
        };
    }

    private sealed class Room
    {
        public uint Id;
        public string LevelKey = string.Empty;
        public string Name = string.Empty;
        public string MapName = string.Empty;
        /// <summary>游戏房间所属逻辑服务器，防止跨服匹配。</summary>
        public string ServerId = ServerScope.DefaultServerId;
        public uint MaxPlayers;
        public string HostSessionId = string.Empty;
        public bool Started;
        public bool IsLobby;
        public readonly HashSet<string> Members = new();
        public readonly HashSet<string> Reservations = new();
        public readonly HashSet<string> ReadyMembers = new();
        /// <summary>是否只向说话者一定距离内的成员转发语音。</summary>
        public bool VoiceAoiEnabled = true;
        /// <summary>是否只允许相同语音队伍的成员互相通话。</summary>
        public bool VoiceTeamOnly;
        /// <summary>会话 ID 到语音队伍 ID 的映射，由房主或服务端对战逻辑分配。</summary>
        public readonly Dictionary<string, string> VoiceTeams = new();
        /// <summary>本次主动切图中尚未报告场景加载完成的房间成员。</summary>
        public readonly HashSet<string> MapTransitionPendingMembers = new();
        public bool MapTransitionActive;
    }

    public NetworkRoomData JoinLobbyNow(PlayerSession session)
    {
        lock (_gate)
        {
            Room lobby = _rooms[LobbyRoomId];
            lobby.Members.Add(session.PlayerId);
            session.NetworkRoomId = LobbyRoomId;
            session.PendingNetworkRoomId = 0;
            return ToData(lobby, session.ServerId);
        }
    }

    public NetworkRoomData ReserveLobby(PlayerSession session)
    {
        lock (_gate)
        {
            Room lobby = _rooms[LobbyRoomId];
            lobby.Reservations.Add(session.PlayerId);
            session.PendingNetworkRoomId = LobbyRoomId;
            return ToData(lobby, session.ServerId);
        }
    }

    public bool TryCreate(PlayerSession session, NetworkRoomData? roomData, out NetworkRoomData data, out string error)
    {
        data = null!;
        if (!ValidateCreateData(session, roomData, out error)) return false;
        lock (_gate)
        {
            if (session.NetworkRoomId != 0 || session.PendingNetworkRoomId != 0)
            { error = "玩家仍在其他房间中。"; return false; }

            uint id = NextRoomId();
            var room = new Room
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(roomData!.RoomName) ? $"Room_{id}" : roomData.RoomName.Trim(),
                MapName = roomData.MapName.Trim(),
                LevelKey = roomData.LevelKey.Trim(),
                MaxPlayers = ClampMaxPlayers(roomData.MaxPlayers),
                HostSessionId = session.PlayerId,
                ServerId = session.ServerId ?? ServerScope.DefaultServerId
            };
            room.Reservations.Add(session.PlayerId);
            _rooms.Add(id, room);
            session.PendingNetworkRoomId = id;
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public bool TryMatch(PlayerSession session, NetworkRoomData? roomData, out NetworkRoomData data, out string error)
    {
        data = null!;
        if (!ValidateCreateData(session, roomData, out error)) return false;
        lock (_gate)
        {
            if (session.NetworkRoomId != 0 || session.PendingNetworkRoomId != 0)
            { error = "玩家仍在其他房间中。"; return false; }

            string map = roomData!.MapName.Trim();
            string levelKey = roomData.LevelKey.Trim();
            Room? room = _rooms.Values
                .Where(r => !r.IsLobby && !r.Started && !r.MapTransitionActive &&
                            string.Equals(r.MapName, map, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(r.LevelKey, levelKey, StringComparison.Ordinal) &&
                            r.ServerId == (session.ServerId ?? ServerScope.DefaultServerId) &&
                            r.Members.Count + r.Reservations.Count < r.MaxPlayers)
                .OrderByDescending(r => r.Members.Count)
                .FirstOrDefault();
            if (room == null)
            {
                uint id = NextRoomId();
                room = new Room { Id = id,
                    Name = string.IsNullOrWhiteSpace(roomData.RoomName) ? $"Match_{id}" : roomData.RoomName.Trim(),
                    MapName = map, LevelKey = levelKey,
                    MaxPlayers = ClampMaxPlayers(roomData.MaxPlayers), HostSessionId = session.PlayerId,
                    ServerId = session.ServerId ?? ServerScope.DefaultServerId };
                _rooms.Add(id, room);
            }
            room.Reservations.Add(session.PlayerId);
            session.PendingNetworkRoomId = room.Id;
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public bool TryReserveInvite(PlayerSession session, uint roomId, out NetworkRoomData data, out string error)
    {
        data = null!;
        lock (_gate)
        {
            if (session.NetworkRoomId != 0 || session.PendingNetworkRoomId != 0)
            { error = "玩家仍在其他房间中。"; return false; }
            if (!_rooms.TryGetValue(roomId, out Room? room) || room.IsLobby || room.Started ||
                room.MapTransitionActive ||
                room.ServerId != (session.ServerId ?? ServerScope.DefaultServerId))
            { error = "邀请的房间不存在或游戏已经开始。"; return false; }
            if (room.Members.Count + room.Reservations.Count >= room.MaxPlayers)
            { error = "房间人数已满。"; return false; }
            room.Reservations.Add(session.PlayerId);
            session.PendingNetworkRoomId = room.Id;
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public bool TryReserveJoin(PlayerSession session, uint roomId, out NetworkRoomData data, out string error)
    {
        data = null!;
        lock (_gate)
        {
            if (session.NetworkRoomId != 0 || session.PendingNetworkRoomId != 0)
            {
                error = "玩家仍在其他房间中。";
                return false;
            }
            if (roomId == LobbyRoomId || !_rooms.TryGetValue(roomId, out Room? room) ||
                room.IsLobby || room.Started || room.MapTransitionActive ||
                room.ServerId != (session.ServerId ?? ServerScope.DefaultServerId))
            {
                error = "房间不存在或当前不可加入。";
                return false;
            }
            if (room.Members.Count + room.Reservations.Count >= room.MaxPlayers)
            {
                error = "房间人数已满。";
                return false;
            }

            room.Reservations.Add(session.PlayerId);
            session.PendingNetworkRoomId = room.Id;
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public bool TryCompleteJoin(
        PlayerSession session,
        uint roomId,
        out NetworkRoomData data,
        out bool allPlayersEntered,
        out string error)
    {
        data = null!;
        allPlayersEntered = false;
        lock (_gate)
        {
            // 主动切图不会让玩家离开原房间，只等待每个成员重新报告场景就绪。
            if (roomId != 0 &&
                session.NetworkRoomId == roomId &&
                _rooms.TryGetValue(roomId, out Room? switchingRoom) &&
                switchingRoom.MapTransitionActive &&
                switchingRoom.Members.Contains(session.PlayerId))
            {
                if (!switchingRoom.MapTransitionPendingMembers.Remove(session.PlayerId))
                {
                    error = "该玩家已经确认进入目标地图。";
                    return false;
                }

                allPlayersEntered = switchingRoom.MapTransitionPendingMembers.Count == 0;
                if (allPlayersEntered)
                    switchingRoom.MapTransitionActive = false;
                data = ToData(switchingRoom);
                error = string.Empty;
                return true;
            }

            if (roomId == 0 || session.PendingNetworkRoomId != roomId || !_rooms.TryGetValue(roomId, out Room? room) ||
                !room.Reservations.Remove(session.PlayerId))
            { error = "房间预留不存在或已经失效。"; return false; }
            if (room.Members.Count >= room.MaxPlayers)
            { session.PendingNetworkRoomId = 0; CleanupIfEmpty(room); error = "房间人数已满。"; return false; }
            room.Members.Add(session.PlayerId);
            session.PendingNetworkRoomId = 0;
            session.NetworkRoomId = room.Id;
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 房主发起整房切图。成员仍属于原房间，服务端记录一份需要重新确认场景就绪的成员快照。
    /// </summary>
    public bool TryBeginMapChange(
        PlayerSession host,
        string? mapName,
        out NetworkRoomData data,
        out PlayerSession[] members,
        out string error)
    {
        data = null!;
        members = Array.Empty<PlayerSession>();
        lock (_gate)
        {
            if (!TryGetGameRoomMember(host, out Room? room, out error))
                return false;
            if (room.HostSessionId != host.PlayerId)
            {
                error = "只有房主可以切换地图。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(mapName))
            {
                error = "地图名称不能为空。";
                return false;
            }
            if (room.MapTransitionActive)
            {
                error = "房间正在切换地图。";
                return false;
            }

            PlayerSession[] connectedMembers = room.Members
                .Select(PlayerSessionManager.Instance.GetSession)
                .Where(member => member != null && member.IsConnected)
                .Cast<PlayerSession>()
                .ToArray();
            if (connectedMembers.Length == 0)
            {
                error = "房间内没有可切换地图的玩家。";
                return false;
            }

            room.MapName = mapName.Trim();
            room.Started = false;
            room.ReadyMembers.Clear();
            room.MapTransitionPendingMembers.Clear();
            foreach (PlayerSession member in connectedMembers)
                room.MapTransitionPendingMembers.Add(member.PlayerId);
            room.MapTransitionActive = true;

            data = ToData(room);
            members = connectedMembers;
            error = string.Empty;
            return true;
        }
    }

    public bool TrySetReady(PlayerSession session, bool ready, out NetworkRoomData data, out string error)
    {
        data = null!;
        lock (_gate)
        {
            if (!TryGetGameRoomMember(session, out Room? room, out error)) return false;
            if (room.MapTransitionActive) { error = "房间正在切换地图。"; return false; }
            if (ready) room.ReadyMembers.Add(session.PlayerId); else room.ReadyMembers.Remove(session.PlayerId);
            data = ToData(room);
            return true;
        }
    }

    /// <summary>由房主在等待阶段修改房间名称、人数上限和关卡标识。</summary>
    public bool TryUpdateRoom(
        PlayerSession session,
        NetworkRoomData? roomData,
        out NetworkRoomData data,
        out string error)
    {
        data = null!;
        lock (_gate)
        {
            if (!TryGetGameRoomMember(session, out Room? room, out error)) return false;
            if (room.HostSessionId != session.PlayerId)
            { error = "只有房主可以修改房间信息。"; return false; }
            if (room.Started || room.MapTransitionActive)
            { error = "游戏已开始或房间正在切换地图，不能修改房间信息。"; return false; }
            if (roomData == null)
            { error = "房间信息不能为空。"; return false; }
            if (roomData.RoomId != 0 && roomData.RoomId != room.Id)
            { error = "提交的房间 ID 与当前房间不一致。"; return false; }

            string roomName = roomData.RoomName.Trim();
            if (string.IsNullOrWhiteSpace(roomName) || roomName.Length > 50)
            { error = "房间名称长度必须为 1 到 50 个字符。"; return false; }
            if (string.IsNullOrWhiteSpace(roomData.LevelKey) || roomData.LevelKey.Length > 100)
            { error = "关卡标识长度必须为 1 到 100 个字符。"; return false; }

            uint maxPlayers = ClampMaxPlayers(roomData.MaxPlayers);
            uint occupiedCount = (uint)(room.Members.Count + room.Reservations.Count);
            if (maxPlayers < occupiedCount)
            { error = "房间人数上限不能小于当前成员和待进入玩家数量。"; return false; }

            room.Name = roomName;
            room.LevelKey = roomData.LevelKey.Trim();
            room.MaxPlayers = maxPlayers;
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public bool TryStart(PlayerSession session, out NetworkRoomData data, out string error)
    {
        data = null!;
        lock (_gate)
        {
            if (!TryGetGameRoomMember(session, out Room? room, out error)) return false;
            if (room.MapTransitionActive) { error = "房间正在切换地图。"; return false; }
            if (room.HostSessionId != session.PlayerId) { error = "只有房主可以开始游戏。"; return false; }
            if (room.Members.Count == 0 || room.Members.Any(id => !room.ReadyMembers.Contains(id)))
            { error = "还有玩家没有准备。"; return false; }
            room.Started = true;
            data = ToData(room);
            return true;
        }
    }

    public bool TryKick(PlayerSession host, PlayerSession target, out uint roomId, out NetworkRoomData data, out string error)
    {
        roomId = 0; data = null!;
        lock (_gate)
        {
            if (!TryGetGameRoomMember(host, out Room? room, out error)) return false;
            if (room.MapTransitionActive) { error = "房间正在切换地图。"; return false; }
            if (room.HostSessionId != host.PlayerId) { error = "只有房主可以踢人。"; return false; }
            if (target == host || !room.Members.Contains(target.PlayerId)) { error = "目标玩家不在房间中。"; return false; }
            roomId = room.Id;
            RemoveMember(room, target);
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public bool CanInvite(PlayerSession inviter, uint roomId, out NetworkRoomData data, out string error)
    {
        data = null!;
        lock (_gate)
        {
            if (inviter.NetworkRoomId != roomId || !_rooms.TryGetValue(roomId, out Room? room) ||
                room.IsLobby || room.Started || room.MapTransitionActive)
            { error = "当前房间不能发送邀请。"; return false; }
            if (room.Members.Count + room.Reservations.Count >= room.MaxPlayers)
            { error = "房间人数已满。"; return false; }
            data = ToData(room);
            error = string.Empty;
            return true;
        }
    }

    public uint Leave(PlayerSession session)
    {
        return Leave(session, out _);
    }

    /// <summary>
    /// 移除成员；如果该成员是切图批次中最后一个尚未完成的人，则返回剩余成员的全员到齐状态。
    /// </summary>
    public uint Leave(PlayerSession session, out NetworkRoomData? mapTransitionCompletedRoom)
    {
        mapTransitionCompletedRoom = null;
        lock (_gate)
        {
            uint id = session.NetworkRoomId != 0 ? session.NetworkRoomId : session.PendingNetworkRoomId;
            if (id != 0 && _rooms.TryGetValue(id, out Room? room))
            {
                bool wasPendingMapTransition =
                    room.MapTransitionActive &&
                    room.MapTransitionPendingMembers.Contains(session.PlayerId);
                RemoveMember(room, session);
                room.Reservations.Remove(session.PlayerId);
                if (wasPendingMapTransition &&
                    !room.MapTransitionActive &&
                    room.Members.Count > 0)
                {
                    mapTransitionCompletedRoom = ToData(room);
                }
                CleanupIfEmpty(room);
            }
            session.NetworkRoomId = 0;
            session.PendingNetworkRoomId = 0;
            return id;
        }
    }

    public bool TryGetRoomData(uint roomId, out NetworkRoomData data)
    {
        return TryGetRoomData(roomId, null, out data);
    }

    /// <summary>由房主修改当前游戏房间的 AOI 和队伍语音规则。</summary>
    public bool TrySetVoiceOptions(
        PlayerSession host,
        bool aoiEnabled,
        bool teamOnly,
        out string error)
    {
        lock (_gate)
        {
            if (!TryGetGameRoomMember(host, out Room? room, out error))
                return false;
            if (room.HostSessionId != host.PlayerId)
            {
                error = "只有房主可以修改房间语音模式。";
                return false;
            }

            room.VoiceAoiEnabled = aoiEnabled;
            room.VoiceTeamOnly = teamOnly;
            error = string.Empty;
            return true;
        }
    }

    /// <summary>由房主为房间成员分配服务端权威的语音队伍。</summary>
    public bool TrySetVoiceTeam(
        PlayerSession host,
        PlayerSession target,
        string? teamId,
        out string error)
    {
        lock (_gate)
        {
            if (!TryGetGameRoomMember(host, out Room? room, out error))
                return false;
            if (room.HostSessionId != host.PlayerId)
            {
                error = "只有房主可以分配语音队伍。";
                return false;
            }
            if (!room.Members.Contains(target.PlayerId))
            {
                error = "目标玩家不在当前房间。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(teamId) || teamId.Length > 32)
            {
                error = "语音队伍 ID 无效。";
                return false;
            }

            room.VoiceTeams[target.PlayerId] = teamId.Trim();
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// 判断接收者是否有权收到发送者的语音，并返回本次播放是否需要 AOI 距离判断。
    /// </summary>
    public bool CanReceiveVoice(
        PlayerSession sender,
        PlayerSession receiver,
        out bool useAoi)
    {
        useAoi = true;
        lock (_gate)
        {
            if (sender.NetworkRoomId <= LobbyRoomId ||
                sender.NetworkRoomId != receiver.NetworkRoomId ||
                !_rooms.TryGetValue(sender.NetworkRoomId, out Room? room) ||
                !room.Members.Contains(sender.PlayerId) ||
                !room.Members.Contains(receiver.PlayerId))
                return false;

            useAoi = room.VoiceAoiEnabled;
            if (!room.VoiceTeamOnly)
                return true;

            return room.VoiceTeams.TryGetValue(sender.PlayerId, out string? senderTeam) &&
                   room.VoiceTeams.TryGetValue(receiver.PlayerId, out string? receiverTeam) &&
                   string.Equals(senderTeam, receiverTeam, StringComparison.Ordinal);
        }
    }

    public NetworkRoomData[] GetJoinableRooms(string? serverId)
    {
        string normalizedServerId = ServerScope.Normalize(serverId);
        lock (_gate)
        {
            return _rooms.Values
                .Where(room => !room.IsLobby &&
                               !room.Started &&
                               !room.MapTransitionActive &&
                               room.ServerId == normalizedServerId &&
                               room.Members.Count + room.Reservations.Count < room.MaxPlayers)
                .OrderBy(room => room.Id)
                .Select(room => ToData(room))
                .ToArray();
        }
    }

    public bool TryGetRoomData(uint roomId, string? serverId, out NetworkRoomData data)
    {
        lock (_gate)
        {
            if (_rooms.TryGetValue(roomId, out Room? room))
            {
                data = ToData(room, room.IsLobby ? ServerScope.Normalize(serverId) : null);
                return true;
            }
            data = null!;
            return false;
        }
    }

    public bool IsHost(PlayerSession session)
    {
        lock (_gate)
            return session.NetworkRoomId != 0 && _rooms.TryGetValue(session.NetworkRoomId, out Room? room) &&
                   room.HostSessionId == session.PlayerId;
    }

    private static bool TryGetGameRoomMember(PlayerSession session, out Room room, out string error)
    {
        if (session.NetworkRoomId != 0 && Instance._rooms.TryGetValue(session.NetworkRoomId, out room!) &&
            !room.IsLobby && room.Members.Contains(session.PlayerId))
        { error = string.Empty; return true; }
        room = null!; error = "玩家不在有效游戏房间中。"; return false;
    }

    private static void RemoveMember(Room room, PlayerSession session)
    {
        room.Members.Remove(session.PlayerId);
        room.ReadyMembers.Remove(session.PlayerId);
        room.VoiceTeams.Remove(session.PlayerId);
        room.MapTransitionPendingMembers.Remove(session.PlayerId);
        if (room.MapTransitionActive && room.MapTransitionPendingMembers.Count == 0)
            room.MapTransitionActive = false;
        if (room.HostSessionId == session.PlayerId)
            room.HostSessionId = room.Members.FirstOrDefault() ?? string.Empty;
    }

    private static bool ValidateCreateData(PlayerSession session, NetworkRoomData? roomData, out string error)
    {
        if (string.IsNullOrWhiteSpace(session.UserId))
        { error = "玩家尚未登录。"; return false; }
        if (roomData == null || string.IsNullOrWhiteSpace(roomData.MapName))
        { error = "地图名称不能为空。"; return false; }
        if (roomData.RoomName.Length > 50)
        { error = "房间名称不能超过 50 个字符。"; return false; }
        if (string.IsNullOrWhiteSpace(roomData.LevelKey) || roomData.LevelKey.Length > 100)
        { error = "关卡标识长度必须为 1 到 100 个字符。"; return false; }
        error = string.Empty;
        return true;
    }

    private uint NextRoomId() { do { _nextRoomId++; } while (_nextRoomId == 0 || _rooms.ContainsKey(_nextRoomId)); return _nextRoomId; }
    private static uint ClampMaxPlayers(uint value) => Math.Clamp(value == 0 ? DefaultMaxPlayers : value, 1u, MaxPlayersLimit);
    private void CleanupIfEmpty(Room room)
    {
        if (!room.IsLobby && room.Members.Count == 0 && room.Reservations.Count == 0 && _rooms.Remove(room.Id))
        {
            RoomSharedValueManager.Instance.RemoveRoom(room.ServerId, room.Id);
            _ = NetworkRoomObjectManager.Instance.DestroyRoomObjectsAsync(room.Id);
        }
    }

    private static NetworkRoomData ToData(Room room, string? serverId = null)
    {
        var visibleMembers = room.Members
            .Where(sessionId => !room.IsLobby ||
                PlayerSessionManager.Instance.GetSession(sessionId)?.ServerId == serverId)
            .ToArray();
        var data = new NetworkRoomData { RoomId = room.Id, RoomName = room.Name, MapName = room.MapName,
            MaxPlayers = room.MaxPlayers, PlayerCount = (uint)visibleMembers.Length, Started = room.Started,
            LevelKey = room.LevelKey };
        PlayerSession? host = PlayerSessionManager.Instance.GetSession(room.HostSessionId);
        data.HostPlayerId = host?.UserId ?? string.Empty;
        foreach (string sessionId in visibleMembers)
        {
            PlayerSession? member = PlayerSessionManager.Instance.GetSession(sessionId);
            if (member?.UserId == null) continue;
            data.Members.Add(new NetworkRoomMemberData
            {
                PlayerId = member.UserId,
                ObjectId = member.NetworkObjectId,
                Ready = room.ReadyMembers.Contains(sessionId),
                Host = sessionId == room.HostSessionId,
                UserData = member.UserDataSnapshot?.Clone(),
            });
        }
        return data;
    }
}
