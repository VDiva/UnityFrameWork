using System.Collections.Concurrent;
using GameData;
using Google.Protobuf;

namespace WebSocketDemo
{
    public class PlayerSessionManager : IDisposable
    {
        private const int MaxOnlineSessions = 2000;
        private const int BroadcastConcurrency = 64;
        private static readonly TimeSpan BoundIdleTimeout = TimeSpan.FromMinutes(10);

        private static readonly Lazy<PlayerSessionManager> _instance =
            new Lazy<PlayerSessionManager>(() => new PlayerSessionManager());

        public static PlayerSessionManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, PlayerSession> _sessions;
        private readonly ConcurrentDictionary<string, string> _userSessions;
        private int _nextNetworkObjectId;
        private bool _disposed;

        private PlayerSessionManager()
        {
            _sessions = new ConcurrentDictionary<string, PlayerSession>();
            _userSessions = new ConcurrentDictionary<string, string>();
        }

        public int OnlineCount => _sessions.Count;
        public bool CanAcceptSession => OnlineCount < MaxOnlineSessions;

        public bool TryAddSession(string playerId, PlayerSession session)
        {
            if (!CanAcceptSession)
            {
                Console.WriteLine($"Reject session {playerId}: online limit reached ({MaxOnlineSessions}).");
                return false;
            }

            if (!_sessions.TryAdd(playerId, session))
            {
                Console.WriteLine($"Reject session {playerId}: duplicate session id.");
                return false;
            }

            Console.WriteLine($"Player {playerId} online, current online: {_sessions.Count}");
            return true;
        }

        public async Task BindUserSessionAsync(string userId, string serverId, PlayerSession session)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var newPlayerId = session.PlayerId;
            if (!_sessions.ContainsKey(newPlayerId))
            {
                Console.WriteLine($"Bind user ignored: user={userId}, session={newPlayerId} is not in online sessions.");
                return;
            }

            Console.WriteLine($"Bind user session: user={userId}, server={serverId}, session={newPlayerId}, online={_sessions.Count}.");

            // 在线唯一性只看账号 id：新连接先接管账号索引，再通知并关闭旧连接。
            if (_userSessions.TryGetValue(userId, out var oldPlayerId) &&
                oldPlayerId != newPlayerId &&
                _sessions.TryRemove(oldPlayerId, out var oldSession))
            {
                _userSessions[userId] = newPlayerId;
                // 先隔离旧 socket，权威清理仍在新登录之前完成，不等旧网络恢复。
                Task closing = oldSession.CloseForReplacementAsync();
                await ReleaseNetworkObjectAsync(oldSession);
                Console.WriteLine(
                    $"User {userId} logged in again, kick old session {oldPlayerId}, new session {newPlayerId}.");

                // 旧连接已中止，不再等待提示送达。
                _ = FinishReplacedSessionAsync(oldSession, closing);
                Console.WriteLine($"Player {oldPlayerId} offline, current online: {_sessions.Count}");
            }

            _userSessions[userId] = newPlayerId;
        }

