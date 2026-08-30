using GameData;
using System.Threading;
using Google.Protobuf.Collections;

namespace WebSocketDemo;

/// <summary>添加好友业务操作的结果。</summary>
internal enum AddFriendResult
{
    Success,
    InvalidTarget,
    TargetNotFound,
    FriendLimitReached,
    SaveFailed
}

/// <summary>
/// 游戏数据访问入口。
/// 业务层优先调用这个类，由它判断当前数据应该走 Redis 缓存还是 MongoDB 持久化。
/// </summary>
public static class GameDataMrg
{
    private const int SaveAllConcurrency = 16;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>
        UserSaveLocks = new();
    private const int WeeklyRankRewardLimit = 100;
    private const string WeeklyRankRewardItemId = "weekly_rank_reward";


    public static async Task AddEquips(string userId,string serverId,List<EquipData> equips)
    {
        if (!MongoDBMrg.IsConnected)return;
        await MongoDBMrg.AddEquips(userId, serverId, equips);
        if (RedisMrg.IsConnected)
        {
            var userData = await MongoDBMrg.GetUserAsCreate(userId, serverId);
            await RedisMrg.SetUserData(userId, serverId, userData);
        }
    }


    public static async Task AddEmails(string userId, string serverId, List<EmailData> emails)
    {
        if (!MongoDBMrg.IsConnected)return;
        await MongoDBMrg.AddEmails(userId, serverId, emails);
        if (RedisMrg.IsConnected)
        {
            await RedisMrg.ClearEmail(userId, serverId);
        }
    }
    
    /// <summary>
    /// 获取玩家数据；Redis 有缓存时直接返回，缓存没有时回源 MongoDB，并写入 Redis。
    /// 如果玩家不存在，则由 MongoDB 原子创建玩家数据。
    /// </summary>
    public static async Task<UserData> GetUserAsCreate(string userId, string serverId)
    {
        serverId = ServerScope.Normalize(serverId);
        if (RedisMrg.IsConnected)
        {
            var cachedUser = await RedisMrg.GetUserData(userId, serverId);
            if (cachedUser != null)
            {
                await RedisMrg.SetPlayerOnline(userId, serverId);
                //await UpdateFightingCapacityRank(userId, serverId, cachedUser);
                return cachedUser;
            }
        }

        var userData = await MongoDBMrg.GetUserAsCreate(userId, serverId);
        if (RedisMrg.IsConnected)
        {
            await RedisMrg.SetUserData(userId, serverId, userData);
            await RedisMrg.SetPlayerOnline(userId, serverId);
            await RefreshEmailCache(userId, serverId);
        }

        // 新账号创建或老账号从 MongoDB 回源后立即加入/刷新排行榜，
        // 不再要求客户端先触发一次 SaveUserData 才能在榜单中出现。
        //await UpdateFightingCapacityRank(userId, serverId, userData);
        return userData;
    }

    /// <summary>
    /// 只获取玩家数据，不自动创建。
    /// 适合后台查看玩家是否存在，或业务中需要判断用户是否已注册的场景。
    /// </summary>
    public static async Task<UserData?> GetUser(string userId, string serverId)
    {
        if (RedisMrg.IsConnected)
        {
            var cachedUser = await RedisMrg.GetUserData(userId, serverId);
            if (cachedUser != null)
            {
                return cachedUser;
            }
        }

        var userData = await MongoDBMrg.GetUser(userId, serverId);
        if (userData != null && RedisMrg.IsConnected)
        {
            await RedisMrg.SetUserData(userId, serverId, userData);
        }

        return userData;
    }

