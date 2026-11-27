using GameData;
using Google.Protobuf;
using StackExchange.Redis;

namespace WebSocketDemo;

public static class RedisMrg
{
    // 部署时默认连接同机 Redis；拆分部署可通过环境变量覆盖，无需重新编译。
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? "127.0.0.1:6379";
    private static readonly string Password =
        Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "zhang1320.";
    private static readonly TimeSpan DefaultCacheExpire = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan OnlineExpire = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockExpire = TimeSpan.FromSeconds(10);
    private const string DirtyUsersKey = "player:dirty:users";
    private const string RankNamesKey = "rank:names";
    private const string OnlineUsersKey = "player:online:users";

    private static ConnectionMultiplexer? _redis;
    private static IDatabase? _database;

    public static bool IsConnected => _redis?.IsConnected==true&&_database != null;
    
    /// <summary>
    /// 初始化 Redis 连接。连接失败不会抛到主流程，游戏服仍可继续使用 MongoDB。
    /// </summary>
    public static void Connect()
    {
        try
        {
            var options = ConfigurationOptions.Parse(ConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 3000;
            options.SyncTimeout = 3000;
            options.AsyncTimeout = 3000;
            options.KeepAlive = 30;
            options.ReconnectRetryPolicy = new ExponentialRetry(1000);
            options.Password = Password;
            _redis = ConnectionMultiplexer.Connect(options);
            _database = _redis.GetDatabase();
            Console.WriteLine("Redis connected.");
        }
        catch (Exception ex)
        {
            _redis = null;
            _database = null;
            Console.WriteLine($"Redis connect failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 写入字符串缓存，适合 token、状态值、简单计数结果。
    /// </summary>
    public static async Task<bool> SetString(string key, string value, TimeSpan? expire = null)
    {
        if (!IsConnected) return false;
        try
        {
            return await GetDatabase().StringSetAsync(key, value, expire ?? DefaultCacheExpire);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SetString failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 读取字符串缓存。不存在或 Redis 不可用时返回 null。
    /// </summary>
    public static async Task<string?> GetString(string key)
    {
        if (!IsConnected) return null;
        try
        {
            var value = await GetDatabase().StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetString failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 写入二进制缓存。protobuf 数据推荐使用这个方法，避免 JSON 序列化成本。
    /// </summary>
    public static async Task<bool> SetBytes(string key, byte[] value, TimeSpan? expire = null)
    {
        if (!IsConnected) return false;
        try
        {
            return await GetDatabase().StringSetAsync(key, value, expire ?? DefaultCacheExpire);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SetBytes failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 读取二进制缓存。
    /// </summary>
    public static async Task<byte[]?> GetBytes(string key)
    {
        if (!IsConnected) return null;
        try
        {
            var value = await GetDatabase().StringGetAsync(key);
            return value.HasValue ? (byte[]?)value : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetBytes failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 删除缓存 key。
    /// </summary>
    public static async Task<bool> Delete(string key)
    {
        if (!IsConnected) return false;
        try
        {
            return await GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis Delete failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取服务器列表
    /// </summary>
    /// <returns></returns>
    public static async Task<ServerDataList> GetServers()
    {
        var bytes=await GetBytes("ServerDataList");
        if (bytes == null) return new ServerDataList();
        return ServerDataList.Parser.ParseFrom(bytes);
    }
    
    /// <summary>
    /// 保存服务器列表
    /// </summary>
    /// <param name="serverDataList"></param>
    public static async Task SetServers(ServerDataList serverDataList)
    {
        await SetBytes("ServerDataList",serverDataList.ToByteArray());
    }
    
    /// <summary>
    /// 缓存玩家数据。UserData 是 protobuf 对象，直接用二进制保存。
    /// </summary>
    public static Task<bool> SetUserData(string userId, string serverId, UserData data, TimeSpan? expire = null)
    {
        serverId = ServerScope.Normalize(serverId);
        data.ServerId = serverId;
        return SetBytes(GetUserDataKey(serverId, userId), data.ToByteArray(), expire ?? DefaultCacheExpire);
    }

    /// <summary>
    /// 从 Redis 读取玩家数据。缓存不存在时返回 null，上层再回源 MongoDB。
    /// </summary>
    public static async Task<UserData?> GetUserData(string userId, string serverId)
    {
        var bytes = await GetBytes(GetUserDataKey(serverId, userId));
        if (bytes == null) return null;

        try
        {
            return UserData.Parser.ParseFrom(bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis parse user data failed: {ex.Message}");
            await Delete(GetUserDataKey(serverId, userId));
            return null;
        }
    }

    /// <summary>
    /// 单次管线批量写入玩家二进制快照并标记 dirty，省去串行多次网络往返。
    /// </summary>
    public static async Task<bool> SaveUserDataAndMarkDirty(string userId, string serverId, UserData data)
    {
        if (!IsConnected) return false;
        try
        {
            serverId = ServerScope.Normalize(serverId);
            data.ServerId = serverId;
            byte[] payload = data.ToByteArray();
            var database = GetDatabase();
            var batch = database.CreateBatch();
            string userKey = ServerScope.UserKey(serverId, userId);

            Task<bool> saveTask = batch.StringSetAsync(GetUserDataKey(serverId, userId), payload, TimeSpan.FromHours(3));
            Task<bool> markTask = batch.StringSetAsync(GetUserDirtyKey(serverId, userId), "1", TimeSpan.FromHours(2));
            Task<bool> setTask = batch.SetAddAsync(DirtyUsersKey, userKey);
            batch.Execute();

            await Task.WhenAll(saveTask, markTask, setTask);
            return saveTask.Result && markTask.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SaveUserDataAndMarkDirty failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 标记玩家数据已变更，后续可以定时把 dirty 玩家批量写回 MongoDB。
    /// </summary>
    public static async Task<bool> MarkUserDirty(string userId, string serverId)
    {
        if (!IsConnected) return false;
        try
        {
            var database = GetDatabase();
            var userKey = ServerScope.UserKey(serverId, userId);
            var markTask = database.StringSetAsync(GetUserDirtyKey(serverId, userId), "1", TimeSpan.FromHours(2));
            // 同步延长玩家数据缓存 TTL，防止 30 分钟缓存早于 2 小时脏标记过期造成未落库数据丢失。
            var extendDataTask = database.KeyExpireAsync(GetUserDataKey(serverId, userId), TimeSpan.FromHours(3));
            var setTask = database.SetAddAsync(DirtyUsersKey, userKey);
            await Task.WhenAll(markTask, extendDataTask, setTask);
            // SetAddAsync 在成员已存在时返回 false，不应据此判定失败。
            return markTask.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis MarkUserDirty failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清除玩家 dirty 标记，通常在成功落库后调用。
    /// </summary>
    public static async Task<bool> ClearUserDirty(string userId, string serverId)
    {
        if (!IsConnected) return false;
        try
        {
            var database = GetDatabase();
            var deleteTask = database.KeyDeleteAsync(GetUserDirtyKey(serverId, userId));
            var removeTask = database.SetRemoveAsync(DirtyUsersKey, ServerScope.UserKey(serverId, userId));
            await Task.WhenAll(deleteTask, removeTask);
            return deleteTask.Result || removeTask.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis ClearUserDirty failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>单次 MGET 批量读取多名玩家的缓存存档，供定时脏数据批量落库使用。</summary>
    public static async Task<Dictionary<string, UserData>> GetUsersDataBatch(IReadOnlyList<(string UserKey, string ServerId, string UserId)> entries)
    {
        var result = new Dictionary<string, UserData>(entries.Count, StringComparer.Ordinal);
        if (!IsConnected || entries.Count == 0)
            return result;

        try
        {
            var database = GetDatabase();
            var keys = new RedisKey[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                keys[i] = GetUserDataKey(entries[i].ServerId, entries[i].UserId);

            var values = await database.StringGetAsync(keys);
            for (int i = 0; i < values.Length; i++)
            {
                if (!values[i].HasValue) continue;
                byte[]? bytes = values[i];
                if (bytes == null) continue;
                try
                {
                    result[entries[i].UserKey] = UserData.Parser.ParseFrom(bytes);
                }
                catch (Exception parseEx)
                {
                    Console.WriteLine($"Redis batch parse user data failed ({entries[i].UserKey}): {parseEx.Message}");
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetUsersDataBatch failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>单次管线批量清理已成功落库玩家的 dirty 标记。</summary>
    public static async Task ClearUsersDirtyBatch(IReadOnlyList<(string UserKey, string ServerId, string UserId)> entries)
    {
        if (!IsConnected || entries.Count == 0)
            return;

        try
        {
            var database = GetDatabase();
            var dirtyKeys = new RedisKey[entries.Count];
            var members = new RedisValue[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                dirtyKeys[i] = GetUserDirtyKey(entries[i].ServerId, entries[i].UserId);
                members[i] = entries[i].UserKey;
            }

            var batch = database.CreateBatch();
            Task delTask = batch.KeyDeleteAsync(dirtyKeys);
            Task remTask = batch.SetRemoveAsync(DirtyUsersKey, members);
            batch.Execute();
            await Task.WhenAll(delTask, remTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis ClearUsersDirtyBatch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有 dirty 玩家 id。服务器退出或定时落库时使用。
    /// </summary>
    public static async Task<List<string>> GetDirtyUserIds()
    {
        if (!IsConnected) return new List<string>();
        try
        {
            var values = await GetDatabase().SetMembersAsync(DirtyUsersKey);
            return values.Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetDirtyUserIds failed: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// 设置玩家在线状态，带过期时间。客户端心跳或业务包可定期刷新。
    /// </summary>
    public static async Task<bool> SetPlayerOnline(string userId, string serverId)
    {
        if (!IsConnected) return false;
        try
        {
            var database = GetDatabase();
            var userKey = ServerScope.UserKey(serverId, userId);
            var onlineKeyTask = database.StringSetAsync(GetUserOnlineKey(serverId, userId), "1", OnlineExpire);
            var onlineSetTask = database.SetAddAsync(OnlineUsersKey, userKey);
            await Task.WhenAll(onlineKeyTask, onlineSetTask);
            // SetAddAsync 对已存在成员返回 false，续期在线状态仍视为成功。
            return onlineKeyTask.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SetPlayerOnline failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清除玩家在线状态。
    /// </summary>
    public static async Task<bool> SetPlayerOffline(string userId, string serverId)
    {
        if (!IsConnected) return false;
        try
        {
            var database = GetDatabase();
            var deleteOnlineTask = database.KeyDeleteAsync(GetUserOnlineKey(serverId, userId));
            var removeOnlineTask = database.SetRemoveAsync(OnlineUsersKey, ServerScope.UserKey(serverId, userId));
            var clearEmailTask = database.KeyDeleteAsync(GetEmailKey(serverId, userId));
            await Task.WhenAll(deleteOnlineTask, removeOnlineTask, clearEmailTask);
            return deleteOnlineTask.Result || removeOnlineTask.Result || clearEmailTask.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SetPlayerOffline failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 判断玩家是否在线。适合跨服或后台查询。
    /// </summary>
    public static async Task<bool> IsPlayerOnline(string userId, string serverId)
    {
        if (!IsConnected) return false;
        try
        {
            return await GetDatabase().KeyExistsAsync(GetUserOnlineKey(serverId, userId));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis IsPlayerOnline failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>批量判断多名同服玩家是否在线（单次 MGET，避免好友列表 N+1）。</summary>
    public static async Task<HashSet<string>> GetOnlineUsersSet(IReadOnlyList<string> userIds, string serverId)
    {
        var online = new HashSet<string>(StringComparer.Ordinal);
        if (!IsConnected || userIds.Count == 0) return online;
        try
        {
            var database = GetDatabase();
            var keys = new RedisKey[userIds.Count];
            for (int i = 0; i < userIds.Count; i++)
                keys[i] = GetUserOnlineKey(serverId, userIds[i]);
            var values = await database.StringGetAsync(keys);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                    online.Add(userIds[i]);
            }
            return online;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetOnlineUsersSet failed: {ex.Message}");
            return online;
        }
    }

    /// <summary>
    /// 获取当前在线玩家 id 列表。过期的在线 key 会批量从集合中清理掉。
    /// </summary>
    public static async Task<List<string>> GetOnlineUserIds()
    {
        if (!IsConnected) return new List<string>();
        try
        {
            var database = GetDatabase();
            var values = await database.SetMembersAsync(OnlineUsersKey);
            var userKeys = new List<string>(values.Length);
            foreach (var value in values)
            {
                var userKey = value.ToString();
                if (!string.IsNullOrWhiteSpace(userKey))
                    userKeys.Add(userKey);
            }
            if (userKeys.Count == 0) return userKeys;

            var result = new List<string>(userKeys.Count);
            var expired = new List<RedisValue>();
            const int batchSize = 500;
            for (int offset = 0; offset < userKeys.Count; offset += batchSize)
            {
                int count = Math.Min(batchSize, userKeys.Count - offset);
                var keys = new RedisKey[count];
                for (int i = 0; i < count; i++)
                    keys[i] = $"player:online:{userKeys[offset + i]}";
                var exists = await database.StringGetAsync(keys);
                for (int i = 0; i < count; i++)
                {
                    if (exists[i].HasValue)
                        result.Add(userKeys[offset + i]);
                    else
                        expired.Add(userKeys[offset + i]);
                }
            }

            if (expired.Count > 0)
                await database.SetRemoveAsync(OnlineUsersKey, expired.ToArray());

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetOnlineUserIds failed: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// 缓存玩家邮件列表。邮件列表用 Msg.EmailList 承载，直接 protobuf 二进制保存。
    /// </summary>
    public static async Task<bool> SetEmail(string userId, string serverId, List<EmailData> emails, TimeSpan? expire = null)
    {
        if (!IsConnected) return false;

        try
        {
            var msg = new Msg();
            msg.EmailList.AddRange(emails);

            var database = GetDatabase();
            return await database.StringSetAsync(
                GetEmailKey(serverId, userId),
                msg.ToByteArray(),
                expire ?? DefaultCacheExpire);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis SetEmail failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从 Redis 读取玩家邮件列表。缓存不存在或解析失败时返回 null。
    /// </summary>
    public static async Task<List<EmailData>?> GetEmail(string userId, string serverId)
    {
        var bytes = await GetBytes(GetEmailKey(serverId, userId));
        if (bytes == null) return null;

        try
        {
            var msg = Msg.Parser.ParseFrom(bytes);
            return msg.EmailList.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis parse email failed: {ex.Message}");
            await Delete(GetEmailKey(serverId, userId));
            return null;
        }
    }

    /// <summary>
    /// 删除玩家邮件缓存，下次读取时会重新从 MongoDB 拉取。
    /// </summary>
    public static Task<bool> ClearEmail(string userId, string serverId)
    {
        return Delete(GetEmailKey(serverId, userId));
    }

    /// <summary>
    /// 尝试获取简单分布式锁。成功后要用同一个 lockValue 调用 ReleaseLock。
    /// </summary>
    public static async Task<bool> TryAcquireLock(string lockKey, string lockValue, TimeSpan? expire = null)
    {
        if (!IsConnected) return false;
        try
        {
            return await GetDatabase().StringSetAsync(
                GetLockKey(lockKey),
                lockValue,
                expire ?? LockExpire,
                When.NotExists);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis TryAcquireLock failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 释放分布式锁。只有 value 匹配时才删除，避免误删其他线程/服务器的锁。
    /// </summary>
    public static async Task<bool> ReleaseLock(string lockKey, string lockValue)
    {
        if (!IsConnected) return false;
        try
        {
            const string script = """
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end
                """;

            var result = await GetDatabase().ScriptEvaluateAsync(
                script,
                new RedisKey[] { GetLockKey(lockKey) },
                new RedisValue[] { lockValue });

            return (int)result == 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis ReleaseLock failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 更新排行榜数据。
    /// Redis score 保存等级；同等级再按 RankData.LevelTime 升序排序。
    /// </summary>
    public static async Task<bool> UpdateRankData(string rankName, string serverId, RankData rankData)
    {
        if (!IsConnected) return false;
        serverId = ServerScope.Normalize(serverId);

        var userId = string.IsNullOrWhiteSpace(rankData.Id)
            ? rankData.UserData?.UserId
            : rankData.Id;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        rankData.Id = userId;
        if (rankData.UserData == null)
        {
            rankData.UserData = new UserData { UserId = userId };
        }
        else
        {
            rankData.UserData.UserId = userId;
        }
        rankData.UserData.ServerId = serverId;

        try
        {
            var database = GetDatabase();
            var scopedRankName = $"{serverId}:{rankName}";
            var oldBytes = await GetBytes(GetRankDataKey(serverId, rankName, userId));
            if (rankName == BattleRankPolicy.RankName)
            {
                RankData? oldRank = null;
                if (oldBytes != null)
                {
                    try { oldRank = RankData.Parser.ParseFrom(oldBytes); }
                    catch (InvalidProtocolBufferException) { }
                }
                BattleRankPolicy.MergeBest(rankData, oldRank);
            }
            else if (oldBytes != null)
            {
                try
                {
                    var oldRank = RankData.Parser.ParseFrom(oldBytes);

                    rankData.Level = rankData.Level > oldRank.Level ? rankData.Level : oldRank.Level;
                    
                    rankData.LevelTime = rankData.Level <= oldRank.Level && oldRank.LevelTime > 0
                        ? oldRank.LevelTime
                        : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                }
                catch { rankData.LevelTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
            }
            else if (rankData.LevelTime <= 0)
            {
                rankData.LevelTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            var nameTask = database.SetAddAsync(RankNamesKey, scopedRankName);
            var dataTask = database.StringSetAsync(
                GetRankDataKey(serverId, rankName, userId),
                rankData.ToByteArray(),
                DefaultCacheExpire);
            var rankTask = database.SortedSetAddAsync(
                GetRankKey(serverId, rankName),
                userId,
                rankData.Level);

            await Task.WhenAll(nameTask, dataTask, rankTask);
            // SetAddAsync / SortedSetAddAsync 在成员已存在时返回 false，不应据此判定更新失败。
            return dataTask.Result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis UpdateRankData failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取排行榜数据，Level 越大越靠前；Level 相同则 LevelTime 越小越靠前。
    /// </summary>
    public static async Task<List<RankData>> GetRankTopData(string rankName, string serverId, int count = 100)
    {
        if (!IsConnected) return new List<RankData>();

        try
        {
            var database = GetDatabase();
            var safeCount = Math.Clamp(count, 1, 1000);
            var candidateCount = Math.Clamp(safeCount * 10, 100, 5000);
            var candidates = await database.SortedSetRangeByRankWithScoresAsync(
                GetRankKey(serverId, rankName),
                0,
                candidateCount - 1,
                Order.Descending);
            var userIds = candidates.Select(entry => entry.Element).ToList();
            // Redis score only contains the level. Include all players tied at the
            // cutoff so an arbitrary user-ID ordering cannot hide a faster run.
            if (candidates.Length == candidateCount)
            {
                double boundaryLevel = candidates[^1].Score;
                var tied = await database.SortedSetRangeByScoreAsync(
                    GetRankKey(serverId, rankName), boundaryLevel, boundaryLevel);
                userIds = userIds.Concat(tied).Distinct().ToList();
            }

            var validUserIds = userIds
                .Select(x => x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var rankData = new List<RankData>(validUserIds.Count);
            var corruptedKeys = new List<RedisKey>();
            const int batchSize = 500;
            for (int offset = 0; offset < validUserIds.Count; offset += batchSize)
            {
                int batchCount = Math.Min(batchSize, validUserIds.Count - offset);
                var keys = new RedisKey[batchCount];
                for (int i = 0; i < batchCount; i++)
                    keys[i] = GetRankDataKey(serverId, rankName, validUserIds[offset + i]);

                var values = await database.StringGetAsync(keys);
                for (int i = 0; i < batchCount; i++)
                {
                    if (!values[i].HasValue) continue;
                    try
                    {
                        byte[]? bytes = values[i];
                        if (bytes == null) continue;
                        var parsed = RankData.Parser.ParseFrom(bytes);
                        if (rankName != BattleRankPolicy.RankName || BattleRankPolicy.IsValid(parsed))
                            rankData.Add(parsed);
                    }
                    catch
                    {
                        corruptedKeys.Add(keys[i]);
                    }
                }
            }

            if (corruptedKeys.Count > 0)
                await database.KeyDeleteAsync(corruptedKeys.ToArray());

            return rankData
                .OrderByDescending(x => x.Level)
                .ThenBy(x => x.LevelTime <= 0 ? long.MaxValue : x.LevelTime)
                .ThenBy(x => x.UserId, StringComparer.Ordinal)
                .Take(safeCount)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis GetRankTopData failed: {ex.Message}");
            return new List<RankData>();
        }
    }

    /// <summary>
    /// 清除指定排行榜的 Redis 缓存，包括排序集合和每个玩家的 RankData 缓存。
    /// </summary>
    public static async Task<bool> ClearRankData(string rankName, string serverId)
    {
        if (!IsConnected) return false;

        try
        {
            var database = GetDatabase();
            var rankKey = GetRankKey(serverId, rankName);
            var userIds = await database.SortedSetRangeByRankAsync(rankKey);
            var keys = userIds
                .Select(x => x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(userId => (RedisKey)GetRankDataKey(serverId, rankName, userId))
                .Append(rankKey)
                .ToArray();

            if (keys.Length == 0)
            {
                return true;
            }

            await database.KeyDeleteAsync(keys);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis ClearRankData failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清除 Redis 中所有排行榜相关数据。
    /// </summary>
    public static async Task<bool> ClearAllRankData()
    {
        if (!IsConnected) return false;

        try
        {
            var database = GetDatabase();
            var rankNames = await database.SetMembersAsync(RankNamesKey);
            foreach (var rankNameValue in rankNames)
            {
                var scopedRankName = rankNameValue.ToString();
                var separator = scopedRankName.IndexOf(':');
                if (separator > 0)
                {
                    await ClearRankData(scopedRankName[(separator + 1)..], scopedRankName[..separator]);
                }
            }

            await database.KeyDeleteAsync(RankNamesKey);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis ClearAllRankData failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取公告
    /// </summary>
    /// <returns></returns>
    public static async Task<string> GetGongGao()
    {
        return await GetString("GongGao")?? string.Empty;
    }

    public static async Task SetGongGao(string gongGao)
    {
        await SetString("GongGao", gongGao);
    }


    private static IDatabase GetDatabase()
    {
        return _database ?? throw new InvalidOperationException("Redis is not connected.");
    }

    private static string GetUserDataKey(string serverId, string userId) => $"player:data:{ServerScope.UserKey(serverId, userId)}";
    private static string GetUserDirtyKey(string serverId, string userId) => $"player:dirty:{ServerScope.UserKey(serverId, userId)}";
    private static string GetUserOnlineKey(string serverId, string userId) => $"player:online:{ServerScope.UserKey(serverId, userId)}";
    private static string GetEmailKey(string serverId, string userId) => $"email:list:{ServerScope.UserKey(serverId, userId)}";
    private static string GetLockKey(string key) => $"lock:{key}";
    private static string GetRankKey(string serverId, string rankName) => $"rank:{ServerScope.Normalize(serverId)}:{BattleRankPolicy.CacheName(rankName)}";
    private static string GetRankDataKey(string serverId, string rankName, string userId) => $"rank:data:{ServerScope.Normalize(serverId)}:{BattleRankPolicy.CacheName(rankName)}:{userId}";

    private static string NormalizeNumericString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0";
        }

        var digits = new string(value.Where(char.IsDigit).ToArray()).TrimStart('0');
        return digits.Length == 0 ? "0" : digits;
    }

    private sealed class NumericStringComparer : IComparer<string>
    {
        public static readonly NumericStringComparer Instance = new NumericStringComparer();

        public int Compare(string? x, string? y)
        {
            var left = NormalizeNumericString(x);
            var right = NormalizeNumericString(y);
            var lengthCompare = left.Length.CompareTo(right.Length);
            return lengthCompare != 0
                ? lengthCompare
                : string.CompareOrdinal(left, right);
        }
    }
}