        private static async Task KickOldSessionAsync(PlayerSession oldSession)
        {
            try
            {
                var kickMsg = new Msg
                {
                    MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.Tips,
                    TipsSrt = "账号已在其他地方登录，当前连接即将断开。",
                    ServerId = oldSession.ServerId ?? ServerScope.DefaultServerId
                };

                await oldSession.SendBinaryAsync(kickMsg.ToByteArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send kick message failed: {oldSession.PlayerId}, {ex.Message}");
            }

            try
            {
                await oldSession.CloseAsync(
                    System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation,
                    "Logged in elsewhere");
            }
            finally
            {
                oldSession.Dispose();
            }
        }

        private static async Task FinishReplacedSessionAsync(PlayerSession session, Task closing)
        {
            try { await closing; }
            catch (Exception exception) { Console.WriteLine(exception.Message); }
            finally { session.Dispose(); }
        }

        public void AddSession(string playerId, PlayerSession session)
        {
            if (!TryAddSession(playerId, session))
            {
                session.Dispose();
            }
        }

        public void RemoveSession(string playerId)
        {
            _ = RemoveSessionAsync(playerId);
        }

        public async Task RemoveSessionAsync(string playerId)
        {
            if (_sessions.TryRemove(playerId, out var session))
            {
                RemoveUserSessionIndex(session);
                await CleanupRemovedSessionAsync(session);
                Console.WriteLine($"Player {playerId} offline, current online: {_sessions.Count}");
            }
        }

        public PlayerSession? GetSession(string playerId)
        {
            _sessions.TryGetValue(playerId, out var session);
            return session;
        }

        /// <summary>退出角色登录，清理账号索引和房间状态，但保留底层 WebSocket 会话。</summary>
        public async Task UnbindUserSessionAsync(PlayerSession session)
        {
            RemoveUserSessionIndex(session);
            await ReleaseNetworkObjectAsync(session);
        }

        public PlayerSession? GetUserSession(string userId, string serverId)
        {
            if (!_userSessions.TryGetValue(userId, out string? sessionId)) return null;
            var session = GetSession(sessionId);
            return session?.ServerId == ServerScope.Normalize(serverId) ? session : null;
        }

        public PlayerSession[] GetRoomSessions(uint roomId)
        {
            return _sessions.Values.Where(s => s.IsConnected && s.NetworkRoomId == roomId).ToArray();
        }

        public bool HasOnlineUser(string userId, string serverId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return _userSessions.TryGetValue(userId, out var playerId) &&
                   _sessions.TryGetValue(playerId, out var session) &&
                   session.ServerId == ServerScope.Normalize(serverId) &&
                   session.IsConnected;
        }

        public int RemoveDisconnectedSessions()
        {
            var removed = 0;
            foreach (var pair in _sessions.ToArray())
            {
                if (pair.Value.IsConnected)
                {
                    continue;
                }

                if (_sessions.TryRemove(pair.Key, out var session))
                {
                    RemoveUserSessionIndex(session);
                    _ = CleanupRemovedSessionAsync(session);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Console.WriteLine($"Cleaned disconnected sessions: {removed}, current online: {_sessions.Count}");
            }

            return removed;
        }

        public int RemoveExpiredSessions()
        {
            var now = DateTimeOffset.UtcNow;
            var removed = 0;

            foreach (var pair in _sessions.ToArray())
            {
                var session = pair.Value;
                // 未登录连接不再自动超时，方便客户端停留在选服界面。
                if (string.IsNullOrWhiteSpace(session.UserId))
                {
                    continue;
                }

                var timeout = BoundIdleTimeout;
                var idleTime = now - session.LastActiveAt;

                if (idleTime <= timeout)
                {
                    continue;
                }

                if (_sessions.TryRemove(pair.Key, out var removedSession))
                {
                    RemoveUserSessionIndex(removedSession);
                    var reason = string.IsNullOrWhiteSpace(removedSession.UserId)
                        ? "login timeout"
                        : "idle timeout";
                    Console.WriteLine(
                        $"Session expired: {removedSession.PlayerId}, user={removedSession.UserId ?? "none"}, reason={reason}, idle={idleTime.TotalSeconds:F1}s.");

                    _ = CleanupExpiredSessionAsync(removedSession);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Console.WriteLine($"Cleaned expired sessions: {removed}, current online: {_sessions.Count}");
            }

            return removed;
        }

        private void RemoveUserSessionIndex(PlayerSession session)
        {
            if (string.IsNullOrWhiteSpace(session.UserId))
            {
                return;
            }

            if (_userSessions.TryGetValue(session.UserId, out var playerId) &&
                playerId == session.PlayerId)
            {
                ((ICollection<KeyValuePair<string, string>>)_userSessions).Remove(
                    new KeyValuePair<string, string>(session.UserId, session.PlayerId));
            }
        }

        public async Task BroadcastAsync(string message)
        {
            using var concurrency = new SemaphoreSlim(BroadcastConcurrency, BroadcastConcurrency);
            var tasks = _sessions.Values
                .Where(s => s.IsConnected)
                .Select(async session =>
                {
                    await concurrency.WaitAsync();
                    try
                    {
                        await session.SendMessageAsync(message);
                    }
                    finally
                    {
                        concurrency.Release();
                    }
                });

            await Task.WhenAll(tasks);
        }

        /// <summary>向所有已登录连接广播二进制消息，可排除发送者自身。</summary>
        public Task BroadcastBinaryAsync(
            byte[] data, string? exceptPlayerId = null, uint roomId = 0, string? serverId = null)
        {
            foreach (PlayerSession session in _sessions.Values)
            {
                if (session.IsConnected && !string.IsNullOrWhiteSpace(session.UserId) &&
                    session.PlayerId != exceptPlayerId &&
                    (serverId == null || session.ServerId == serverId) &&
                    (roomId == 0 || session.NetworkRoomId == roomId))
                    _ = session.SendBinaryAsync(data);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 广播可丢帧的实时状态。每个接收连接对同一个 key 只保留最新消息，且不等待
        /// 实际 socket 写入，避免最慢客户端阻塞发送者的接收循环。
        /// </summary>
        public void BroadcastLatestBinary(
            ulong key,
            byte[] data,
            string? exceptPlayerId = null,
            uint roomId = 0,
            string? serverId = null)
        {
            foreach (PlayerSession session in _sessions.Values)
            {
                if (session.IsConnected &&
                    !string.IsNullOrWhiteSpace(session.UserId) &&
                    session.PlayerId != exceptPlayerId &&
                    (serverId == null || session.ServerId == serverId) &&
                    (roomId == 0 || session.NetworkRoomId == roomId))
                {
                    session.QueueLatestBinary(key, data);
                }
            }
        }

        /// <summary>广播只需要最新版本的可靠状态，不等待 socket 写完。</summary>
        public void BroadcastLatestControlBinary(
            ulong key, byte[] data, string? exceptPlayerId = null,
            uint roomId = 0, string? serverId = null)
        {
            foreach (PlayerSession session in _sessions.Values)
            {
                if (session.IsConnected && !string.IsNullOrWhiteSpace(session.UserId) &&
                    session.PlayerId != exceptPlayerId &&
                    (serverId == null || session.ServerId == serverId) &&
                    (roomId == 0 || session.NetworkRoomId == roomId))
                    session.QueueLatestControlBinary(key, data);
            }
        }

        /// <summary>为已登录会话分配类似 Mirror netId 的进程内唯一大厅对象 ID。</summary>
        public uint EnsureNetworkObjectId(PlayerSession session)
        {
            if (session.NetworkObjectId != 0)
                return session.NetworkObjectId;

            uint id = AllocateNetworkObjectId();
            session.NetworkObjectId = id;
            return id;
        }

        public uint AllocateNetworkObjectId()
        {
            uint id;
            do
            {
                id = unchecked((uint)Interlocked.Increment(ref _nextNetworkObjectId));
            } while (id == 0);
            return id;
        }

        /// <summary>向新玩家发送现有对象，并向其他玩家广播新对象。</summary>
        public async Task AnnounceNetworkObjectAsync(PlayerSession session)
        {
            if (session.NetworkObjectId == 0 || session.NetworkRoomId == 0 || string.IsNullOrWhiteSpace(session.UserId))
                return;

            if (session.NetworkRoomId == NetworkRoomManager.LobbyRoomId)
            {
                await UpdateLobbyAoiAsync(session);
                return;
            }

            await NetworkRoomObjectManager.Instance.SendRoomObjectsToAsync(session);

            foreach (PlayerSession existing in _sessions.Values)
            {
                if (existing == session || existing.NetworkObjectId == 0 ||
                    existing.ServerId != session.ServerId ||
                    existing.NetworkRoomId != session.NetworkRoomId || string.IsNullOrWhiteSpace(existing.UserId))
                    continue;

                await session.SendBinaryAsync(CreateNetworkObjectMessage(
                    ServerMsgType.NetworkObjectSpawn,
                    existing.NetworkObjectId,
                    existing.UserId));

                if (existing.LastNetworkTransform != null)
                {
                    await session.SendBinaryAsync(new Msg
                    {
                        MsgType = ProtobufMsgType.Server,
                        ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                        NetworkTransform = existing.LastNetworkTransform.Clone()
                    }.ToByteArray());
                }

                if (existing.LastNetworkAnimation != null)
                {
                    await session.SendBinaryAsync(new Msg
                    {
                        MsgType = ProtobufMsgType.Server,
                        ServerMsgType = ServerMsgType.NetworkAnimationUpdate,
                        NetworkAnimation = existing.LastNetworkAnimation.Clone()
                    }.ToByteArray());
                }
                await NetworkSyncVarManager.Instance.SendObjectStateAsync(session, existing.NetworkObjectId);
            }

            await BroadcastBinaryAsync(
                CreateNetworkObjectMessage(ServerMsgType.NetworkObjectSpawn, session.NetworkObjectId, session.UserId),
                session.PlayerId,
                session.NetworkRoomId,
                session.ServerId);
        }

        public Task LeaveNetworkRoomAsync(PlayerSession session, bool waitForNotifications = true)
        {
            uint roomId = session.NetworkRoomId;
            NetworkRoomManager.Instance.Leave(session, out NetworkRoomData? mapTransitionCompletedRoom);
            session.LastNetworkTransform = null;
            session.LastNetworkAnimation = null;
            NetworkSyncVarManager.Instance.RemoveObject(session.NetworkObjectId);
            Task notifications = NotifyRoomLeaveAsync(session, roomId, mapTransitionCompletedRoom);
            if (waitForNotifications)
                return notifications;
            _ = ObserveBackgroundTaskAsync(notifications, "room leave notifications");
            return Task.CompletedTask;
        }

        private async Task NotifyRoomLeaveAsync(
            PlayerSession session, uint roomId, NetworkRoomData? mapTransitionCompletedRoom)
        {
            if (roomId != 0 && roomId != NetworkRoomManager.LobbyRoomId)
                await NetworkRoomObjectManager.Instance.HandleOwnerLeavingAsync(session, roomId);
            await DespawnFromRoomAsync(session, roomId);
            if (mapTransitionCompletedRoom != null)
                await BroadcastAllPlayersEnteredAsync(mapTransitionCompletedRoom);
        }

        private static async Task ObserveBackgroundTaskAsync(Task task, string operation)
        {
            try { await task; }
            catch (Exception exception)
            {
                Console.WriteLine($"Background {operation} failed: {exception.Message}");
            }
        }

        public async Task DespawnFromRoomAsync(PlayerSession session, uint roomId)
        {
            if (roomId == 0 || session.NetworkObjectId == 0) return;
            uint objectId = session.NetworkObjectId;
            byte[] despawn = CreateNetworkObjectMessage(ServerMsgType.NetworkObjectDespawn,
                objectId, session.UserId ?? string.Empty);
            if (roomId != NetworkRoomManager.LobbyRoomId)
            {
                await BroadcastBinaryAsync(despawn, session.PlayerId, roomId, session.ServerId);
                return;
            }

            foreach (PlayerSession other in _sessions.Values)
            {
                if (other == session) continue;
                if (other.VisibleNetworkObjects.TryRemove(objectId, out _))
                    await other.SendBinaryAsync(despawn);
                session.VisibleNetworkObjects.TryRemove(other.NetworkObjectId, out _);
            }
            session.VisibleNetworkObjects.Clear();
        }

        public async Task RelayNetworkTransformAsync(PlayerSession sender, NetworkTransformData snapshot)
        {
            sender.LastNetworkTransform = snapshot.Clone();
            if (sender.NetworkRoomId == NetworkRoomManager.LobbyRoomId)
            {
                await UpdateLobbyAoiAsync(sender);
                byte[] bytes = new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                    NetworkTransform = snapshot }.ToByteArray();
                SendToObserversLatest(sender, CreateRealtimeKey(ServerMsgType.NetworkTransformUpdate,
                    snapshot.ObjectId), bytes);
            }
            else
            {
                byte[] bytes = new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                    NetworkTransform = snapshot }.ToByteArray();
                BroadcastLatestBinary(CreateRealtimeKey(ServerMsgType.NetworkTransformUpdate, snapshot.ObjectId),
                    bytes, sender.PlayerId, sender.NetworkRoomId, sender.ServerId);
            }
        }

        public Task RelayNetworkAnimationAsync(PlayerSession sender, NetworkAnimationData snapshot)
        {
            sender.LastNetworkAnimation = snapshot.Clone();
            byte[] bytes = new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkAnimationUpdate,
                NetworkAnimation = snapshot }.ToByteArray();
            if (sender.NetworkRoomId == NetworkRoomManager.LobbyRoomId)
                SendToObserversLatest(sender, CreateRealtimeKey(ServerMsgType.NetworkAnimationUpdate,
                    snapshot.ObjectId, snapshot.TrackIndex), bytes);
            else
                BroadcastLatestBinary(CreateRealtimeKey(ServerMsgType.NetworkAnimationUpdate, snapshot.ObjectId, snapshot.TrackIndex),
                    bytes, sender.PlayerId, sender.NetworkRoomId, sender.ServerId);
            return Task.CompletedTask;
        }

        private async Task UpdateLobbyAoiAsync(PlayerSession sender)
        {
            const float visibleDistance = 40f;
            NetworkTransformData? a = sender.LastNetworkTransform;
            if (a == null) return;
            float maxDistanceSqr = visibleDistance * visibleDistance;

            foreach (PlayerSession other in _sessions.Values)
            {
                if (other == sender || other.NetworkRoomId != NetworkRoomManager.LobbyRoomId ||
                    other.ServerId != sender.ServerId ||
                    other.NetworkObjectId == 0 || other.LastNetworkTransform == null) continue;
                NetworkTransformData b = other.LastNetworkTransform;
                float dx = a.PositionX - b.PositionX;
                float dy = a.PositionY - b.PositionY;
                float dz = a.PositionZ - b.PositionZ;
                bool visible = dx * dx + dy * dy + dz * dz <= maxDistanceSqr;

                if (visible)
                {
                    if (sender.VisibleNetworkObjects.TryAdd(other.NetworkObjectId, 0))
                        await SendSpawnAndStateAsync(sender, other);
                    if (other.VisibleNetworkObjects.TryAdd(sender.NetworkObjectId, 0))
                        await SendSpawnAndStateAsync(other, sender);
                }
                else
                {
                    if (sender.VisibleNetworkObjects.TryRemove(other.NetworkObjectId, out _))
                        await sender.SendBinaryAsync(CreateNetworkObjectMessage(ServerMsgType.NetworkObjectDespawn,
                            other.NetworkObjectId, other.UserId ?? string.Empty));
                    if (other.VisibleNetworkObjects.TryRemove(sender.NetworkObjectId, out _))
                        await other.SendBinaryAsync(CreateNetworkObjectMessage(ServerMsgType.NetworkObjectDespawn,
                            sender.NetworkObjectId, sender.UserId ?? string.Empty));
                }
            }
        }

        private static async Task SendSpawnAndStateAsync(PlayerSession receiver, PlayerSession subject)
        {
            await receiver.SendBinaryAsync(CreateNetworkObjectMessage(ServerMsgType.NetworkObjectSpawn,
                subject.NetworkObjectId, subject.UserId ?? string.Empty));
            if (subject.LastNetworkTransform != null)
                await receiver.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkTransformUpdate,
                    NetworkTransform = subject.LastNetworkTransform.Clone() }.ToByteArray());
            if (subject.LastNetworkAnimation != null)
                await receiver.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkAnimationUpdate,
                    NetworkAnimation = subject.LastNetworkAnimation.Clone() }.ToByteArray());
            await NetworkSyncVarManager.Instance.SendObjectStateAsync(receiver, subject.NetworkObjectId);
        }

        private async Task CleanupRemovedSessionAsync(PlayerSession session)
        {
            try
            {
                await ReleaseNetworkObjectAsync(session);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Release disconnected player failed: {session.PlayerId}, {ex.Message}");
            }
            finally
            {
                session.Dispose();
            }
        }

        private async Task CleanupExpiredSessionAsync(PlayerSession session)
        {
            try
            {
                await ReleaseNetworkObjectAsync(session);
                await session.CloseAsync(
                    System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                    "Session timeout");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Release expired player failed: {session.PlayerId}, {ex.Message}");
            }
            finally
            {
                session.Dispose();
            }
        }

        private async Task ReleaseNetworkObjectAsync(PlayerSession session)
        {
            uint objectId = session.NetworkObjectId;
            uint roomId = session.NetworkRoomId;
            string? serverId = session.ServerId;
            if (roomId != 0 && roomId != NetworkRoomManager.LobbyRoomId)
                await NetworkRoomObjectManager.Instance.HandleOwnerLeavingAsync(session, roomId);
            if (objectId != 0 && roomId != 0)
            {
                await DespawnFromRoomAsync(session, roomId);
            }
            session.NetworkObjectId = 0;
            NetworkSyncVarManager.Instance.RemoveObject(objectId);
            session.LastNetworkTransform = null;
            session.LastNetworkAnimation = null;
            NetworkRoomManager.Instance.Leave(session, out NetworkRoomData? mapTransitionCompletedRoom);
            if (roomId != 0 &&
                NetworkRoomManager.Instance.TryGetRoomData(roomId, serverId, out NetworkRoomData remainingRoom))
            {
                await BroadcastBinaryAsync(new Msg
                {
                    MsgType = ProtobufMsgType.Server,
                    ServerMsgType = ServerMsgType.NetworkRoomState,
                    NetworkRoom = remainingRoom
                }.ToByteArray(), null, roomId, serverId);
            }
            if (mapTransitionCompletedRoom != null)
                await BroadcastAllPlayersEnteredAsync(mapTransitionCompletedRoom);
        }

        private void SendToObserversLatest(PlayerSession sender, ulong key, byte[] data)
        {
            foreach (PlayerSession target in _sessions.Values)
                if (target.IsConnected && target.VisibleNetworkObjects.ContainsKey(sender.NetworkObjectId))
                    target.QueueLatestBinary(key, data);
        }

        public static ulong CreateRealtimeKey(ServerMsgType messageType, uint objectId, int trackIndex = 0)
        {
            return ((ulong)objectId << 32) | (messageType == ServerMsgType.NetworkAnimationUpdate
                ? 0x80000000u | ((uint)trackIndex & 0x7fffffffu) : 0u);
        }

        private Task BroadcastAllPlayersEnteredAsync(NetworkRoomData room)
        {
            return BroadcastBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkRoomAllPlayersEntered,
                NetworkRoom = room
            }.ToByteArray(), null, room.RoomId);
        }

        private static byte[] CreateNetworkObjectMessage(ServerMsgType type, uint objectId, string playerId)
        {
            return new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = type,
                NetworkObject = new NetworkObjectData
                {
                    ObjectId = objectId,
                    PlayerId = playerId,
                    OwnerPlayerId = playerId,
                    PlayerObject = true
                }
            }.ToByteArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (var session in _sessions.Values)
            {
                try
                {
                    ReleaseNetworkObjectAsync(session).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Release player during shutdown failed: {session.PlayerId}, {ex.Message}");
                }
                finally
                {
                    session.Dispose();
                }
            }

            _sessions.Clear();
            _userSessions.Clear();
            _disposed = true;
        }
    }
}
