using GameData;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace WebSocketDemo;

public static class MsgMrg
{
    private static readonly AccountLoginGate LoginGates = new();
    // 限制玩家 id 长度，避免异常输入直接进入数据库查询。
    private const int MaxUserIdLength = 64;
    private const int MaxAvatarUrlLength = 2048;
    private const int MaxFriendMessageLength = 500;
    private const int MaxChannelChatMessageLength = 200;
    private const string ShowRewardPanelKey = "__show_reward_panel";
    private const string LoginProviderKey = "__login_provider";
    private static readonly TimeSpan ChannelChatInterval = TimeSpan.FromSeconds(1);


    public static async Task ReceivedSrt(string message)
    {
        try
        {
            if (message.StartsWith("Msg"))
            {
                var sprit=message.Split(";");
                var type = sprit[1];
                switch (type)
                {
                    case "AddServer":
                        var name=sprit[2];
                        var id=sprit[3];
                        await GameDataMrg.AddServers(name,id);
                        break;
                    case "AddGongGao":
                        var gongGao=sprit[2];
                        await GameDataMrg.AddGongGao(gongGao);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
    
    /// <summary>
    /// 统一处理客户端发来的二进制协议包。
    /// 这里先做 protobuf 解析和消息类型分发，具体业务再交给对应方法。
    /// </summary>
    public static async Task Received(byte[] data, PlayerSession player)
    {
        Msg msg;
        try
        {
            msg = Msg.Parser.ParseFrom(data);
        }
        catch (InvalidProtocolBufferException ex)
        {
            Console.WriteLine($"[{player.PlayerId}] Invalid protobuf message: {ex.Message}");
            return;
        }

        await Received(msg, player);
    }

    public static async Task Received(Msg msg, PlayerSession player)
    {
        // 登录后 serverId 固定，拒绝客户端在同一连接中切换服务器。
        if (!string.IsNullOrWhiteSpace(player.ServerId) &&
            !string.IsNullOrWhiteSpace(msg.ServerId) &&
            ServerScope.Normalize(msg.ServerId) != player.ServerId)
        {
            return;
        }

        if (msg.MsgType != ProtobufMsgType.Game)
        {
            return;
        }

        try
        {
            switch (msg.GameMsgType)
            {
                case GameMsgType.Login:
                    await Login(msg, player);
                    break;
                case GameMsgType.Logout:
                    await Logout(player);
                    break;
                case GameMsgType.GetEmail:
                    await GetEmail(player);
                    break;
                case GameMsgType.AddItem:
                    await AddRequestedItems(
                        player, msg.AddItemData, ShouldShowRewardPanel(msg));
                    break;
                case GameMsgType.GetRank:
                    await GetRank(player,msg);
                    break;
                case GameMsgType.AddEquip:
                    await AddRequestedEquips(
                        player, msg.RewardEquip, ShouldShowRewardPanel(msg));
                    break;
                case GameMsgType.DelEquip:
                    break;
                case GameMsgType.UploadNetworkTransform:
                    await RelayNetworkTransform(msg.NetworkTransform, player);
                    break;
                case GameMsgType.UploadNetworkAnimation:
                    await RelayNetworkAnimation(msg.NetworkAnimation, player);
                    break;
                case GameMsgType.CreateNetworkRoom:
                    await CreateRoom(msg.NetworkRoom, player);
                    break;
                case GameMsgType.MatchNetworkRoom:
                    await MatchRoom(msg.NetworkRoom, player);
                    break;
                case GameMsgType.NetworkRoomSceneReady:
                    await CompleteRoomJoin(msg.NetworkRoomRequest?.RoomId ?? 0, player);
                    break;
                case GameMsgType.ChangeNetworkRoomMap:
                    await ChangeRoomMap(msg.NetworkRoomRequest?.MapName, player);
                    break;
                case GameMsgType.LeaveNetworkRoom:
                    await LeaveRoom(player);
                    break;
                case GameMsgType.SetNetworkRoomReady:
                    await SetRoomReady(msg.NetworkRoomRequest?.Ready ?? false, player);
                    break;
                case GameMsgType.UpdateNetworkRoom:
                    await UpdateRoom(msg.NetworkRoom, player);
                    break;
                case GameMsgType.StartNetworkRoomGame:
                    await StartRoomGame(player);
                    break;
                case GameMsgType.KickNetworkRoomPlayer:
                    await KickRoomPlayer(msg.NetworkRoomRequest?.TargetPlayerId ?? string.Empty, player);
                    break;
                case GameMsgType.InviteNetworkRoomPlayer:
                    await InviteRoomPlayer(msg.NetworkRoomRequest?.TargetPlayerId ?? string.Empty, player);
                    break;
                case GameMsgType.AcceptNetworkRoomInvite:
                    await AcceptRoomInvite(msg.NetworkRoomRequest?.RoomId ?? 0, player);
                    break;
                case GameMsgType.JoinNetworkRoom:
                    await JoinRoom(msg.NetworkRoomRequest?.RoomId ?? 0, player);
                    break;
                case GameMsgType.GetNetworkRoomList:
                    await SendRoomList(player);
                    break;
                case GameMsgType.UploadNetworkVoice:
                    await RelayNetworkVoice(msg.Id, player);
                    break;
                case GameMsgType.SetNetworkVoiceOptions:
                    await SetVoiceOptions(
                        msg.NetworkRoomRequest?.Ready ?? true,
                        (msg.NetworkRoomRequest?.MaxPlayers ?? 0) != 0,
                        player);
                    break;
                case GameMsgType.SetNetworkVoiceTeam:
                    await SetVoiceTeam(
                        msg.NetworkRoomRequest?.TargetPlayerId ?? string.Empty,
                        msg.NetworkRoomRequest?.MapName ?? string.Empty,
                        player);
                    break;
                case GameMsgType.SpawnNetworkRoomObject:
                    await NetworkRoomObjectManager.Instance.SpawnAsync(player, msg.NetworkObject, msg.NetworkTransform);
                    break;
                case GameMsgType.DestroyNetworkRoomObject:
                    await NetworkRoomObjectManager.Instance.DestroyAsync(player, msg.NetworkObject?.ObjectId ?? 0);
                    break;
                case GameMsgType.UploadNetworkSyncVar:
                    await NetworkSyncVarManager.Instance.RelayAsync(player, msg.NetworkSyncVar);
                    break;
                case GameMsgType.UploadNetworkRpc:
                    await NetworkRpcManager.Instance.RelayAsync(player, msg.NetworkRpc);
                    break;
                case GameMsgType.SpawnNetworkRoomAi:
                    await NetworkRoomObjectManager.Instance.SpawnAiAsync(player, msg.NetworkObject, msg.NetworkTransform);
                    break;
                case GameMsgType.SpawnNetworkRoomAifromTrigger:
                    await NetworkRoomObjectManager.Instance.SpawnTriggeredAiAsync(
                        player, msg.NetworkObject, msg.NetworkTransform);
                    break;
                case GameMsgType.GetServerList:
                    await GetServerList(msg, player);
                    break;
                case GameMsgType.SetRoleType:
                    await SetRoleType(msg.Id??string.Empty, msg.ServerId, msg.RoleType, player);
                    break;
                case GameMsgType.GetGongGao:
                    await GetGongGao(msg, player);
                    break;
                case GameMsgType.GetAllEmailReward:
                    await GetAllEmailReward(player);
                    break;
                case GameMsgType.DeleteAllGetRewardEmail:
                    await DeleteUseEmail(player);
                    break;
                case GameMsgType.GetEmailReward:
                    await GetEmailReward(player, msg.EmailIds.ToArray());
                    break;
                case GameMsgType.AddUserDataDic:
                    await AddUserDataDic(msg, player);
                    break;
                case GameMsgType.SetAvatar:
                    await SetAvatar(msg, player);
                    break;
                case GameMsgType.AddFriend:
                    await AddFriend(msg, player);
                    break;
                case GameMsgType.DeleteFriend:
                    await DeleteFriend(msg, player);
                    break;
                case GameMsgType.GetFriendList:
                    await GetFriendList(msg, player);
                    break;
                case GameMsgType.SendFriendMessage:
                    await SendFriendMessage(msg, player);
                    break;
                case GameMsgType.SendChannelChatMessage:
                    await SendChannelChatMessage(msg, player);
                    break;
                case GameMsgType.AddRoomSharedValue:
                    await RoomSharedValueManager.Instance.AddAsync(player, msg.RoomSharedValue);
                    break;
                case GameMsgType.ResetRoomSharedValues:
                    await RoomSharedValueManager.Instance.ResetAllAsync(player);
                    break;
                case GameMsgType.SetRoomSharedValue:
                    await RoomSharedValueManager.Instance.SetAsync(player, msg.RoomSharedValue);
                    break;
                case GameMsgType.ClaimNetworkRoomObject:
                    await NetworkRoomObjectManager.Instance.ClaimAsync(player, msg.NetworkObjectId);
                    break;
                case GameMsgType.UpdateRank:
                    await UpdateRank(player, msg);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{player.PlayerId}] Handle message failed: {ex.Message}");
            if (msg.IsRequest())
                await player.ReplyErrorAsync(msg, ex.Message);
        }
    }
    
    
    /// <summary>
    /// 领取所有可领取的奖励邮件
    /// </summary>
    /// <param name="id"></param>
    /// <param name="serverId"></param>
    /// <param name="playerSession"></param>
    private static async Task GetAllEmailReward(PlayerSession playerSession)
    {
        var userId=playerSession.UserId;
        var serverId=playerSession.ServerId;
        if (userId==null|| serverId==null)return;
        var emailData =await GetEmailData(playerSession);
        await GetEmailReward(playerSession, emailData);
    }

    /// <summary>
    /// 获取邮件奖励
    /// </summary>
    /// <param name="playerSession"></param>
    /// <param name="emailData"></param>
    public static async Task GetEmailReward(PlayerSession playerSession,List<EmailData> emailData)
    {
        var userId=playerSession.UserId;
        var serverId=playerSession.ServerId;
        if (userId==null|| serverId==null)return;
        emailData = await GameDataMrg.ClaimEmailRewards(
            userId,
            serverId,
            emailData.Select(data => data.Id));
        if (emailData.Count == 0)
        {
            return;
        }

        List<EquipData> equipDatas = new List<EquipData>();
        Dictionary<string, long> itemDic = new Dictionary<string, long>();
        try
        {
            for (int i = 0; i < emailData.Count; i++)
            {
                if (emailData[i].Equips!=null)
                {
                    equipDatas.AddRange(emailData[i].Equips);
                }

                if (emailData[i].ItemDic!=null)
                {
                    foreach (var item in emailData[i].ItemDic)
                    {
                        if (itemDic.ContainsKey(item.Key))
                        {
                            itemDic[item.Key] += item.Value;
                        }
                        else
                        {
                            itemDic[item.Key] = item.Value;
                        }
                    }
                }
            }

            if (equipDatas.Count > 0 || itemDic.Count > 0)
            {
                await playerSession.MutateAndSaveUserDataSnapshotAsync(data =>
                {
                    if (equipDatas.Count > 0)
                        data.EquipData.AddRange(equipDatas);
                    foreach (var item in itemDic)
                    {
                        string key = item.Key.StartsWith("Item.", StringComparison.Ordinal)
                            ? item.Key[5..]
                            : item.Key;
                        data.Item[key] = data.Item.GetValueOrDefault(key, 0) + item.Value;
                    }
                });
            }

            await GameDataMrg.UpdateEmailStates(emailData.Select(data => data.Id).ToArray(), 1);
            await GameDataMrg.CompleteGlobalEmailClaims(
                userId, serverId, emailData.Select(data => data.Id), true);
            if (RedisMrg.IsConnected)
            {
                await GameDataMrg.RefreshEmailCache(userId, serverId);
            }
        }
        catch
        {
            await GameDataMrg.UpdateEmailStates(emailData.Select(data => data.Id).ToArray(), 0);
            await GameDataMrg.CompleteGlobalEmailClaims(
                userId, serverId, emailData.Select(data => data.Id), false);
            if (RedisMrg.IsConnected)
            {
                await GameDataMrg.RefreshEmailCache(userId, serverId);
            }
            throw;
        }
        
        
        
        
        var showRewardMsg=new Msg()
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.ShowRewardPanel,
        };
        
        
        foreach (var item in itemDic)
        {
            showRewardMsg.RewardItem[item.Key] = item.Value;
        }

        for (int i = 0; i < equipDatas.Count; i++)
        {
            showRewardMsg.RewardEquip.Add(equipDatas[i]);
        }
        
        await playerSession.SendBinaryAsync(showRewardMsg.ToByteArray());
        var emails = await GetEmailData(playerSession);
        var msg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.UpdateEmail,
            ServerId = serverId
        };
        msg.EmailList.AddRange(emails);
        await playerSession.SendBinaryAsync(msg.ToByteArray());
    }


    /// <summary>
    /// 删除已领取邮件
    /// </summary>
    /// <param name="playerSession"></param>
    private static async Task DeleteUseEmail(PlayerSession playerSession)
    {
        var userId=playerSession.UserId;
        var serverId=playerSession.ServerId;
        if (userId==null|| serverId==null)return;
        await GameDataMrg.DeleteUseEmail(userId,serverId);
        
        
        var emails = await GetEmailData(playerSession);
        var msg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.UpdateEmail,
            ServerId = serverId
        };
        msg.EmailList.AddRange(emails);
        await playerSession.SendBinaryAsync(msg.ToByteArray());
    }

    public static async Task GetEmailReward(PlayerSession playerSession,string[] ids)
    {
        var userId=playerSession.UserId;
        var serverId=playerSession.ServerId;
        if (userId==null|| serverId==null)return;
       var allEmail=await GetEmailData(playerSession);
       var list = allEmail.Where((data =>ids.Contains(data.Id) )).ToList();
       await GetEmailReward(playerSession, list);
    }

    /// <summary>
    /// 添加道具
    /// </summary>
    /// <param name="id"></param>
    /// <param name="serverId"></param>
    /// <param name="dic"></param>
    private static async Task<UserData> AddItems(PlayerSession player, Dictionary<string, long> dic)
    {
        return await player.MutateAndSaveUserDataSnapshotAsync(data =>
        {
            foreach (var item in dic)
            {
                string key = item.Key.StartsWith("Item.", StringComparison.Ordinal)
                    ? item.Key[5..]
                    : item.Key;
                data.Item[key] = data.Item.GetValueOrDefault(key, 0) + item.Value;
            }
        });
    }

    /// <summary>
    /// 添加字典数据
    /// </summary>
    /// <param name="id"></param>
    /// <param name="serverId"></param>
    /// <param name="dic"></param>
    private static async Task AddUserDataDic(Msg request, PlayerSession player)
    {
        string id = player.UserId ?? string.Empty;
        string serverId = player.ServerId ?? string.Empty;
        Dictionary<string, string> values = request.GetBusinessData();
        if (values.Count == 0)
            throw new ArgumentException("DataDic 数据不能为空。");
        if (values.Keys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("DataDic 的 key 不能为空。");

        UserData userData = await player.MutateAndSaveUserDataSnapshotAsync(data =>
        {
            foreach (var pair in values)
                data.DataDic[pair.Key] = pair.Value;
        });

        var response = new Msg
        {
            ServerMsgType = ServerMsgType.UpdateUserData,
            UserData = userData
        };
        if (request.IsRequest())
            await player.ReplyAsync(request, response);
        else
            await player.SendBinaryAsync(response.ToByteArray());

        // UserData is embedded in NetworkRoomMemberData. Rebuild and broadcast the
        // room snapshot so other clients can refresh this player's equipment/skin.
        uint roomId = player.NetworkRoomId;
        string? roomServerId = roomId == NetworkRoomManager.LobbyRoomId ? player.ServerId : null;
        if (roomId != 0 && NetworkRoomManager.Instance.TryGetRoomData(
                roomId, roomServerId, out NetworkRoomData room))
        {
            await BroadcastRoomState(room, roomServerId);
        }
    }

    /// <summary>
    /// 添加装备
    /// </summary>
    /// <param name="id"></param>
    /// <param name="serverId"></param>
    /// <param name="datas"></param>
    private static async Task<UserData> AddEquips(PlayerSession player, List<EquipData> datas)
    {
        return await player.MutateAndSaveUserDataSnapshotAsync(
            data => data.EquipData.AddRange(datas));
    }

    private static async Task AddRequestedItems(
        PlayerSession player, MapField<string, long> requestedItems, bool showRewardPanel)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId) ||
            requestedItems == null || requestedItems.Count == 0)
            return;

