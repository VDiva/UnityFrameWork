using GameData;
using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace WebSocketDemo;

public static class MongoDBMrg
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserMutationLocks = new();

    static MongoDBMrg()
    {
        BsonSerializer.RegisterSerializer(new MongoUserDataSerializer());
        BsonSerializer.RegisterSerializer(new MongoEmailDataSerializer());
    }

    // 部署时默认连接同机 MongoDB；拆分部署可通过环境变量覆盖，无需重新编译。
    public static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("MONGODB_URI") ??
        _password;

    private const string DatabaseName = "GameData";
    private const string UserCollectionName = "UserData";
    private const string EmailCollectionName = "EmailData";
    private const string GlobalEmailCollectionName = "GlobalEmailData";
    private const string GlobalEmailReceiptCollectionName = "GlobalEmailReceipt";
    private const string RankCollectionName = "RankData";
    private const string ServerCollectionName = "ServerData";
    private const string GongGaoCollectionName = "GongGaoData";
    private const string FriendRelationCollectionName = "FriendRelation";

    // 所有数据库操作统一超时，避免 MongoDB 卡住时拖住游戏主流程。
    private static readonly TimeSpan MongoOperationTimeout = TimeSpan.FromSeconds(5);

    private static MongoClient? _mongoClient;
    private static IMongoDatabase? _database;

    public static bool IsConnected => _database != null;

    private static string _password = "";
    public static void Configure(IConfiguration configuration)
    {
        _password = ReadSetting(configuration, "Mongodb:URL", "MONGODB_URI");
    }
    
    private static string ReadSetting(IConfiguration configuration, string key, string envKey)
    {
        string? configured = configuration[key];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : Environment.GetEnvironmentVariable(envKey)?.Trim() ?? string.Empty;
    }
    
    
    /// <summary>
    /// 初始化 MongoDB 客户端、测试连接，并创建常用索引。
    /// </summary>
    public static void Connect()
    {
        try
        {
            var mongoUrl = MongoUrl.Create(ConnectionString);
            var settings = MongoClientSettings.FromUrl(mongoUrl);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            settings.SocketTimeout = TimeSpan.FromSeconds(10);
            settings.MaxConnectionPoolSize = 200;
            settings.MinConnectionPoolSize = 0;

            _mongoClient = new MongoClient(settings);
            _database = _mongoClient.GetDatabase(DatabaseName);
            _database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            EnsureIndexes();
            Console.WriteLine("MongoDB connected.");
        }
        catch (Exception ex)
        {
            _mongoClient = null;
            _database = null;
            Console.WriteLine($"MongoDB connect failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 按 MongoDB 主键 _id 查询一条数据，适合玩家、邮件等已知唯一 id 的查询。
    /// </summary>
    public static async Task<T?> FindByIdAsync<T>(string collectionName, string id)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        var filter = Builders<T>.Filter.Eq("_id", id);
        return await collection.Find(filter).FirstOrDefaultAsync(timeoutCts.Token);
    }

    /// <summary>
    /// 按条件查询多条数据，并限制最大返回数量，避免一次查询拉取过多数据。
    /// </summary>
    public static async Task<List<T>> FindManyAsync<T>(
        string collectionName,
        FilterDefinition<T> filter,
        int limit = 100)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        return await collection
            .Find(filter)
            .Limit(Math.Clamp(limit, 1, 1000))
            .ToListAsync(timeoutCts.Token);
    }

    /// <summary>
    /// 插入一条新数据。如果 _id 已存在，MongoDB 会抛出重复键异常。
    /// </summary>
    public static async Task InsertAsync<T>(string collectionName, T data)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        await collection.InsertOneAsync(data, cancellationToken: timeoutCts.Token);
    }

    /// <summary>
    /// 按 _id 整体替换一条数据。适合保存完整玩家快照，不适合只改某个字段。
    /// </summary>
    public static async Task<bool> ReplaceByIdAsync<T>(string collectionName, string id, T data)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        var filter = Builders<T>.Filter.Eq("_id", id);
        var result = await collection.ReplaceOneAsync(
            filter,
            data,
            new ReplaceOptions { IsUpsert = false },
            timeoutCts.Token);

        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// 按 _id 局部更新字段。推荐优先使用这个方法，减少数据库读写数据量。
    /// </summary>
    public static async Task<bool> UpdateByIdAsync<T>(
        string collectionName,
        string id,
        UpdateDefinition<T> update)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        var filter = Builders<T>.Filter.Eq("_id", id);
        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: timeoutCts.Token);
        return result.ModifiedCount > 0;
    }

    /// <summary>
    /// 按 _id 删除一条数据。
    /// </summary>
    public static async Task<bool> DeleteByIdAsync<T>(string collectionName, string id)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        var filter = Builders<T>.Filter.Eq("_id", id);
        var result = await collection.DeleteOneAsync(filter, timeoutCts.Token);
        return result.DeletedCount > 0;
    }
    
    /// <summary>
    /// 删除一个集合
    /// </summary>
    /// <param name="collectionName"></param>
    /// <param name="ids"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static async Task<bool> DeleteByIdsAsync<T>(string collectionName, string[] ids)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<T>(collectionName);
        var filter = Builders<T>.Filter.In("_id", ids);
        var result = await collection.DeleteManyAsync(filter, timeoutCts.Token);
        return result.DeletedCount > 0;
    }
    

    /// <summary>
    /// 创建玩家数据。一般由 GetUserAsCreate 调用，避免并发创建时重复插入。
    /// </summary>
    public static async Task<UserData> CreateUser(string userId, string serverId)
    {
        serverId = ServerScope.Normalize(serverId);
        var data = new UserData()
        {
            UserAndServerId = ServerScope.UserKey(serverId, userId),
            UserId = userId,
            ServerId = serverId,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Name = "猎渊者"+Random.Shared.NextInt64(999,9999999)
        };
        await InsertAsync(UserCollectionName, data);
        return data;
    }

    /// <summary>
    /// 获取玩家数据；如果玩家不存在则原子创建，并同时刷新登录时间。
    /// FindOneAndUpdate + Upsert 可以减少数据库往返，也能避免并发登录时重复创建。
    /// </summary>
    public static async Task<UserData> GetUserAsCreate(string userId, string serverId)
    {
        serverId = ServerScope.Normalize(serverId);
        var documentId = ServerScope.UserKey(serverId, userId);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<UserData>(UserCollectionName);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<UserData>.Filter.Eq(x => x.UserAndServerId, documentId);
        var update = Builders<UserData>.Update
            .SetOnInsert(x => x.UserAndServerId, documentId)
            .SetOnInsert(x => x.ServerId, serverId)
            .SetOnInsert(x => x.CreatedAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            .SetOnInsert(x => x.UserId, userId)
            .SetOnInsert(x=> x.Name,"猎渊者"+Random.Shared.NextInt64(999,9999999))
            .Set(x => x.LoginTime, timestamp);

        var options = new FindOneAndUpdateOptions<UserData>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var player = await collection.FindOneAndUpdateAsync(filter, update, options, timeoutCts.Token);
        return player;
    }

    /// <summary>
    /// 只查询玩家数据，不自动创建。适合查看玩家是否存在。
    /// </summary>
    public static async Task<UserData?> GetUser(string userId, string serverId)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<UserData>(UserCollectionName);
        var documentId = ServerScope.UserKey(serverId, userId);
        var filter = Builders<UserData>.Filter.Eq(x => x.UserAndServerId, documentId);
        return await collection.Find(filter).FirstOrDefaultAsync(timeoutCts.Token);
    }

    /// <summary>
    /// 替换玩家的 UserData 字段。保存前会强制同步玩家 id。
    /// </summary>
    public static async Task<bool> UpdateUserData(string userId, string serverId, UserData userData)
    {
        serverId = ServerScope.Normalize(serverId);
        userData.UserId = userId;
        userData.ServerId = serverId;
        userData.UserAndServerId = ServerScope.UserKey(serverId, userId);
        if (userData.CreatedAt <= 0)
        {
            userData.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<UserData>(UserCollectionName);
        var filter = Builders<UserData>.Filter.Eq(x => x.UserAndServerId, userData.UserAndServerId);
        var result = await collection.ReplaceOneAsync(
            filter,
            userData,
            new ReplaceOptions { IsUpsert = true },
            timeoutCts.Token);
        return result.IsAcknowledged;
    }

    /// <summary>
    /// 只更新玩家登录时间，比保存完整 UserData 更轻。
    /// </summary>
    public static async Task<bool> UpdateUserLoginTime(string userId, string serverId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var documentId = ServerScope.UserKey(serverId, userId);
        var update = Builders<UserData>.Update.Set(x => x.LoginTime, timestamp);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<UserData>(UserCollectionName);
        var filter = Builders<UserData>.Filter.Eq(x => x.UserAndServerId, documentId);
        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: timeoutCts.Token);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public static async Task<string> GetGongGao()
    {
        var collection=GetCollection<GongGaoData>(GongGaoCollectionName);
        var filter = Builders<GongGaoData>.Filter.Empty;
        
        // 使用 Find 方法查询，并用 ToListAsync() 将所有结果加载到内存
        var all=await collection.Find(filter).ToListAsync();
        string srt = "";
        all.Sort(((data, gaoData) =>-data.CreateTime.CompareTo(gaoData.CreateTime) ));
        for (int i = 0; i < MathF.Min(all.Count,10); i++)
        {
            srt += all[i].Info;
            srt += "\n\n\n";
        }
        return srt;
    }

    /// <summary>
    /// 添加公告
    /// </summary>
    /// <param name="gongGao"></param>
    public static async Task AddGongGao(string gongGao)
    {
        var collection=GetCollection<GongGaoData>(GongGaoCollectionName);
        var data = new GongGaoData()
        {
            Id = Guid.NewGuid().ToString(),
            Info = gongGao,
            CreateTime =  DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await collection.InsertOneAsync(data);
    }

    /// <summary>
    /// 删除玩家数据。真实上线前建议谨慎调用，必要时改成软删除。
    /// </summary>
    public static async Task<bool> DeleteUser(string userId, string serverId)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<UserData>(UserCollectionName);
        var documentId = ServerScope.UserKey(serverId, userId);
        var filter = Builders<UserData>.Filter.Eq(x => x.UserAndServerId, documentId);
        var result = await collection.DeleteOneAsync(filter, timeoutCts.Token);
        return result.DeletedCount > 0;
    }

    /// <summary>
    /// 更新职业类型
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    /// <param name="roleType"></param>
    public static async Task SetRoleType(string userId, string serverId, int roleType)
    {
        var collection=GetCollection<UserData>(UserCollectionName);
        // 2. 筛选：按 _id 定位
        var documentId = ServerScope.UserKey(serverId, userId);
        var filter = Builders<UserData>.Filter.Eq(data => data.UserAndServerId, documentId);

        // 3. 更新：将 Name 字段改为新值
        var update = Builders<UserData>.Update.Set(data =>data.RoleType,roleType);

        // 4. 执行更新
        await collection.UpdateOneAsync(filter, update);
    }

    /// <summary>更新玩家头像链接，并返回更新后的完整玩家数据。</summary>
    /// <param name="userId">玩家 ID。</param>
    /// <param name="serverId">玩家所在的逻辑区服 ID。</param>
    /// <param name="avatar">已经由业务层校验过的头像链接；空字符串表示清除头像。</param>
    /// <returns>更新后的玩家数据；玩家不存在时返回 <see langword="null"/>。</returns>
    public static async Task<UserData?> SetAvatar(string userId, string serverId, string avatar)
    {
        serverId = ServerScope.Normalize(serverId);
        var documentId = ServerScope.UserKey(serverId, userId);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<UserData>(UserCollectionName);
        var filter = Builders<UserData>.Filter.Eq(data => data.UserAndServerId, documentId);
        var update = Builders<UserData>.Update.Set(data => data.Avatar, avatar);
        var options = new FindOneAndUpdateOptions<UserData>
        {
            ReturnDocument = ReturnDocument.After,
            IsUpsert = false
        };
        return await collection.FindOneAndUpdateAsync(filter, update, options, timeoutCts.Token);
    }

    /// <summary>以幂等方式创建一条双向好友关系。</summary>
    /// <param name="userId">发起操作的玩家 ID。</param>
    /// <param name="friendUserId">目标好友玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>MongoDB 是否确认了写入操作。</returns>
    public static async Task<bool> AddFriend(string userId, string friendUserId, string serverId)
    {
        FriendRelationRecord relation = FriendRelationRecord.Create(userId, friendUserId, serverId);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<FriendRelationRecord>(FriendRelationCollectionName);
        var filter = Builders<FriendRelationRecord>.Filter.Eq(x => x.Id, relation.Id);
        var result = await collection.ReplaceOneAsync(
            filter, relation, new ReplaceOptions { IsUpsert = true }, timeoutCts.Token);
        return result.IsAcknowledged;
    }

    /// <summary>删除两个玩家之间的双向好友关系。</summary>
    /// <param name="userId">关系一方的玩家 ID。</param>
    /// <param name="friendUserId">关系另一方的玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>是否确实删除了一条关系记录。</returns>
    public static async Task<bool> DeleteFriend(string userId, string friendUserId, string serverId)
    {
        FriendRelationRecord relation = FriendRelationRecord.Create(userId, friendUserId, serverId);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<FriendRelationRecord>(FriendRelationCollectionName);
        var result = await collection.DeleteOneAsync(
            Builders<FriendRelationRecord>.Filter.Eq(x => x.Id, relation.Id), timeoutCts.Token);
        return result.DeletedCount > 0;
    }

    /// <summary>判断两个同区服玩家之间是否存在好友关系。</summary>
    /// <param name="userId">关系一方的玩家 ID。</param>
    /// <param name="friendUserId">关系另一方的玩家 ID。</param>
    /// <param name="serverId">双方所在的逻辑区服 ID。</param>
    /// <returns>存在好友关系时为 <see langword="true"/>。</returns>
    public static async Task<bool> AreFriends(string userId, string friendUserId, string serverId)
    {
        FriendRelationRecord relation = FriendRelationRecord.Create(userId, friendUserId, serverId);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<FriendRelationRecord>(FriendRelationCollectionName);
        FriendRelationRecord? existing = await collection.Find(
                Builders<FriendRelationRecord>.Filter.Eq(x => x.Id, relation.Id))
            .Limit(1)
            .FirstOrDefaultAsync(timeoutCts.Token);
        return existing != null;
    }

    /// <summary>获取指定玩家的好友 ID，按建立关系的时间从早到晚返回。</summary>
    /// <param name="userId">需要查询的玩家 ID。</param>
    /// <param name="serverId">玩家所在的逻辑区服 ID。</param>
    /// <param name="limit">最大返回数量，最终会限制在 1 到 500 之间。</param>
    /// <returns>去重后的好友玩家 ID 列表。</returns>
    public static async Task<List<string>> GetFriendUserIds(
        string userId, string serverId, int limit = 200)
    {
        serverId = ServerScope.Normalize(serverId);
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<FriendRelationRecord>(FriendRelationCollectionName);
        var filter = Builders<FriendRelationRecord>.Filter.Eq(x => x.ServerId, serverId) &
                     (Builders<FriendRelationRecord>.Filter.Eq(x => x.UserAId, userId) |
                      Builders<FriendRelationRecord>.Filter.Eq(x => x.UserBId, userId));
        var relations = await collection.Find(filter)
            .SortBy(x => x.CreatedAt)
            .Limit(Math.Clamp(limit, 1, 500))
            .ToListAsync(timeoutCts.Token);
        return relations
            .Select(x => x.UserAId == userId ? x.UserBId : x.UserAId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 新增一封邮件。如果未传邮件 id，会自动生成 ObjectId 字符串。
    /// </summary>
    public static async Task AddEmail(EmailData email, string serverId)
    {
        email.ServerId = ServerScope.Normalize(serverId);
        if (string.IsNullOrWhiteSpace(email.Id))
        {
            email.Id = ObjectId.GenerateNewId().ToString();
        }

        await InsertAsync(EmailCollectionName, email);
    }

    /// <summary>
    /// 查询玩家邮件。UserId 已建立索引，适合高频按玩家拉取邮件列表。
    /// </summary>
    public static async Task<List<EmailData>> GetEmail(string userId, string serverId, int limit = 50)
    {
        var filter = Builders<EmailData>.Filter.And(
            Builders<EmailData>.Filter.Eq(x => x.UserId, userId),
            Builders<EmailData>.Filter.Eq(x => x.ServerId, ServerScope.Normalize(serverId)));
        return await FindManyAsync(EmailCollectionName, filter, limit);
    }

    public static async Task<List<EmailData>> GetAvailableGlobalEmails(
        string userId, string serverId, int limit = 50)
    {
        serverId = ServerScope.Normalize(serverId);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var globalCollection = GetCollection<GlobalEmailRecord>(GlobalEmailCollectionName);
        var receiptCollection = GetCollection<GlobalEmailReceipt>(GlobalEmailReceiptCollectionName);
        using var timeoutCts = CreateTimeoutToken();

        var filter = Builders<GlobalEmailRecord>.Filter.And(
            Builders<GlobalEmailRecord>.Filter.Eq(x => x.Enabled, true),
            Builders<GlobalEmailRecord>.Filter.Lte(x => x.StartTime, now),
            Builders<GlobalEmailRecord>.Filter.Or(
                Builders<GlobalEmailRecord>.Filter.Eq(x => x.ExpireTime, 0),
                Builders<GlobalEmailRecord>.Filter.Gt(x => x.ExpireTime, now)),
            Builders<GlobalEmailRecord>.Filter.Or(
                Builders<GlobalEmailRecord>.Filter.Eq(x => x.Scope, "all"),
                Builders<GlobalEmailRecord>.Filter.And(
                    Builders<GlobalEmailRecord>.Filter.Eq(x => x.Scope, "server"),
                    Builders<GlobalEmailRecord>.Filter.Eq(x => x.ServerId, serverId))));

        var records = await globalCollection.Find(filter)
            .SortByDescending(x => x.CreateTime)
            .Limit(Math.Clamp(limit, 1, 1000))
            .ToListAsync(timeoutCts.Token);
        if (records.Count == 0) return new List<EmailData>();

        var receiptIds = records.Select(x => GlobalReceiptId(serverId, userId, x.Id)).ToArray();
        var received = await receiptCollection.Find(
                Builders<GlobalEmailReceipt>.Filter.In(x => x.Id, receiptIds))
            .Project(x => x.EmailId)
            .ToListAsync(timeoutCts.Token);
        var receivedSet = received.ToHashSet(StringComparer.Ordinal);

        return records.Where(x => !receivedSet.Contains(x.Id)).Select(x =>
        {
            var email = new EmailData
            {
                Id = "global:" + x.Id,
                UserId = userId,
                ServerId = serverId,
                EmailTitle = x.EmailTitle,
                EmailInfo = x.EmailInfo,
                CreateTime = x.CreateTime,
                State = 0
            };
            email.ItemDic.Add(x.ItemDic);
            return email;
        }).ToList();
    }

    public static async Task<List<EmailData>> ClaimGlobalEmails(
        string userId, string serverId, IEnumerable<string> ids)
    {
        serverId = ServerScope.Normalize(serverId);
        var wanted = ids.Where(x => x.StartsWith("global:", StringComparison.Ordinal))
            .Select(x => x[7..]).Distinct(StringComparer.Ordinal).Take(1000).ToArray();
        var result = new List<EmailData>();
        foreach (string id in wanted)
        {
            var available = await GetAvailableGlobalEmails(userId, serverId, 1000);
            var email = available.FirstOrDefault(x => x.Id == "global:" + id);
            if (email == null) continue;
            try
            {
                await GetCollection<GlobalEmailReceipt>(GlobalEmailReceiptCollectionName).InsertOneAsync(
                    new GlobalEmailReceipt
                    {
                        Id = GlobalReceiptId(serverId, userId, id), EmailId = id,
                        UserId = userId, ServerId = serverId, State = 2
                    });
                result.Add(email);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey) { }
        }
        return result;
    }

    public static async Task CompleteGlobalEmailClaims(
        string userId, string serverId, IEnumerable<string> ids, bool success)
    {
        var globalIds = ids.Where(x => x.StartsWith("global:", StringComparison.Ordinal))
            .Select(x => x[7..]).Distinct(StringComparer.Ordinal).ToArray();
        if (globalIds.Length == 0) return;
        var receiptIds = globalIds.Select(x => GlobalReceiptId(serverId, userId, x)).ToArray();
        var collection = GetCollection<GlobalEmailReceipt>(GlobalEmailReceiptCollectionName);
        var filter = Builders<GlobalEmailReceipt>.Filter.In(x => x.Id, receiptIds);
        if (success)
            await collection.UpdateManyAsync(filter, Builders<GlobalEmailReceipt>.Update
                .Set(x => x.State, 1).Set(x => x.ClaimTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        else
            await collection.DeleteManyAsync(filter);
    }

    private static string GlobalReceiptId(string serverId, string userId, string emailId) =>
        $"{ServerScope.Normalize(serverId)}:{userId}:{emailId}";

    /// <summary>
    /// 更新邮件状态，例如未领取、已领取、过期。
    /// </summary>
    public static async Task<bool> UpdateEmailState(string emailId, int state)
    {
        var update = Builders<EmailData>.Update.Set(x => x.State, state);
        return await UpdateByIdAsync(EmailCollectionName, emailId, update);
    }

    /// <summary>
    /// 删除邮件数据。
    /// </summary>
    public static async Task<bool> DeleteEmail(string emailId)
    {
        return await DeleteByIdAsync<EmailData>(EmailCollectionName, emailId);
    }
    
    public static async Task<bool> DeleteEmails(string[] emailId)
    {
        return await DeleteByIdsAsync<EmailData>(EmailCollectionName, emailId);
    }

    /// <summary>
    /// 持久化排行榜数据。Redis 负责快速读写，MongoDB 负责长期保存和恢复。
    /// </summary>
    public static async Task<bool> UpsertRankData(string rankName, string serverId, RankData rankData)
    {
        serverId = ServerScope.Normalize(serverId);
        using var timeoutCts = CreateTimeoutToken();
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

        var fightingCapacity = NormalizeNumericString(rankData.UserData.FightingCapacity);
        var data = rankData.Clone();
        data.Id = GetRankDocumentId(serverId, rankName, userId);
        data.ServerId = serverId;
        data.RankName = rankName;
        data.UserId = userId;
        data.FightingCapacityLength = fightingCapacity.Length;
        data.FightingCapacity = fightingCapacity;
        data.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var collection = GetCollection<RankData>(RankCollectionName);
        var filter = Builders<RankData>.Filter.Eq(x => x.Id, data.Id);
        var existing = await collection.Find(filter).FirstOrDefaultAsync(timeoutCts.Token);
        data.Level = existing != null
            ? rankData.Level > existing.Level ? rankData.Level : existing.Level
            : rankData.Level;
        data.LevelTime = existing != null && data.Level <= existing.Level && existing.LevelTime > 0
            ? existing.LevelTime
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var result = await collection.ReplaceOneAsync(
            filter,
            data,
            new ReplaceOptions { IsUpsert = true },
            timeoutCts.Token);

        return result.IsAcknowledged;
    }

    /// <summary>
    /// 从 MongoDB 获取排行榜。用于 Redis 不可用或 Redis 数据丢失后的兜底读取。
    /// </summary>
    public static async Task<List<RankData>> GetRankData(string rankName, string serverId, int limit = 100)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<RankData>(RankCollectionName);
        var filter = Builders<RankData>.Filter.And(
            Builders<RankData>.Filter.Eq(x => x.ServerId, ServerScope.Normalize(serverId)),
            Builders<RankData>.Filter.Eq(x => x.RankName, rankName));
        var sort = Builders<RankData>.Sort
            .Descending(x => x.Level)
            .Ascending(x => x.LevelTime)
            .Ascending(x => x.UserId);

        var list = await collection
            .Find(filter)
            .Sort(sort)
            .Limit(Math.Clamp(limit, 1, 1000))
            .ToListAsync(timeoutCts.Token);
        return list.Select(data =>
        {
            var rank = data.Clone();
            rank.Id = rank.UserId;
            return rank;
        }).ToList();
    }

    /// <summary>
    /// 获取所有排行榜持久化记录。每周发奖前使用。
    /// </summary>
    public static async Task<List<RankData>> GetAllRankData(int limit = 100000)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<RankData>(RankCollectionName);
        var sort = Builders<RankData>.Sort
            .Ascending(x => x.RankName)
            .Descending(x => x.Level)
            .Ascending(x => x.LevelTime)
            .Ascending(x => x.UserId);

        return await collection
            .Find(Builders<RankData>.Filter.Empty)
            .Sort(sort)
            .Limit(Math.Clamp(limit, 1, 200000))
            .ToListAsync(timeoutCts.Token);
    }

    /// <summary>
    /// 清除指定排行榜的 MongoDB 持久化数据。
    /// </summary>
    public static async Task<long> ClearRankData(string rankName, string serverId)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<RankData>(RankCollectionName);
        var filter = Builders<RankData>.Filter.And(
            Builders<RankData>.Filter.Eq(x => x.ServerId, ServerScope.Normalize(serverId)),
            Builders<RankData>.Filter.Eq(x => x.RankName, rankName));
        var result = await collection.DeleteManyAsync(filter, timeoutCts.Token);
        return result.DeletedCount;
    }

    /// <summary>
    /// 清除 MongoDB 中所有排行榜持久化数据。
    /// </summary>
    public static async Task<long> ClearAllRankData()
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<RankData>(RankCollectionName);
        var result = await collection.DeleteManyAsync(Builders<RankData>.Filter.Empty, timeoutCts.Token);
        return result.DeletedCount;
    }

    /// <summary>
    /// 创建查询常用索引。重复调用是安全的，MongoDB 已存在索引时不会重复创建。
    /// </summary>
    private static void EnsureIndexes()
    {
        var database = GetDatabase();

        var userCollection = database.GetCollection<UserData>(UserCollectionName);
        userCollection.Indexes.CreateOne(new CreateIndexModel<UserData>(
            Builders<UserData>.IndexKeys.Ascending(x => x.UserAndServerId),
            new CreateIndexOptions { Unique = true }));

        var serverCollection = database.GetCollection<ServerData>(ServerCollectionName);
        serverCollection.Indexes.CreateOne(new CreateIndexModel<ServerData>(
            Builders<ServerData>.IndexKeys.Ascending(x => x.ServerId),
            new CreateIndexOptions { Unique = true }));

        var emailCollection = database.GetCollection<EmailData>(EmailCollectionName);
        emailCollection.Indexes.CreateOne(new CreateIndexModel<EmailData>(
            Builders<EmailData>.IndexKeys.Ascending(x => x.ServerId).Ascending(x => x.UserId)));
        emailCollection.Indexes.CreateOne(new CreateIndexModel<EmailData>(
            Builders<EmailData>.IndexKeys.Ascending(x => x.State)));

        var rankCollection = database.GetCollection<RankData>(RankCollectionName);
        rankCollection.Indexes.CreateOne(new CreateIndexModel<RankData>(
            Builders<RankData>.IndexKeys
                .Ascending(x => x.RankName)
                .Ascending(x => x.ServerId)
                .Descending(x => x.Level)
                .Ascending(x => x.LevelTime)
                .Ascending(x => x.UserId)));

        var friendCollection = database.GetCollection<FriendRelationRecord>(FriendRelationCollectionName);
        friendCollection.Indexes.CreateOne(new CreateIndexModel<FriendRelationRecord>(
            Builders<FriendRelationRecord>.IndexKeys
                .Ascending(x => x.ServerId)
                .Ascending(x => x.UserAId)));
        friendCollection.Indexes.CreateOne(new CreateIndexModel<FriendRelationRecord>(
            Builders<FriendRelationRecord>.IndexKeys
                .Ascending(x => x.ServerId)
                .Ascending(x => x.UserBId)));

    }

    /// <summary>
    /// 获取集合对象，统一检查数据库是否已经连接。
    /// </summary>
    private static IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return GetDatabase().GetCollection<T>(collectionName);
    }

    /// <summary>
    /// 为每次数据库操作创建独立超时 token。
    /// </summary>
    private static CancellationTokenSource CreateTimeoutToken()
    {
        return new CancellationTokenSource(MongoOperationTimeout);
    }

    /// <summary>
    /// 获取数据库实例。未连接时抛出明确异常，方便上层捕获和日志定位。
    /// </summary>
    private static IMongoDatabase GetDatabase()
    {
        return _database ?? throw new InvalidOperationException("MongoDB is not connected.");
    }

    private static string GetRankDocumentId(string serverId, string rankName, string userId)
    {
        return $"{ServerScope.Normalize(serverId)}:{rankName}:{userId}";
    }

    private static string NormalizeNumericString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0";
        }

        var digits = new string(value.Where(char.IsDigit).ToArray()).TrimStart('0');
        return digits.Length == 0 ? "0" : digits;
    }

    public static async Task<List<ServerData>> GetServers()
    {
        var serverData=GetCollection<ServerData>(ServerCollectionName);
        var filter = Builders<ServerData>.Filter.Empty;
        var allDocuments = await serverData.Find(filter).ToListAsync();
        return allDocuments.ToList();
    }

    public static async Task AddServer(ServerData serverData)
    {
        serverData.ServerId = ServerScope.Normalize(serverData.ServerId);
        serverData.Id = serverData.ServerId;
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<ServerData>(ServerCollectionName);
        var filter = Builders<ServerData>.Filter.Eq(x => x.Id, serverData.Id);
        await collection.ReplaceOneAsync(
            filter,
            serverData,
            new ReplaceOptions { IsUpsert = true },
            timeoutCts.Token);
        Console.WriteLine($"添加服务器成功-----{serverData.ServerName}----{serverData.ServerId}");
    }

    /// <summary>
    /// 添加装备
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    /// <param name="equips"></param>
    public static async Task AddEquips(string userId,string serverId,List<EquipData> equips)
    {
        if (equips == null || equips.Count == 0)
            return;

        await MutateUserData(userId, serverId, data => data.EquipData.AddRange(equips));
    }


    /// <summary>
    /// 添加邮件
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    /// <param name="emails"></param>
    public static async Task AddEmails(string userId, string serverId, List<EmailData> emails)
    {
        serverId = ServerScope.Normalize(serverId);
        foreach (var email in emails)
        {
            email.UserId = userId;
            email.ServerId = serverId;
            if (string.IsNullOrWhiteSpace(email.Id))
            {
                email.Id = ObjectId.GenerateNewId().ToString();
            }
        }

        if (emails.Count == 0)
        {
            return;
        }

        var collection = GetCollection<EmailData>(EmailCollectionName);
        using var timeoutCts = CreateTimeoutToken();
        await collection.InsertManyAsync(emails,cancellationToken: timeoutCts.Token);
    }

    /// <summary>
    /// 更新邮件状态
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    /// <param name="ids"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static async Task<bool> UpdateEmailsState(string[] ids,int state)
    {
        using var timeoutCts = CreateTimeoutToken();
        var collection = GetCollection<EmailData>(EmailCollectionName);
    
        // 过滤出所有匹配的ID
        var filter = Builders<EmailData>.Filter.In("_id", ids);
    
        // 设置要更新的字段值
        var update = Builders<EmailData>.Update.Set(data =>data.State , state);
    
        // 批量更新
        var result = await collection.UpdateManyAsync(filter, update, cancellationToken: timeoutCts.Token);
    
        return result.ModifiedCount>0;
    }

    /// <summary>
    /// 原子抢占仍处于指定状态、且确实属于当前玩家的邮件。
    /// 多个领取请求并发到达时，同一封邮件只会被一个请求成功抢占。
    /// </summary>
    public static async Task<List<EmailData>> ClaimEmails(
        string userId,
        string serverId,
        IEnumerable<string> ids,
        int expectedState,
        int claimedState)
    {
        serverId = ServerScope.Normalize(serverId);
        var result = new List<EmailData>();
        var distinctIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(1000);
        var collection = GetCollection<EmailData>(EmailCollectionName);

        foreach (var id in distinctIds)
        {
            using var timeoutCts = CreateTimeoutToken();
            var filter = Builders<EmailData>.Filter.And(
                Builders<EmailData>.Filter.Eq("_id", id),
                Builders<EmailData>.Filter.Eq(x => x.UserId, userId),
                Builders<EmailData>.Filter.Eq(x => x.ServerId, serverId),
                Builders<EmailData>.Filter.Eq(x => x.State, expectedState));
            var update = Builders<EmailData>.Update.Set(x => x.State, claimedState);
            var claimed = await collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<EmailData>
                {
                    ReturnDocument = ReturnDocument.Before
                },
                timeoutCts.Token);

            if (claimed != null)
            {
                result.Add(claimed);
            }
        }

        return result;
    }
    
    /// <summary>
    /// 添加道具
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    /// <param name="dic"></param>
    public static async Task AddItems(string userId, string serverId, Dictionary<string, long> dic)
    {
        if (dic == null || dic.Count == 0)
            return;

        await MutateUserData(userId, serverId, data =>
        {
            foreach (var item in dic)
            {
                var key = item.Key.StartsWith("Item.", StringComparison.Ordinal)
                    ? item.Key[5..]
                    : item.Key;
                data.Item[key] = data.Item.GetValueOrDefault(key, 0) + item.Value;
            }
        });
    }
    
    
    /// <summary>
    /// 添加字典数据
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="serverId"></param>
    /// <param name="dic"></param>
    public static async Task AddUserDataDic(string userId, string serverId, Dictionary<string, string> dic)
    {
        if (dic == null || dic.Count == 0)
            return;

        await MutateUserData(userId, serverId, data =>
        {
            foreach (var item in dic)
                data.DataDic[item.Key] = item.Value;
        });
    }

    /// <summary>
    /// UserData 由 MongoUserDataSerializer 整体存入 _protobuf，所有 map/repeated 修改必须回写整个消息。
    /// 同一玩家串行化可避免同时拾取多个掉落物时相互覆盖。
    /// </summary>
    private static async Task MutateUserData(string userId, string serverId, Action<UserData> mutation)
    {
        serverId = ServerScope.Normalize(serverId);
        var documentId = ServerScope.UserKey(serverId, userId);
        var mutationLock = UserMutationLocks.GetOrAdd(documentId, _ => new SemaphoreSlim(1, 1));
        await mutationLock.WaitAsync();
        try
        {
            var data = await GetUserAsCreate(userId, serverId);
            mutation(data);
            if (!await UpdateUserData(userId, serverId, data))
                throw new InvalidOperationException($"Failed to save user data: {documentId}");
        }
        finally
        {
            mutationLock.Release();
        }
    }
}