    /// <summary>
    /// 保存玩家数据。
    /// Redis 可用时先写 Redis 并标记 dirty，后续再统一落库；Redis 不可用时直接写 MongoDB。
    /// </summary>
    public static async Task<bool> SaveUserData(
        string userId, string serverId, UserData userData, bool updateRank = true)
    {
        serverId = ServerScope.Normalize(serverId);
        userData.UserId = userId;
        userData.ServerId = serverId;
        string userKey = ServerScope.UserKey(serverId, userId);
        SemaphoreSlim saveLock = UserSaveLocks.GetOrAdd(userKey, _ => new SemaphoreSlim(1, 1));
        bool saved;
        await saveLock.WaitAsync();
        try
        {
            if (RedisMrg.IsConnected)
            {
                var cacheSaved = await RedisMrg.SetUserData(userId, serverId, userData);
                if (cacheSaved)
                {
                    await RedisMrg.MarkUserDirty(userId, serverId);
                    saved = true;
                }
                else
                {
                    saved = await MongoDBMrg.UpdateUserData(userId, serverId, userData);
                }
            }
            else
            {
                saved = await MongoDBMrg.UpdateUserData(userId, serverId, userData);
            }
        }
        finally
        {
            saveLock.Release();
        }

        // if (saved && updateRank)
        //     await UpdateFightingCapacityRank(userId, serverId, userData);
        return saved;
    }