        Dictionary<string, long> validItems = requestedItems
            .Where(item => item.Value != 0 && item.Key.StartsWith("Item.", StringComparison.Ordinal) &&
                           item.Key.Length > 5 && item.Key.Length <= 128 && item.Key.IndexOf('$') < 0)
            .Take(100)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        if (validItems.Count == 0)
            return;

        await AddItems(player, validItems);
        await SendCurrentUserData(player);

        if (!showRewardPanel)
            return;

        var rewardMsg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.ShowRewardPanel
        };
        foreach (KeyValuePair<string, long> item in validItems)
        {
            if (item.Value <= 0)
                continue;

            string key = item.Key.StartsWith("Item.", StringComparison.Ordinal)
                ? item.Key[5..]
                : item.Key;
            rewardMsg.RewardItem[key] = item.Value;
        }

        if (rewardMsg.RewardItem.Count == 0)
            return;

        await player.SendBinaryAsync(rewardMsg.ToByteArray());
    }

    private static bool ShouldShowRewardPanel(Msg request)
    {
        return !request.DataDic.TryGetValue(ShowRewardPanelKey, out string value) ||
               !bool.TryParse(value, out bool showRewardPanel) || showRewardPanel;
    }

    private static async Task AddRequestedEquips(
        PlayerSession player, RepeatedField<EquipData> requestedEquips, bool showRewardPanel)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId) ||
            requestedEquips == null || requestedEquips.Count == 0)
            return;

        var validEquips = new List<EquipData>();
        foreach (EquipData requested in requestedEquips.Take(20))
        {
            if (requested == null || requested.Data == null || requested.Data.Length == 0 ||
                requested.Data.Length > 64 * 1024)
                continue;

            EquipData equip = requested.Clone();
            equip.Id = Guid.NewGuid().ToString("N");
            validEquips.Add(equip);
        }
        if (validEquips.Count == 0)
            return;

        await AddEquips(player, validEquips);
        await SendCurrentUserData(player);

        if (!showRewardPanel)
            return;

        var rewardMsg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.ShowRewardPanel
        };
        rewardMsg.RewardEquip.AddRange(validEquips);
        await player.SendBinaryAsync(rewardMsg.ToByteArray());
    }

    private static async Task SendCurrentUserData(PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
            return;

        UserData? userData = player.UserDataSnapshot?.Clone();
        if (userData == null)
            return;
        await player.SendBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.UpdateUserData,
            UserData = userData
        }.ToByteArray());
    }
    

    /// <summary>
    /// 获取公告
    /// </summary>
    /// <param name="playerSession"></param>
    private static async Task GetGongGao(Msg request, PlayerSession playerSession)
    {
        var msg = new Msg()
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.GetGongGaoSuc,
            GongGaoData = new GongGaoData() { Info = await GameDataMrg.GetGongGao() }
        };
        await playerSession.ReplyAsync(request, msg);
    }
    
    
    private static async Task GetServerList(Msg request, PlayerSession player)
    {
        var data =await GameDataMrg.GetServers();
        var msg = new Msg()
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.GetServerListSuc,
            ServerData = data
        };
        await player.ReplyAsync(request, msg);
    }
    
    private static async Task RelayNetworkTransform(NetworkTransformData? position, PlayerSession player)
    {
        if (position == null || player.NetworkObjectId == 0 || player.NetworkRoomId == 0 || string.IsNullOrWhiteSpace(player.UserId))
            return;

        if (!float.IsFinite(position.PositionX) || !float.IsFinite(position.PositionY) ||
            !float.IsFinite(position.PositionZ) || !float.IsFinite(position.RotationX) ||
            !float.IsFinite(position.RotationY) || !float.IsFinite(position.RotationZ) ||
            !float.IsFinite(position.RotationW))
            return;

        NetworkTransformData snapshot = position.Clone();
        if (snapshot.ObjectId == 0 || snapshot.ObjectId == player.NetworkObjectId)
            snapshot.ObjectId = player.NetworkObjectId;
        else
        {
            await NetworkRoomObjectManager.Instance.RelayTransformAsync(player, snapshot);
            return;
        }
        await PlayerSessionManager.Instance.RelayNetworkTransformAsync(player, snapshot);
    }

    private static async Task RelayNetworkAnimation(NetworkAnimationData? animation, PlayerSession player)
    {
        if (animation == null || player.NetworkObjectId == 0 || player.NetworkRoomId == 0 || string.IsNullOrWhiteSpace(player.UserId))
            return;

        NetworkAnimationData snapshot = animation.Clone();
        if (snapshot.ObjectId == 0 || snapshot.ObjectId == player.NetworkObjectId)
            snapshot.ObjectId = player.NetworkObjectId;
        else
        {
            await NetworkRoomObjectManager.Instance.RelayAnimationAsync(player, snapshot);
            return;
        }
        await PlayerSessionManager.Instance.RelayNetworkAnimationAsync(player, snapshot);
    }

    private static async Task CreateRoom(NetworkRoomData? roomData, PlayerSession player)
    {
        long startedAt = Environment.TickCount64;
        await LeaveCurrentRoomAndBroadcast(player);
        if (NetworkRoomManager.Instance.TryCreate(player, roomData, out NetworkRoomData room, out string error))
        {
            await SendRoomMessage(player, ServerMsgType.NetworkRoomLoad, room);
            Console.WriteLine($"[RoomPerf] CreateRoom load queued: {Environment.TickCount64 - startedAt}ms, room={room.RoomId}");
        }
        else
        {
            await JoinLobbyAndNotify(player);
            await SendRoomError(player, error);
        }
    }

    private static async Task MatchRoom(NetworkRoomData? roomData, PlayerSession player)
    {
        await LeaveCurrentRoomAndBroadcast(player);
        if (NetworkRoomManager.Instance.TryMatch(player, roomData, out NetworkRoomData room, out string error))
            await SendRoomMessage(player, ServerMsgType.NetworkRoomLoad, room);
        else
        {
            await JoinLobbyAndNotify(player);
            await SendRoomError(player, error);
        }
    }

    private static async Task CompleteRoomJoin(uint roomId, PlayerSession player)
    {
        long startedAt = Environment.TickCount64;
        if (!NetworkRoomManager.Instance.TryCompleteJoin(
                player, roomId, out NetworkRoomData room, out bool allPlayersEntered, out string error))
        {
            await SendRoomError(player, error);
            return;
        }

        await SendRoomMessage(player, ServerMsgType.NetworkRoomEntered, room);
        // UI/房间成员刷新是进入房间的关键路径，先于对象快照和生成消息送达。
        await BroadcastRoomState(room);
        Console.WriteLine($"[RoomPerf] Room entered/state queued: {Environment.TickCount64 - startedAt}ms, room={room.RoomId}, members={room.Members.Count}");
        await RoomSharedValueManager.Instance.SendSnapshotAsync(player);
        await PlayerSessionManager.Instance.AnnounceNetworkObjectAsync(player);
        if (allPlayersEntered)
        {
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkRoomAllPlayersEntered,
                NetworkRoom = room
            }.ToByteArray(), null, room.RoomId);
        }
    }

    /// <summary>由房主主动把当前房间的全部在线成员切换到指定地图。</summary>
    private static async Task ChangeRoomMap(string? mapName, PlayerSession player)
    {
        if (!NetworkRoomManager.Instance.TryBeginMapChange(
                player, mapName, out NetworkRoomData room, out PlayerSession[] members, out string error))
        {
            await SendRoomError(player, error);
            return;
        }

        // 清掉旧地图生成的房间对象；玩家对象会在各客户端完成新场景加载后重新广播。
        // 全员收到 Load 后统一清理旧场景，无需为每个旧对象逐条发送 Despawn。
        await NetworkRoomObjectManager.Instance.DestroyRoomObjectsAsync(room.RoomId, notifyClients: false);

        // WebNetworkTransform 在新场景会从序列号 1 重新开始。
        // 如果保留旧地图的高序列号快照，新客户端会先应用旧快照，并把新地图的首批
        // 位置包误判为过期数据，直到序列号追上旧值后才开始同步。
        foreach (PlayerSession member in members)
        {
            member.LastNetworkTransform = null;
            member.LastNetworkAnimation = null;
        }

        byte[] loadMessage = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomLoad,
            NetworkRoom = room
        }.ToByteArray();
        await Task.WhenAll(members.Select(member => member.SendBinaryAsync(loadMessage)));
    }

    /// <summary>
    /// 离开当前游戏房间并切回长期大厅。大厅成员关系立即生效，客户端加载完成后会派发进入回调。
    /// </summary>
    /// <param name="player">主动离开房间的玩家会话。</param>
    private static async Task LeaveRoom(PlayerSession player)
    {
        uint oldRoomId = player.NetworkRoomId;
        // 创建/加入房间不能被大厅中最慢客户端的销毁通知阻塞。
        await PlayerSessionManager.Instance.LeaveNetworkRoomAsync(player, waitForNotifications: false);
        if (NetworkRoomManager.Instance.TryGetRoomData(oldRoomId, out NetworkRoomData remainingRoom) &&
            oldRoomId != NetworkRoomManager.LobbyRoomId)
            await BroadcastRoomState(remainingRoom);
        NetworkRoomData lobby = NetworkRoomManager.Instance.JoinLobbyNow(player);
        await SendRoomMessage(player, ServerMsgType.NetworkRoomLoad, lobby);
        await NotifyLobbyJoined(player, lobby);
    }

    private static async Task SetRoomReady(bool ready, PlayerSession player)
    {
        if (NetworkRoomManager.Instance.TrySetReady(player, ready, out NetworkRoomData room, out string error))
            await BroadcastRoomState(room);
        else await SendRoomError(player, error);
    }

    private static async Task UpdateRoom(NetworkRoomData? roomData, PlayerSession player)
    {
        if (NetworkRoomManager.Instance.TryUpdateRoom(player, roomData, out NetworkRoomData room, out string error))
            await BroadcastRoomState(room, player.ServerId);
        else
            await SendRoomError(player, error);
    }

    private static async Task StartRoomGame(PlayerSession player)
    {
        if (NetworkRoomManager.Instance.TryStart(player, out NetworkRoomData room, out string error))
        {
            // 每次开始新游戏都清除上一局的房间共享值；房主中途转移不会经过这里。
            await RoomSharedValueManager.Instance.ResetAllAsync(player);
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkRoomGameStarted, NetworkRoom = room }.ToByteArray(), null, room.RoomId);
        }
        else await SendRoomError(player, error);
    }

    private static async Task KickRoomPlayer(string targetUserId, PlayerSession host)
    {
        PlayerSession? target = PlayerSessionManager.Instance.GetUserSession(targetUserId, host.ServerId ?? ServerScope.DefaultServerId);
        if (target == null) { await SendRoomError(host, "目标玩家不在线。"); return; }
        if (!NetworkRoomManager.Instance.TryKick(host, target, out uint oldRoomId, out NetworkRoomData room, out string error))
        { await SendRoomError(host, error); return; }

        await NetworkRoomObjectManager.Instance.HandleOwnerLeavingAsync(target, oldRoomId);
        await PlayerSessionManager.Instance.DespawnFromRoomAsync(target, oldRoomId);
        NetworkRoomManager.Instance.Leave(target, out NetworkRoomData? mapTransitionCompletedRoom);
        NetworkRoomData lobby = NetworkRoomManager.Instance.JoinLobbyNow(target);
        await SendRoomMessage(target, ServerMsgType.NetworkRoomKicked, lobby);
        await NotifyLobbyJoined(target, lobby);
        await BroadcastRoomState(room);
        if (mapTransitionCompletedRoom != null)
        {
            await PlayerSessionManager.Instance.BroadcastBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkRoomAllPlayersEntered,
                NetworkRoom = mapTransitionCompletedRoom
            }.ToByteArray(), null, mapTransitionCompletedRoom.RoomId);
        }
    }

    private static async Task InviteRoomPlayer(string targetUserId, PlayerSession inviter)
    {
        PlayerSession? target = PlayerSessionManager.Instance.GetUserSession(targetUserId, inviter.ServerId ?? ServerScope.DefaultServerId);
        if (target == null) { await SendRoomError(inviter, "好友不在线。"); return; }
        if (target.NetworkRoomId != NetworkRoomManager.LobbyRoomId)
        { await SendRoomError(inviter, "好友当前不在大厅。" ); return; }
        if (!NetworkRoomManager.Instance.CanInvite(inviter, inviter.NetworkRoomId, out NetworkRoomData room, out string error))
        { await SendRoomError(inviter, error); return; }
        target.NetworkRoomInvites[room.RoomId] = 0;
        await target.SendBinaryAsync(new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomInviteReceived,
            NetworkRoomInvite = new NetworkRoomInviteData { RoomId = room.RoomId,
                InviterPlayerId = inviter.UserId ?? string.Empty, RoomName = room.RoomName, MapName = room.MapName } }.ToByteArray());
    }

    private static async Task AcceptRoomInvite(uint roomId, PlayerSession player)
    {
        if (roomId == 0 || !player.NetworkRoomInvites.TryRemove(roomId, out _))
        {
            await SendRoomError(player, "房间邀请不存在或已经失效。");
            return;
        }
        await LeaveCurrentRoomAndBroadcast(player);
        if (NetworkRoomManager.Instance.TryReserveInvite(player, roomId, out NetworkRoomData room, out string error))
            await SendRoomMessage(player, ServerMsgType.NetworkRoomLoad, room);
        else
        {
            await JoinLobbyAndNotify(player);
            await SendRoomError(player, error);
        }
    }

    private static async Task JoinRoom(uint roomId, PlayerSession player)
    {
        if (player.NetworkRoomId != NetworkRoomManager.LobbyRoomId ||
            player.PendingNetworkRoomId != 0)
        {
            await SendRoomError(player, "玩家当前不在大厅，无法加入房间。");
            return;
        }

        await LeaveCurrentRoomAndBroadcast(player);
        if (NetworkRoomManager.Instance.TryReserveJoin(player, roomId, out NetworkRoomData room, out string error))
            await SendRoomMessage(player, ServerMsgType.NetworkRoomLoad, room);
        else
        {
            await JoinLobbyAndNotify(player);
            await SendRoomError(player, error);
        }
    }

    private static async Task SendRoomList(PlayerSession player)
    {
        NetworkRoomData[] rooms =
            NetworkRoomManager.Instance.GetJoinableRooms(player.ServerId);

        foreach (NetworkRoomData room in rooms)
        {
            await SendRoomMessage(player, ServerMsgType.NetworkRoomListItem, room);
        }

        await player.SendBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomListCompleted
        }.ToByteArray());
    }

    /// <summary>
    /// 转发 Opus 语音包。服务端不做音频解码，只校验包大小并执行房间、队伍和 AOI 过滤。
    /// </summary>
    private static async Task RelayNetworkVoice(string? base64Opus, PlayerSession player)
    {
        const float voiceRange = 25f;
        const int maxVoiceFrameBytes = 1275;

        if (player.NetworkRoomId <= NetworkRoomManager.LobbyRoomId ||
            player.NetworkObjectId == 0 ||
            string.IsNullOrWhiteSpace(base64Opus))
            return;

        byte[] opusPacket;
        try
        {
            opusPacket = Convert.FromBase64String(base64Opus);
        }
        catch (FormatException)
        {
            return;
        }

        if (opusPacket.Length == 0 || opusPacket.Length > maxVoiceFrameBytes)
            return;

        var sends = new List<Task>();
        foreach (PlayerSession receiver in
                 PlayerSessionManager.Instance.GetRoomSessions(player.NetworkRoomId))
        {
            if (receiver == player ||
                !NetworkRoomManager.Instance.CanReceiveVoice(
                    player, receiver, out bool useAoi))
                continue;

            // AOI 关闭时不依赖网络坐标，同房间中符合队伍规则的成员均可收听。
            if (useAoi)
            {
                if (player.LastNetworkTransform == null ||
                    receiver.LastNetworkTransform == null)
                    continue;

                NetworkTransformData senderPosition = player.LastNetworkTransform;
                NetworkTransformData receiverPosition = receiver.LastNetworkTransform;
                float dx = senderPosition.PositionX - receiverPosition.PositionX;
                float dy = senderPosition.PositionY - receiverPosition.PositionY;
                float dz = senderPosition.PositionZ - receiverPosition.PositionZ;
                if (dx * dx + dy * dy + dz * dz > voiceRange * voiceRange)
                    continue;
            }

            byte[] packet = new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.NetworkVoiceFrame,
                NetworkObjectId = player.NetworkObjectId,
                Id = base64Opus,
                NetworkRoomRequest = new NetworkRoomRequest { Ready = useAoi }
            }.ToByteArray();
            sends.Add(receiver.SendBinaryAsync(packet));
        }

        await Task.WhenAll(sends);
    }

    /// <summary>处理房主发来的房间语音路由设置。</summary>
    private static async Task SetVoiceOptions(
        bool aoiEnabled,
        bool teamOnly,
        PlayerSession player)
    {
        if (!NetworkRoomManager.Instance.TrySetVoiceOptions(
                player, aoiEnabled, teamOnly, out string error))
            await SendRoomError(player, error);
    }

    /// <summary>处理房主为指定玩家分配语音队伍的请求。</summary>
    private static async Task SetVoiceTeam(
        string targetUserId,
        string teamId,
        PlayerSession host)
    {
        PlayerSession? target = PlayerSessionManager.Instance.GetUserSession(
            targetUserId,
            host.ServerId ?? ServerScope.DefaultServerId);
        if (target == null)
        {
            await SendRoomError(host, "目标玩家不在线。");
            return;
        }

        if (!NetworkRoomManager.Instance.TrySetVoiceTeam(
                host, target, teamId, out string error))
            await SendRoomError(host, error);
    }

    private static async Task LeaveCurrentRoomAndBroadcast(PlayerSession player)
    {
        uint oldRoomId = player.NetworkRoomId;
        string? serverId = player.ServerId;

        await PlayerSessionManager.Instance.LeaveNetworkRoomAsync(player, waitForNotifications: false);

        if (oldRoomId == 0 ||
            !NetworkRoomManager.Instance.TryGetRoomData(
                oldRoomId,
                oldRoomId == NetworkRoomManager.LobbyRoomId ? serverId : null,
                out NetworkRoomData remainingRoom))
            return;

        QueueLatestRoomState(
            remainingRoom,
            oldRoomId == NetworkRoomManager.LobbyRoomId ? serverId : null);
    }

    private static void QueueLatestRoomState(NetworkRoomData room, string? serverId)
    {
        byte[] data = new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomState, NetworkRoom = room }.ToByteArray();
        ulong key = ((ulong)(uint)ServerMsgType.NetworkRoomState << 32) | room.RoomId;
        PlayerSessionManager.Instance.BroadcastLatestControlBinary(
            key, data, null, room.RoomId, serverId);
    }

    /// <summary>
    /// 广播最新房间成员状态。
    /// 大厅需要传入 serverId，避免把某个逻辑区服的可见成员列表发送给其他区服。
    /// </summary>
    private static Task BroadcastRoomState(NetworkRoomData room, string? serverId = null)
    {
        byte[] data = new Msg { MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomState, NetworkRoom = room }.ToByteArray();
        // 单路可靠入队，避免重复刷新和跨场景状态乱序。
        return PlayerSessionManager.Instance.BroadcastBinaryAsync(data,
            null, room.RoomId, serverId);
    }

    private static Task SendRoomMessage(PlayerSession player, ServerMsgType type, NetworkRoomData room)
    {
        return player.SendBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = type,
            NetworkRoom = room
        }.ToByteArray());
    }

    /// <summary>
    /// 立即把玩家加入长期大厅，并完成所有客户端通知。
    /// 任何需要直接调用 JoinLobbyNow 的业务都应改走此方法，避免只改了服务端状态却没有进入回调。
    /// </summary>
    private static async Task<NetworkRoomData> JoinLobbyAndNotify(PlayerSession player)
    {
        NetworkRoomData lobbyRoom = NetworkRoomManager.Instance.JoinLobbyNow(player);
        await NotifyLobbyJoined(player, lobbyRoom);
        return lobbyRoom;
    }

    /// <summary>通知加入者和大厅内其他玩家，并创建该玩家的大厅网络表现。</summary>
    private static async Task NotifyLobbyJoined(PlayerSession player, NetworkRoomData lobbyRoom)
    {
        await SendRoomMessage(player, ServerMsgType.NetworkRoomEntered, lobbyRoom);
        await RoomSharedValueManager.Instance.SendSnapshotAsync(player);
        await BroadcastRoomState(lobbyRoom, player.ServerId);
        await PlayerSessionManager.Instance.AnnounceNetworkObjectAsync(player);
    }

    private static Task SendRoomError(PlayerSession player, string error)
    {
        return player.SendBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.NetworkRoomError,
            TipsSrt = error ?? "房间操作失败。"
        }.ToByteArray());
    }

    /// <summary>
    /// 设置职业
    /// </summary>
    /// <param name="id"></param>
    /// <param name="serverId"></param>
    /// <param name="roleType"></param>
    /// <param name="player"></param>
    private static async Task SetRoleType(string id,string serverId,int roleType,PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
        {
            return;
        }

        if (!string.Equals(id, player.UserId, StringComparison.Ordinal))
        {
            return;
        }

        var userData = await player.MutateAndSaveUserDataSnapshotAsync(
            data => data.RoleType = roleType,
            updateRank: true);
        var msg = new Msg
            { MsgType = ProtobufMsgType.Server, ServerMsgType = ServerMsgType.UpdateUserData, UserData = userData };
        await player.SendBinaryAsync(msg.ToByteArray());
    }

    /// <summary>校验并更新当前登录玩家的头像，然后刷新房间成员状态。</summary>
    /// <param name="request">包含头像链接和可选请求 ID 的客户端消息。</param>
    /// <param name="player">发起操作的玩家会话。</param>
    private static async Task SetAvatar(Msg request, PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
        {
            await player.ReplyErrorAsync(request, "NOT_LOGGED_IN", "请先登录后再修改头像。");
            return;
        }

        string avatar = request.UserData?.Avatar?.Trim() ?? string.Empty;
        if (avatar.Length > MaxAvatarUrlLength)
        {
            await player.ReplyErrorAsync(request, "AVATAR_URL_TOO_LONG", "头像链接不能超过 2048 个字符。");
            return;
        }
        if (avatar.Length > 0 &&
            (!Uri.TryCreate(avatar, UriKind.Absolute, out Uri? avatarUri) ||
             (avatarUri.Scheme != Uri.UriSchemeHttp && avatarUri.Scheme != Uri.UriSchemeHttps)))
        {
            await player.ReplyErrorAsync(request, "INVALID_AVATAR_URL", "头像必须是有效的 HTTP 或 HTTPS 链接。");
            return;
        }

        UserData? userData = await player.MutateAndSaveUserDataSnapshotAsync(
            data => data.Avatar = avatar);
        if (userData == null)
        {
            await player.ReplyErrorAsync(request, "USER_NOT_FOUND", "没有找到当前玩家数据。");
            return;
        }

        await player.ReplyAsync(request, new Msg
        {
            ServerMsgType = ServerMsgType.UpdateUserData,
            UserData = userData
        });

        uint roomId = player.NetworkRoomId;
        string? roomServerId = roomId == NetworkRoomManager.LobbyRoomId ? player.ServerId : null;
        if (roomId != 0 && NetworkRoomManager.Instance.TryGetRoomData(
                roomId, roomServerId, out NetworkRoomData room))
        {
            await BroadcastRoomState(room, roomServerId);
        }
    }

    /// <summary>处理添加好友请求，并向在线的双方推送最新好友列表。</summary>
    /// <param name="request">通过 <see cref="Msg.Id"/> 携带目标玩家 ID 的请求。</param>
    /// <param name="player">发起添加的玩家会话。</param>
    private static async Task AddFriend(Msg request, PlayerSession player)
    {
        if (!TryGetFriendRequestContext(request, player, out string userId,
                out string serverId, out string friendUserId, out string errorCode, out string error))
        {
            await player.ReplyErrorAsync(request, errorCode, error);
            return;
        }

        AddFriendResult result = await GameDataMrg.AddFriend(userId, friendUserId, serverId);
        if (result != AddFriendResult.Success)
        {
            (errorCode, error) = result switch
            {
                AddFriendResult.InvalidTarget => ("INVALID_FRIEND_ID", "不能添加自己为好友。"),
                AddFriendResult.TargetNotFound => ("FRIEND_NOT_FOUND", "没有找到该玩家。"),
                AddFriendResult.FriendLimitReached => ("FRIEND_LIMIT_REACHED", "好友数量已达到 200 人上限。"),
                _ => ("ADD_FRIEND_FAILED", "添加好友失败，请稍后重试。")
            };
            await player.ReplyErrorAsync(request, errorCode, error);
            return;
        }

        await ReplyFriendList(request, player, userId, serverId);
        await NotifyFriendList(friendUserId, serverId);
    }

    /// <summary>处理删除好友请求，并向在线的双方推送最新好友列表。</summary>
    /// <param name="request">通过 <see cref="Msg.Id"/> 携带目标玩家 ID 的请求。</param>
    /// <param name="player">发起删除的玩家会话。</param>
    private static async Task DeleteFriend(Msg request, PlayerSession player)
    {
        if (!TryGetFriendRequestContext(request, player, out string userId,
                out string serverId, out string friendUserId, out string errorCode, out string error))
        {
            await player.ReplyErrorAsync(request, errorCode, error);
            return;
        }

        await GameDataMrg.DeleteFriend(userId, friendUserId, serverId);
        await ReplyFriendList(request, player, userId, serverId);
        await NotifyFriendList(friendUserId, serverId);
    }

    /// <summary>获取当前登录玩家的好友列表并作为请求响应返回。</summary>
    /// <param name="request">用于关联响应的客户端请求。</param>
    /// <param name="player">需要获取好友列表的玩家会话。</param>
    private static async Task GetFriendList(Msg request, PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
        {
            await player.ReplyErrorAsync(request, "NOT_LOGGED_IN", "请先登录后再获取好友列表。");
            return;
        }
        await ReplyFriendList(request, player, player.UserId, player.ServerId);
    }

    /// <summary>
    /// 校验好友关系与在线状态，将私聊实时转发给接收方，并向发送方返回送达确认。
    /// </summary>
    /// <param name="request">包含接收者 ID、消息正文和可选请求 ID 的客户端消息。</param>
    /// <param name="player">发送消息的玩家会话。</param>
    private static async Task SendChannelChatMessage(Msg request, PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
        {
            await player.ReplyErrorAsync(request, "NOT_LOGGED_IN", "请先登录后再发送聊天消息。");
            return;
        }

        ChannelChatMessageData? input = request.ChannelChatMessage;
        string content = input?.Content?.Trim() ?? string.Empty;
        if (content.Length == 0 || content.Length > MaxChannelChatMessageLength)
        {
            await player.ReplyErrorAsync(request, "INVALID_CHAT_CONTENT", "聊天内容必须为 1 到 200 个字符。");
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - player.LastChannelChatAt < ChannelChatInterval)
        {
            await player.ReplyErrorAsync(request, "CHAT_TOO_FAST", "发送过于频繁，请稍后再试。");
            return;
        }

        if (ChatContentFilter.TryFindForbiddenWord(content, out _))
        {
            await player.ReplyErrorAsync(request, "FORBIDDEN_CHAT_CONTENT", "聊天内容包含违禁词，消息未发送。");
            return;
        }

        ChatChannelType channel = input?.Channel ?? ChatChannelType.Server;
        uint roomId = 0;
        if (channel == ChatChannelType.Room)
        {
            roomId = player.NetworkRoomId;
            if (roomId == 0 || roomId == NetworkRoomManager.LobbyRoomId)
            {
                await player.ReplyErrorAsync(request, "NOT_IN_GAME_ROOM", "进入游戏房间后才能使用房间聊天。");
                return;
            }
        }
        else if (channel != ChatChannelType.Server)
        {
            await player.ReplyErrorAsync(request, "INVALID_CHAT_CHANNEL", "聊天频道无效。");
            return;
        }

        player.LastChannelChatAt = now;
        var chatMessage = new ChannelChatMessageData
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Channel = channel,
            SenderUserId = player.UserId,
            SenderName = player.UserDataSnapshot?.Name ?? player.UserId,
            Content = content,
            SentAt = now.ToUnixTimeMilliseconds(),
            ServerId = player.ServerId,
            RoomId = roomId
        };
        if (player.UserDataSnapshot != null)
            chatMessage.SenderUserData = player.UserDataSnapshot.Clone();

        byte[] push = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.ChannelChatMessageReceived,
            ChannelChatMessage = chatMessage
        }.ToByteArray();
        await PlayerSessionManager.Instance.BroadcastBinaryAsync(
            push, null, roomId, player.ServerId);

        // Sent 仅用于完成发送者的 RequestAsync；聊天展示统一走上面的 Received 广播。
        await player.ReplyAsync(request, new Msg
        {
            ServerMsgType = ServerMsgType.ChannelChatMessageSent,
            ChannelChatMessage = chatMessage
        });
    }

    private static async Task SendFriendMessage(Msg request, PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
        {
            await player.ReplyErrorAsync(request, "NOT_LOGGED_IN", "请先登录后再发送好友消息。");
            return;
        }

        string receiverUserId = request.FriendMessage?.ReceiverUserId?.Trim() ?? string.Empty;
        string content = request.FriendMessage?.Content?.Trim() ?? string.Empty;
        if (receiverUserId.Length == 0 || receiverUserId.Length > MaxUserIdLength ||
            string.Equals(receiverUserId, player.UserId, StringComparison.Ordinal))
        {
            await player.ReplyErrorAsync(request, "INVALID_FRIEND_ID", "消息接收者无效。");
            return;
        }
        if (content.Length == 0 || content.Length > MaxFriendMessageLength)
        {
            await player.ReplyErrorAsync(request, "INVALID_MESSAGE_CONTENT", "消息内容不能为空且不能超过 500 个字符。");
            return;
        }

        if (!await GameDataMrg.AreFriends(player.UserId, receiverUserId, player.ServerId))
        {
            await player.ReplyErrorAsync(request, "NOT_FRIENDS", "只能给好友发送消息。");
            return;
        }

        PlayerSession? receiver = PlayerSessionManager.Instance.GetUserSession(
            receiverUserId, player.ServerId);
        if (receiver == null || !receiver.IsConnected)
        {
            await player.ReplyErrorAsync(request, "FRIEND_OFFLINE", "好友当前不在线。");
            return;
        }

        var friendMessage = new FriendMessageData
        {
            MessageId = Guid.NewGuid().ToString("N"),
            SenderUserId = player.UserId,
            ReceiverUserId = receiverUserId,
            Content = content,
            SentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        if (receiver.UserDataSnapshot != null)
            friendMessage.FriendUserData = receiver.UserDataSnapshot.Clone();

        FriendMessageData receiverMessage = friendMessage.Clone();
        if (player.UserDataSnapshot != null)
            receiverMessage.FriendUserData = player.UserDataSnapshot.Clone();

        byte[] senderReceivedMessage = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.FriendMessageReceived,
            FriendMessage = friendMessage
        }.ToByteArray();
        byte[] receiverReceivedMessage = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.FriendMessageReceived,
            FriendMessage = receiverMessage
        }.ToByteArray();

        // 发送者和接收者的聊天展示统一通过 Received；Sent 仅完成 RequestAsync。
        await Task.WhenAll(
            receiver.SendBinaryAsync(receiverReceivedMessage),
            player.SendBinaryAsync(senderReceivedMessage));

        await player.ReplyAsync(request, new Msg
        {
            ServerMsgType = ServerMsgType.FriendMessageSent,
            FriendMessage = friendMessage
        });
    }

    /// <summary>从当前会话和请求中提取好友操作上下文并执行公共参数校验。</summary>
    /// <param name="request">添加或删除好友请求。</param>
    /// <param name="player">发起操作的玩家会话。</param>
    /// <param name="userId">校验成功后返回当前登录玩家 ID。</param>
    /// <param name="serverId">校验成功后返回当前逻辑区服 ID。</param>
    /// <param name="friendUserId">校验成功后返回目标好友玩家 ID。</param>
    /// <param name="errorCode">校验失败时返回稳定的客户端错误码。</param>
    /// <param name="error">校验失败时返回可显示的错误信息。</param>
    /// <returns>上下文合法时为 <see langword="true"/>。</returns>
    private static bool TryGetFriendRequestContext(
        Msg request,
        PlayerSession player,
        out string userId,
        out string serverId,
        out string friendUserId,
        out string errorCode,
        out string error)
    {
        userId = player.UserId ?? string.Empty;
        serverId = player.ServerId ?? string.Empty;
        friendUserId = request.Id?.Trim() ?? string.Empty;
        if (userId.Length == 0 || serverId.Length == 0)
        {
            errorCode = "NOT_LOGGED_IN";
            error = "请先登录后再操作好友。";
            return false;
        }
        if (friendUserId.Length == 0 || friendUserId.Length > MaxUserIdLength)
        {
            errorCode = "INVALID_FRIEND_ID";
            error = "好友玩家 ID 无效。";
            return false;
        }
        if (string.Equals(userId, friendUserId, StringComparison.Ordinal))
        {
            errorCode = "INVALID_FRIEND_ID";
            error = "不能对自己执行好友操作。";
            return false;
        }

        errorCode = string.Empty;
        error = string.Empty;
        return true;
    }

    /// <summary>查询好友列表，并保留请求 ID 作为当前操作的直接响应。</summary>
    /// <param name="request">需要响应的客户端请求。</param>
    /// <param name="player">接收响应的玩家会话。</param>
    /// <param name="userId">需要查询好友列表的玩家 ID。</param>
    /// <param name="serverId">玩家所在的逻辑区服 ID。</param>
    private static async Task ReplyFriendList(
        Msg request, PlayerSession player, string userId, string serverId)
    {
        FriendListData friendList = await GameDataMrg.GetFriendList(userId, serverId);
        await player.ReplyAsync(request, new Msg
        {
            ServerMsgType = ServerMsgType.UpdateFriendList,
            FriendList = friendList
        });
    }

    /// <summary>如果指定玩家在线，则主动向其推送最新好友列表。</summary>
    /// <param name="userId">需要接收更新的玩家 ID。</param>
    /// <param name="serverId">玩家所在的逻辑区服 ID。</param>
    private static async Task NotifyFriendList(string userId, string serverId)
    {
        PlayerSession? target = PlayerSessionManager.Instance.GetUserSession(userId, serverId);
        if (target == null)
            return;

        FriendListData friendList = await GameDataMrg.GetFriendList(userId, serverId);
        await target.SendBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.UpdateFriendList,
            FriendList = friendList
        }.ToByteArray());
    }
    
    /// <summary>
    /// 登录：校验玩家 id，读取或创建玩家数据，然后把登录成功消息发回客户端。
    /// </summary>
    private static async Task Login(Msg request, PlayerSession player)
    {
        string id = request.Id ?? string.Empty;
        string? sessionToken = null;
        if (request.DataDic.TryGetValue(LoginProviderKey, out string? provider) &&
            string.Equals(provider, "wechat", StringComparison.Ordinal))
        {
            bool resuming = request.DataDic.TryGetValue(WeChatLoginService.SessionTokenKey, out sessionToken);
            WeChatLoginResult weChatLogin = resuming
                ? WeChatLoginService.ValidateSessionToken(sessionToken!, id)
                : await WeChatLoginService.ExchangeCodeAsync(id);
            if (!weChatLogin.Success)
            {
                if (request.IsRequest())
                    await player.ReplyErrorAsync(request, weChatLogin.ErrorCode, weChatLogin.ErrorMessage);
                else
                {
                    var failure = new Msg
                    {
                        MsgType = ProtobufMsgType.Server,
                        ServerMsgType = ServerMsgType.Tips,
                        TipsSrt = weChatLogin.ErrorMessage
                    };
                    if (resuming) failure.DataDic[WeChatLoginService.SessionInvalidKey] = "1";
                    await player.SendBinaryAsync(failure.ToByteArray());
                }
                return;
            }

            id = weChatLogin.OpenId;
            if (!resuming) sessionToken = WeChatLoginService.CreateSessionToken(id);
        }
        else if (!WeChatLoginService.AllowInsecureDevelopmentLogin)
        {
            await player.ReplyErrorAsync(request, "AUTHENTICATION_REQUIRED", "此服务器要求微信登录，未启用测试账号入口。");
            return;
        }
        string serverId = request.ServerId;
        serverId = ServerScope.Normalize(serverId);
        using (var loginGate = await LoginGates.AcquireAsync(id))
        {
        if (!player.IsConnected || player.IsSuperseded) return;
        if (request.IsRequest() && (string.IsNullOrWhiteSpace(id) || id.Length > MaxUserIdLength))
        {
            await player.ReplyErrorAsync(request, "INVALID_PLAYER_ID", "玩家 ID 无效。");
            return;
        }
        if (request.IsRequest() && !string.IsNullOrWhiteSpace(player.UserId))
        {
            await player.ReplyErrorAsync(request, "ALREADY_LOGGED_IN",
                "当前连接已经登录，请先退出登录再切换账号或服务器。");
            return;
        }
        if (request.IsRequest() && !MongoDBMrg.IsConnected)
        {
            await player.ReplyErrorAsync(request, "DATABASE_UNAVAILABLE", "数据库暂时不可用。");
            return;
        }
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxUserIdLength)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(player.UserId))
        {
            await player.SendBinaryAsync(new Msg
            {
                MsgType = ProtobufMsgType.Server,
                ServerMsgType = ServerMsgType.Tips,
                TipsSrt = "当前连接已经登录，请先退出登录再切换账号或服务器。"
            }.ToByteArray());
            return;
        }

        if (!MongoDBMrg.IsConnected)
        {
            return;
        }

        var data = await GameDataMrg.GetUserAsCreate(id, serverId);
        if (!player.IsConnected || player.IsSuperseded) return;
        player.UpdateUserDataSnapshot(data);
        await player.BindUserAsync(id, serverId);
        uint networkObjectId = PlayerSessionManager.Instance.EnsureNetworkObjectId(player);
        NetworkRoomData lobbyRoom = NetworkRoomManager.Instance.JoinLobbyNow(player);
        var msg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.LoginSuc,
            ServerId = serverId,
            UserData = data,
            NetworkObjectId = networkObjectId,
            NetworkRoom = lobbyRoom
        };

        if (!string.IsNullOrEmpty(sessionToken))
            msg.DataDic[WeChatLoginService.SessionTokenKey] = sessionToken;
        await player.ReplyAsync(request, msg);
        // 登录需要先返回账户数据，再统一完成大厅进入通知。
        await NotifyLobbyJoined(player, lobbyRoom);
        }
    }

    /// <summary>
    /// 退出当前角色但不关闭 WebSocket，保存完成后客户端可以直接登录其他 serverId。
    /// </summary>
    private static async Task Logout(PlayerSession player)
    {
        var userId = player.UserId;
        var serverId = player.ServerId;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(serverId))
        {
            return;
        }

        await PlayerSessionManager.Instance.UnbindUserSessionAsync(player);
        player.ClearUserBinding();
        await GameDataMrg.SetPlayerOffline(userId, serverId);

        await player.SendBinaryAsync(new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.LogoutSuc,
            ServerId = serverId
        }.ToByteArray());
    }

    /// <summary>
    /// 获取当前已登录玩家的邮件，不接受客户端指定其他玩家 id。
    /// </summary>
    private static async Task GetEmail(PlayerSession player)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) ||
            string.IsNullOrWhiteSpace(player.ServerId))
        {
            Console.WriteLine($"[{player.PlayerId}] Ignore GetEmail before login binding completed.");
            return;
        }

        var serverId = player.ServerId;
        var emails = await GetEmailData(player);
        var msg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.UpdateEmail,
            ServerId = serverId
        };
        msg.EmailList.AddRange(emails);
        await player.SendBinaryAsync(msg.ToByteArray());
    }


    /// <summary>
    /// 获取玩家所有邮件
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public static async Task<List<EmailData>> GetEmailData(PlayerSession player)
    {
        string? userId = player.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<EmailData>();
        }

        if (!MongoDBMrg.IsConnected)
        {
            return new List<EmailData>();
        }

        if (string.IsNullOrWhiteSpace(player.ServerId))
        {
            return new List<EmailData>();
        }

        var serverId = player.ServerId;
        return await GameDataMrg.GetEmail(userId, serverId);
    }

    /// <summary>
    /// 获取战斗力排行榜，返回 RankData 列表。
    /// </summary>
    private static async Task GetRank(PlayerSession player,Msg data)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.ServerId))
        {
            return;
        }
        var ranks = await GameDataMrg.GetRankTopData(data.RankName, player.ServerId);
        var msg = new Msg
        {
            MsgType = ProtobufMsgType.Server,
            ServerMsgType = ServerMsgType.GetRankSuc,
            ServerId = player.ServerId
        };
        msg.RankData.AddRange(ranks);

        await player.SendBinaryAsync(msg.ToByteArray());
    }

    private static async Task UpdateRank(PlayerSession player, Msg data)
    {
        if (string.IsNullOrWhiteSpace(player.UserId) ||
            string.IsNullOrWhiteSpace(player.ServerId) ||
            data.UpdateRankData == null ||
            string.IsNullOrWhiteSpace(data.RankName))
        {
            return;
        }

        // 身份和玩家资料必须取自已认证的服务端会话，不能信任客户端上传值。
        var rankData = data.UpdateRankData.Clone();
        rankData.Id = player.UserId;
        rankData.UserId = player.UserId;
        rankData.ServerId = player.ServerId;

        UserData? userData = player.UserDataSnapshot?.Clone();
        if (userData == null)
            userData = await GameDataMrg.GetUser(player.UserId, player.ServerId);

        if (userData != null)
        {
            rankData.UserData = userData;
            rankData.RoleType = userData.RoleType;
        }

        bool saved = await GameDataMrg.UpdateRankData(data.RankName, player.ServerId, rankData);
        if (!saved)
        {
            Console.WriteLine(
                $"Rank update failed: user={player.UserId}, server={player.ServerId}, rank={data.RankName}.");
        }
    }
}