    /// <summary>
    /// 手动刷新指定玩家的战斗力排行榜。
    /// 只接收玩家标识，玩家数据从服务端 Redis/MongoDB 获取，避免客户端伪造战斗力。
    /// </summary>
    public static async Task<bool> UpdateRank(string userId, string serverId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        serverId = ServerScope.Normalize(serverId);
        try
        {
            var userData = await GetUser(userId, serverId);
            if (userData != null)
            {
                return await UpdateRank(userId, serverId, userData);
            }

            Console.WriteLine(
                $"Manual rank update ignored: user not found, user={userId}, server={serverId}.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Manual rank update failed: user={userId}, server={serverId}, error={ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 使用服务端已经校验过的玩家快照手动刷新战斗力排行榜。
    /// 排行榜故障不应阻止登录或用户数据保存，因此方法会记录错误并返回 false。
    /// </summary>
    public static async Task<bool> UpdateRank(
        string userId,
        string serverId,
        UserData userData)
    {
        if (string.IsNullOrWhiteSpace(userId) || userData == null)
        {
            return false;
        }

        serverId = ServerScope.Normalize(serverId);
        try
        {
            long level = 0;
            string? levelText = userData.DataDic.GetValueOrDefault("Level");
            if (string.IsNullOrWhiteSpace(levelText))
                levelText = userData.DataDic.GetValueOrDefault("level");
            long.TryParse(levelText, out level);
            return await UpdateRankData("fightingCapacity", serverId, new RankData
            {
                Id = userId,
                UserData = userData.Clone(),
                RoleType = userData.RoleType,
                Level = Math.Max(0, level)
            });
        }
        catch (Exception ex)
        {
            // 排行榜属于派生数据，写榜失败不能中断登录或覆盖用户数据保存结果。
            Console.WriteLine(
                $"Update fighting-capacity rank failed: user={userId}, server={serverId}, error={ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 强制把玩家数据写入 MongoDB。
    /// 适合玩家下线、关键付费数据变更、服务器关闭前保存。
    /// </summary>
    public static async Task<bool> SaveUserDataToMongo(string userId, string serverId)
    {
        serverId = ServerScope.Normalize(serverId);
        string userKey = ServerScope.UserKey(serverId, userId);
        SemaphoreSlim saveLock = UserSaveLocks.GetOrAdd(userKey, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync();
        try
        {
            var userData = RedisMrg.IsConnected
                ? await RedisMrg.GetUserData(userId, serverId)
                : null;

            userData ??= await MongoDBMrg.GetUser(userId, serverId);
            if (userData == null)
                return false;

            var saved = await MongoDBMrg.UpdateUserData(userId, serverId, userData);
            if (saved && RedisMrg.IsConnected)
                await RedisMrg.ClearUserDirty(userId, serverId);
            return saved;
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>
    /// 保存 Redis 中所有 dirty 玩家到 MongoDB。
    /// 用于服务器正常关闭、进程退出、未捕获异常前的抢救保存。
    /// </summary>
    public static async Task<int> SaveAllDirtyUsersToMongo()
    {
        if (!RedisMrg.IsConnected)
        {
            return 0;
        }

        var userIds = await RedisMrg.GetDirtyUserIds();
        if (userIds.Count == 0)
        {
            return 0;
        }

        var savedCount = 0;
        using var concurrency = new SemaphoreSlim(SaveAllConcurrency, SaveAllConcurrency);
        var tasks = userIds.Select(async userKey =>
        {
            await concurrency.WaitAsync();
            try
            {
                if (ServerScope.TryParseUserKey(userKey, out var serverId, out var userId) &&
                    await SaveUserDataToMongo(userId, serverId))
                {
                    Interlocked.Increment(ref savedCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save dirty user {userKey} failed: {ex.Message}");
            }
            finally
            {
                concurrency.Release();
            }
        });

        await Task.WhenAll(tasks);
        return savedCount;
    }

    /// <summary>
    /// 删除玩家数据。
    /// 永久数据从 MongoDB 删除，Redis 中的缓存和状态也同步清理。
    /// </summary>
    public static async Task<bool> DeleteUser(string userId, string serverId)
    {
        var deleted = await MongoDBMrg.DeleteUser(userId, serverId);
        if (RedisMrg.IsConnected)
        {
            await RedisMrg.SetPlayerOffline(userId, serverId);
            await RedisMrg.ClearUserDirty(userId, serverId);
            await RedisMrg.Delete($"player:data:{ServerScope.UserKey(serverId, userId)}");
        }

        return deleted;
    }

    /// <summary>
    /// 玩家上线状态。
    /// 在线状态属于临时数据，优先使用 Redis；Redis 不可用时直接忽略，不影响主流程。
    /// </summary>
    public static Task SetPlayerOnline(string userId, string serverId)
    {
        return RedisMrg.IsConnected ? RedisMrg.SetPlayerOnline(userId, serverId) : Task.CompletedTask;
    }

    /// <summary>
    /// 玩家下线状态，同时尝试把 Redis 缓存中的玩家数据落到 MongoDB。
    /// </summary>
    public static async Task SetPlayerOffline(string userId, string serverId)
    {
        await SaveUserDataToMongo(userId, serverId);
        if (PlayerSessionManager.Instance.HasOnlineUser(userId, serverId))
        {
            return;
        }

        if (RedisMrg.IsConnected)
        {
            await RedisMrg.SetPlayerOffline(userId, serverId);
        }
    }

    /// <summary>
    /// 新增邮件。
    /// 邮件是永久数据，先写 MongoDB；如果 Redis 有缓存则清掉，避免读到旧邮件。
    /// </summary>
    public static async Task AddEmail(EmailData email, string serverId)
    {
        serverId = ServerScope.Normalize(serverId);
        await MongoDBMrg.AddEmail(email, serverId);
        if (RedisMrg.IsConnected && !string.IsNullOrWhiteSpace(email.UserId))
        {
            await RedisMrg.ClearEmail(email.UserId, serverId);
        }
    }

    /// <summary>
    /// 获取玩家邮件。
    /// 只从 Redis 读取。后台服务每 10 秒为在线玩家从 MongoDB 刷新一次邮件缓存。
    /// </summary>
    public static async Task<List<EmailData>> GetEmail(string userId, string serverId, int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 1000);
        List<EmailData> personalEmails;

        if (RedisMrg.IsConnected)
        {
            var cachedEmails = await RedisMrg.GetEmail(userId, serverId);
            if (cachedEmails != null)
            {
                personalEmails = cachedEmails.Take(limit).ToList();
                return await MergeGlobalEmails(userId, serverId, personalEmails, limit);
            }
        }

        if (!MongoDBMrg.IsConnected)
        {
            return new List<EmailData>();
        }

        // Redis cache misses after the operations site adds a mail and clears
        // the old key. Read through to MongoDB immediately instead of waiting
        // for the periodic online-player cache refresh.
        personalEmails = await MongoDBMrg.GetEmail(userId, serverId, limit);
        if (RedisMrg.IsConnected)
        {
            await RedisMrg.SetEmail(userId, serverId, personalEmails);
        }

        return await MergeGlobalEmails(userId, serverId, personalEmails, limit);
    }

    private static async Task<List<EmailData>> MergeGlobalEmails(
        string userId, string serverId, List<EmailData> personalEmails, int limit)
    {
        var globalEmails = await MongoDBMrg.GetAvailableGlobalEmails(userId, serverId, limit);
        return personalEmails.Concat(globalEmails)
            .OrderByDescending(x => x.CreateTime)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// 更新邮件状态。
    /// 邮件领取、过期等状态必须永久保存，所以直接写 MongoDB。
    /// </summary>
    public static Task<bool> UpdateEmailState(string emailId, int state)
    {
        return MongoDBMrg.UpdateEmailState(emailId, state);
    }

    public static Task<bool> UpdateEmailStates(string[] emailId, int state)
    {
        return MongoDBMrg.UpdateEmailsState(emailId, state);
    }

    /// <summary>
    /// 原子抢占玩家未领取的邮件，避免并发请求重复发放奖励。
    /// 状态 0 表示未领取，2 表示领取处理中。
    /// </summary>
    public static async Task<List<EmailData>> ClaimEmailRewards(
        string userId,
        string serverId,
        IEnumerable<string> emailIds)
    {
        var ids = emailIds.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal).ToArray();
        var claimed = await MongoDBMrg.ClaimEmails(
            userId, serverId, ids.Where(x => !x.StartsWith("global:", StringComparison.Ordinal)), 0, 2);
        claimed.AddRange(await MongoDBMrg.ClaimGlobalEmails(userId, serverId, ids));
        if (claimed.Count > 0 && RedisMrg.IsConnected)
        {
            await RedisMrg.ClearEmail(userId, serverId);
        }

        return claimed;
    }

    public static Task CompleteGlobalEmailClaims(
        string userId, string serverId, IEnumerable<string> emailIds, bool success) =>
        MongoDBMrg.CompleteGlobalEmailClaims(userId, serverId, emailIds, success);

    /// <summary>
    /// 删除邮件。
    /// </summary>
    public static Task<bool> DeleteEmail(string emailId)
    {
        return MongoDBMrg.DeleteEmail(emailId);
    }

    public static Task<bool> DeleteEmails(string[] emailIds)
    {
        return MongoDBMrg.DeleteEmails(emailIds);
    }

    /// <summary>
    /// 从 MongoDB 刷新一个玩家的邮件缓存到 Redis。
    /// </summary>
    public static async Task<bool> RefreshEmailCache(string userId, string serverId, int limit = 50)
    {
        if (!MongoDBMrg.IsConnected || !RedisMrg.IsConnected)
        {
            return false;
        }

        var emails = await MongoDBMrg.GetEmail(userId, serverId, limit);
        return await RedisMrg.SetEmail(userId, serverId, emails);
    }
    
    /// <summary>
    /// 刷新所有在线玩家的邮件缓存。
    /// </summary>
    public static async Task<int> RefreshOnlineEmailCaches(int limit = 50)
    {
        if (!RedisMrg.IsConnected || !MongoDBMrg.IsConnected)
        {
            return 0;
        }

        var userIds = await RedisMrg.GetOnlineUserIds();
        var refreshedCount = 0;
        using var concurrency = new SemaphoreSlim(16, 16);
        var tasks = userIds.Select(async userKey =>
        {
            await concurrency.WaitAsync();
            try
            {
                if (ServerScope.TryParseUserKey(userKey, out var serverId, out var userId) &&
                    await RefreshEmailCache(userId, serverId, limit))
                {
                    Interlocked.Increment(ref refreshedCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Refresh email cache failed: {userKey}, {ex.Message}");
            }
            finally
            {
                concurrency.Release();
            }
        });

        await Task.WhenAll(tasks);
        return refreshedCount;
    }

    /// <summary>
    /// 更新排行榜。
    /// 排行榜保存完整 RankData，按 Level 降序、LevelTime 升序排序。
    /// </summary>
    public static async Task<bool> UpdateRankData(string rankName, string serverId, RankData rankData)
    {
        var mongoSaved = false;
        if (MongoDBMrg.IsConnected)
        {
            mongoSaved = await MongoDBMrg.UpsertRankData(rankName, serverId, rankData);
        }

        var redisSaved = false;
        if (RedisMrg.IsConnected)
        {
            redisSaved = await RedisMrg.UpdateRankData(rankName, serverId, rankData);
        }

        return mongoSaved || redisSaved;
    }

    /// <summary>
    /// 获取排行榜。
    /// 返回完整 RankData，并按 Level 降序、LevelTime 升序排序。
    /// </summary>
    public static async Task<List<RankData>> GetRankTopData(string rankName, string serverId, int count = 100)
    {
        if (RedisMrg.IsConnected)
        {
            var redisRanks = await RedisMrg.GetRankTopData(rankName, serverId, count);
            if (redisRanks.Count > 0)
            {
                return redisRanks;
            }
        }

        if (MongoDBMrg.IsConnected)
        {
            return await MongoDBMrg.GetRankData(rankName, serverId, count);
        }

        return new List<RankData>();
    }

    /// <summary>
    /// 清除排行榜。MongoDB 和 Redis 都会清掉，适合每周结算后重置榜单。
    /// </summary>
    public static async Task ClearRankData(string rankName, string serverId)
    {
        if (MongoDBMrg.IsConnected)
        {
            var deletedCount = await MongoDBMrg.ClearRankData(rankName, serverId);
            Console.WriteLine($"Mongo rank data cleared: {rankName}, count: {deletedCount}");
        }

        if (RedisMrg.IsConnected)
        {
            await RedisMrg.ClearRankData(rankName, serverId);
            Console.WriteLine($"Redis rank data cleared: {rankName}");
        }
    }

    /// <summary>
    /// 清除所有排行榜相关数据。每周重置榜单时使用。
    /// </summary>
    public static async Task ClearAllRankData()
    {
        if (MongoDBMrg.IsConnected)
        {
            var deletedCount = await MongoDBMrg.ClearAllRankData();
            Console.WriteLine($"All Mongo rank data cleared, count: {deletedCount}");
        }

        if (RedisMrg.IsConnected)
        {
            await RedisMrg.ClearAllRankData();
            Console.WriteLine("All Redis rank data cleared.");
        }
    }

    /// <summary>
    /// 给所有排行榜上的玩家发放每周奖励。
    /// 每个榜单独排名；同一玩家如果在多个排行榜上，会收到多封对应榜单奖励邮件。
    /// </summary>
    public static async Task<int> AwardWeeklyRankRewards()
    {
        if (!MongoDBMrg.IsConnected)
        {
            return 0;
        }

        var allRankData = await MongoDBMrg.GetAllRankData();
        if (allRankData.Count == 0)
        {
            return 0;
        }

        var mailCount = 0;
        foreach (var rankGroup in allRankData.GroupBy(x => new { x.ServerId, x.RankName }))
        {
            var rankIndex = 1;
            foreach (var rankData in rankGroup
                         .OrderBy(x => x, MongoRankDataComparer.Instance)
                         .Take(WeeklyRankRewardLimit))
            {
                if (string.IsNullOrWhiteSpace(rankData.UserId))
                {
                    continue;
                }

                await MongoDBMrg.AddEmail(new EmailData
                {
                    UserId = rankData.UserId,
                    ItemId = GetWeeklyRankRewardItemId(rankData.RankName, rankIndex),
                    ItemCount = GetWeeklyRankRewardCount(rankIndex),
                    State = 0
                }, rankData.ServerId);

                mailCount++;
                rankIndex++;
            }
        }

        return mailCount;
    }

    private static string GetWeeklyRankRewardItemId(string rankName, int rankIndex)
    {
        return $"{WeeklyRankRewardItemId}:{rankName}:{rankIndex}";
    }

    private static long GetWeeklyRankRewardCount(int rankIndex)
    {
        return rankIndex switch
        {
            1 => 1000,
            <= 3 => 800,
            <= 10 => 500,
            <= 50 => 300,
            <= 100 => 200,
            _ => 0
        };
    }

    public static async Task RefreshGameServerCaches()
    {
        if (!RedisMrg.IsConnected || !MongoDBMrg.IsConnected)
        {
            return;
        }
        var allServer = await MongoDBMrg.GetServers();
        var serverList = new ServerDataList();
        serverList.List.AddRange(allServer);
        await RedisMrg.SetServers(serverList);
    }

    /// <summary>
    /// 获取服务器列表
    /// </summary>
    /// <returns></returns>
    public static async Task<ServerDataList> GetServers()
    {
        try
        {
            if (!RedisMrg.IsConnected || !MongoDBMrg.IsConnected) return new ServerDataList();
            return await RedisMrg.GetServers();
        }
        catch (Exception exception)
        {
            Console.WriteLine("Get servers error: " + exception.Message);
            return new ServerDataList();
        }
    }

    /// <summary>
    /// 删除使用了的邮件
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    public static async Task DeleteUseEmail(string userId, string serverId)
    {
        var allEmail =await GetEmail(userId, serverId);
        var useEmail = allEmail.Where((data => data.State != 0)).Select((data =>data.Id)).ToArray();
        await DeleteEmails(useEmail);
    }
    
    
    public static async Task AddServers(string serverName,string serverId)
    {
        try
        {
            if (!RedisMrg.IsConnected||!MongoDBMrg.IsConnected) return;
            await MongoDBMrg.AddServer(new ServerData() { ServerName = serverName ,ServerId =  serverId });
        }
        catch (Exception exception)
        {
            Console.WriteLine("Add servers error: " + exception.Message);
            return;
        }
    }

    public static async Task SetRoleType(string id, string serverId, int roleType)
    {
        var userData=await RedisMrg.GetUserData(id, serverId);
        if (userData!=null)
        {
            userData.RoleType = roleType;
            await RedisMrg.SetUserData(id, serverId, userData);
        }
        await MongoDBMrg.SetRoleType(id, serverId,roleType);
    }

    /// <summary>持久化玩家头像，并在 Redis 可用时同步刷新缓存。</summary>
    /// <param name="id">玩家 ID。</param>
    /// <param name="serverId">玩家所在的逻辑区服 ID。</param>
    /// <param name="avatar">头像链接；空字符串表示清除头像。</param>
    /// <returns>更新后的完整玩家数据；玩家不存在时返回 <see langword="null"/>。</returns>
    public static async Task<UserData?> SetAvatar(string id, string serverId, string avatar)
    {
        var userData = await MongoDBMrg.SetAvatar(id, serverId, avatar);
        if (userData != null && RedisMrg.IsConnected)
        {
            await RedisMrg.SetUserData(id, serverId, userData);
        }
        return userData;
    }

    /// <summary>
    /// 校验目标玩家和双方好友上限，然后以幂等方式建立双向好友关系。
    /// </summary>
    /// <param name="userId">发起添加的玩家 ID。</param>
    /// <param name="friendUserId">目标好友玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>用于转换成客户端错误码的业务结果。</returns>
    internal static async Task<AddFriendResult> AddFriend(
        string userId, string friendUserId, string serverId)
    {
        if (string.IsNullOrWhiteSpace(friendUserId) ||
            string.Equals(userId, friendUserId, StringComparison.Ordinal))
            return AddFriendResult.InvalidTarget;

        UserData? friend = await GetUser(friendUserId, serverId);
        if (friend == null)
            return AddFriendResult.TargetNotFound;

        Task<List<string>> currentFriendsTask = MongoDBMrg.GetFriendUserIds(userId, serverId);
        Task<List<string>> targetFriendsTask = MongoDBMrg.GetFriendUserIds(friendUserId, serverId);
        await Task.WhenAll(currentFriendsTask, targetFriendsTask);
        List<string> currentFriends = currentFriendsTask.Result;
        List<string> targetFriends = targetFriendsTask.Result;
        if (currentFriends.Contains(friendUserId, StringComparer.Ordinal))
            return AddFriendResult.Success;
        if (currentFriends.Count >= 200 || targetFriends.Count >= 200)
            return AddFriendResult.FriendLimitReached;

        return await MongoDBMrg.AddFriend(userId, friendUserId, serverId)
            ? AddFriendResult.Success
            : AddFriendResult.SaveFailed;
    }

    /// <summary>解除两个同区服玩家之间的双向好友关系。</summary>
    /// <param name="userId">关系一方的玩家 ID。</param>
    /// <param name="friendUserId">关系另一方的玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>是否确实删除了一条关系记录。</returns>
    public static Task<bool> DeleteFriend(string userId, string friendUserId, string serverId)
    {
        return MongoDBMrg.DeleteFriend(userId, friendUserId, serverId);
    }

    /// <summary>检查两个同区服玩家是否为好友，供私聊权限校验使用。</summary>
    /// <param name="userId">关系一方的玩家 ID。</param>
    /// <param name="friendUserId">关系另一方的玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>双方为好友时为 <see langword="true"/>。</returns>
    public static Task<bool> AreFriends(string userId, string friendUserId, string serverId)
    {
        return MongoDBMrg.AreFriends(userId, friendUserId, serverId);
    }

    /// <summary>
    /// 组装好友公开资料与实时在线状态，并将在线好友排在离线好友之前。
    /// </summary>
    /// <param name="userId">需要查询好友列表的玩家 ID。</param>
    /// <param name="serverId">玩家所在的逻辑区服 ID。</param>
    /// <returns>不包含装备、道具等私有数据的好友列表。</returns>
    public static async Task<FriendListData> GetFriendList(string userId, string serverId)
    {
        List<string> friendIds = await MongoDBMrg.GetFriendUserIds(userId, serverId);
        FriendData?[] friends = await Task.WhenAll(friendIds.Select(async friendId =>
        {
            UserData? userData = await GetUser(friendId, serverId);
            if (userData == null)
                return null;

            bool online = PlayerSessionManager.Instance.HasOnlineUser(friendId, serverId) ||
                          (RedisMrg.IsConnected && await RedisMrg.IsPlayerOnline(friendId, serverId));
            return new FriendData
            {
                UserId = userData.UserId,
                Name = userData.Name,
                Avatar = userData.Avatar,
                RoleType = userData.RoleType,
                Online = online
            };
        }));

        var result = new FriendListData();
        result.Friends.AddRange(friends
            .Where(friend => friend != null)
            .Select(friend => friend!)
            .OrderByDescending(friend => friend.Online)
            .ThenBy(friend => friend.Name, StringComparer.Ordinal)
            .ThenBy(friend => friend.UserId, StringComparer.Ordinal));
        return result;
    }

    /// <summary>
    /// 获取公告
    /// </summary>
    /// <returns></returns>
    public static async Task<string> GetGongGao()
    {
        return await RedisMrg.GetGongGao();
    }

    public static async Task AddGongGao(string gongGao)
    {
        await MongoDBMrg.AddGongGao(gongGao);
    }
    

    private sealed class MongoRankDataComparer : IComparer<RankData>
    {
        public static readonly MongoRankDataComparer Instance = new MongoRankDataComparer();

        public int Compare(RankData? x, RankData? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            var levelCompare = y.Level.CompareTo(x.Level);
            if (levelCompare != 0) return levelCompare;
            var xTime = x.LevelTime <= 0 ? long.MaxValue : x.LevelTime;
            var yTime = y.LevelTime <= 0 ? long.MaxValue : y.LevelTime;
            var timeCompare = xTime.CompareTo(yTime);
            return timeCompare != 0 ? timeCompare : string.CompareOrdinal(x.UserId, y.UserId);
        }
    }
}
