using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading.Channels;
using GameData;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using WebSocketDemo;

static class Program
{
    static int checks;
    static TaskCompletionSource<bool> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    static void Check(bool condition,string message) {if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
    static async Task Until(Func<bool> predicate)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(!predicate())await Task.Delay(5,deadline.Token);
    }
    static Msg[] Messages(TestSocket socket,ServerMsgType type)=>socket.Sent.Select(Msg.Parser.ParseFrom).Where(m=>m.ServerMsgType==type).ToArray();
    static void Set(PlayerSession player,string field,object value)=>typeof(PlayerSession).GetProperty(field)!.SetValue(player,value);

    static async Task Main()
    {
        ReplicationSettings.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Networking:RealtimeSendRate","0"}}).Build());
        Check(ReplicationSettings.RealtimeInterval==TimeSpan.Zero,"rate 0 preserves immediate sending");
        ReplicationSettings.Configure(new ConfigurationBuilder().Build());
        Check(ReplicationSettings.RealtimeSendRate==30,"default realtime send window is 30 Hz");
        bool invalid=false;try{ReplicationSettings.Configure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Networking:RealtimeSendRate","1"}}).Build());}catch(InvalidOperationException){invalid=true;}
        Check(invalid,"unsupported rate rejected instead of silently expiring snapshots");

        var sent=new ConcurrentQueue<byte>();var finished=Signal();int encoded=0;
        var queue=new SessionSendQueue((data,_,_)=>{sent.Enqueue(data[0]);finished.TrySetResult(true);return Task.CompletedTask;},e=>finished.TrySetException(e),realtimeInterval:TimeSpan.FromMilliseconds(150));
        for(int i=0;i<100;i++)queue.Enqueue(new ProbePayload((byte)i,()=>Interlocked.Increment(ref encoded)),WebSocketMessageType.Binary,1);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(sent.SequenceEqual(new byte[]{99})&&encoded==1&&queue.CoalescedPackets==99,"100 pending states coalesce before encoding");
        queue.Stop();await queue.Completion;

        var inputMessage=new Msg { ServerMsgType=ServerMsgType.NetworkAnimationUpdate,
            NetworkAnimation=new NetworkAnimationData { ObjectId=7, Sequence=1, AnimationName="original" } };
        var immutablePacket=new DeferredMessagePacket(inputMessage);
        inputMessage.NetworkAnimation.AnimationName="mutated";
        Check(!immutablePacket.IsEncoded,"deferred message does not encode at construction");
        var sharedBuffers=await Task.WhenAll(Enumerable.Range(0,32).Select(_=>Task.Run(immutablePacket.GetBytes)));
        Check(sharedBuffers.All(x=>ReferenceEquals(x,sharedBuffers[0])) &&
            Msg.Parser.ParseFrom(sharedBuffers[0]).NetworkAnimation.AnimationName=="original" &&
            immutablePacket.ByteCount==sharedBuffers[0].Length,"concurrent receivers share immutable encoding and exact byte budget");
        var packets=Enumerable.Range(0,100).Select(i=>new DeferredMessagePacket(new Msg {
            ServerMsgType=ServerMsgType.NetworkTransformUpdate,
            NetworkTransform=new NetworkTransformData { ObjectId=7, Sequence=(ulong)i } })).ToArray();
        var encodedDone=Signal();
        queue=new SessionSendQueue((data,_,_)=>{encodedDone.TrySetResult(true);return Task.CompletedTask;},
            e=>encodedDone.TrySetException(e),realtimeInterval:TimeSpan.FromMilliseconds(150));
        foreach(var packet in packets)queue.Enqueue(packet,WebSocketMessageType.Binary,7);
        await encodedDone.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(packets.Count(x=>x.IsEncoded)==1 && packets[^1].IsEncoded,"100 real protobuf snapshots require only one encoding after coalescing");
        queue.Stop();await queue.Completion;

        sent.Clear();var two=Signal();
        queue=new SessionSendQueue((data,_,_)=>{sent.Enqueue(data[0]);if(sent.Count==2)two.TrySetResult(true);return Task.CompletedTask;},e=>two.TrySetException(e),realtimeInterval:TimeSpan.FromMilliseconds(150));
        queue.Enqueue(new byte[]{1},WebSocketMessageType.Binary,1);
        queue.Enqueue(new byte[]{2},WebSocketMessageType.Binary,2);
        await two.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(sent.SequenceEqual(new byte[]{1,2}),"different objects share the same send window");
        queue.Stop();await queue.Completion;

        sent.Clear();two=Signal();
        queue=new SessionSendQueue((data,_,_)=>{sent.Enqueue(data[0]);if(sent.Count==2)two.TrySetResult(true);return Task.CompletedTask;},e=>two.TrySetException(e),realtimeInterval:TimeSpan.FromSeconds(5));
        queue.Enqueue(new byte[]{1},WebSocketMessageType.Binary,1);
        await Task.Delay(20);
        queue.Enqueue(new byte[]{9},WebSocketMessageType.Binary);
        await two.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(sent.SequenceEqual(new byte[]{1,9}),"reliable control wakes delayed realtime and preserves FIFO");
        queue.Stop();await queue.Completion;

        encoded=0;sent.Clear();finished=Signal();
        queue=new SessionSendQueue((data,_,_)=>{sent.Enqueue(data[0]);finished.TrySetResult(true);return Task.CompletedTask;},e=>finished.TrySetException(e),realtimeInterval:TimeSpan.FromMilliseconds(70));
        queue.Enqueue(new ProbePayload(1,()=>encoded++),WebSocketMessageType.Binary,1,maxAgeMs:1);
        await Task.Delay(100);
        queue.Enqueue(new byte[]{9},WebSocketMessageType.Binary);
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(encoded==0&&queue.ExpiredRealtimePackets==1&&sent.SequenceEqual(new byte[]{9}),"expired payload discarded without encoding");
        queue.Stop();await queue.Completion;
        encoded=0;
        queue=new SessionSendQueue((_,_,_)=>Task.CompletedTask,e=>throw new Exception("unexpected failure",e),realtimeInterval:TimeSpan.FromSeconds(5));
        queue.Enqueue(new ProbePayload(1,()=>encoded++),WebSocketMessageType.Binary,1);
        queue.Stop();await queue.Completion;
        Check(encoded==0&&queue.PendingBytes==0,"shutdown cancels scheduled flush without retaining payloads");

        var a=new NetworkTransformData{ObjectId=10,Sequence=1,PositionX=1};
        var b=new NetworkTransformData{ObjectId=11,Sequence=1,PositionX=2};
        var original=new TransformBatchPacket(new[]{a,b});a.PositionX=999;
        var replacement=new TransformBatchPacket(new[]{new NetworkTransformData{ObjectId=10,Sequence=2,PositionX=3}});
        var merged=(TransformBatchPacket)replacement.MergeFrom(original);
        bool IsEncoded(TransformBatchPacket packet)=>((Lazy<byte[]>)typeof(TransformBatchPacket).GetField("_bytes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(packet)!).IsValueCreated;
        Check(!IsEncoded(original)&&!IsEncoded(replacement)&&!IsEncoded(merged),"typed batch merge never encodes superseded snapshots");
        var decoded=Msg.Parser.ParseFrom(merged.GetBytes());
        Check(decoded.NetworkTransforms.Count==2&&decoded.NetworkTransforms.Single(t=>t.ObjectId==10).PositionX==3&&decoded.NetworkTransforms.Single(t=>t.ObjectId==11).PositionX==2,"typed merge retains latest state of every object");
        Check(Msg.Parser.ParseFrom(original.GetBytes()).NetworkTransforms[0].PositionX==1,"broadcast captures immutable snapshot ownership");
        Check(merged.ByteCount==merged.GetBytes().Length,"deferred batch byte budget equals actual wire size");
        var parallel=await Task.WhenAll(Task.Run(merged.GetBytes),Task.Run(merged.GetBytes));
        Check(ReferenceEquals(parallel[0],parallel[1]),"recipients share one encoded buffer");

        using (var blockedSession = new PlayerSession("deferred-batch-barrier", new TestSocket { BlockMarker = true }))
        {
            var socket = (TestSocket)typeof(PlayerSession).GetField("_webSocket",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(blockedSession)!;
            await blockedSession.SendBinaryAsync(new byte[] { 0 });
            await socket.MarkerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            const ulong batchKey = 0x40000000UL;
            blockedSession.QueueLatestTransformBatch(batchKey, new TransformBatchPacket(new[]
            {
                new NetworkTransformData { ObjectId=10, PositionX=1 },
                new NetworkTransformData { ObjectId=11, PositionX=2 }
            }));
            blockedSession.QueueLatestTransformBatch(batchKey, new TransformBatchPacket(new[] { new NetworkTransformData { ObjectId=10, PositionX=3 } }));
            await blockedSession.SendBinaryAsync(new byte[] { 9 });
            blockedSession.QueueLatestTransformBatch(batchKey, new TransformBatchPacket(new[] { new NetworkTransformData { ObjectId=10, PositionX=4 } }));
            socket.ReleaseMarker.TrySetResult(true);
            await Until(()=>socket.Sent.Count==4);
            var frames=socket.Sent.ToArray();
            Check(frames[0].SequenceEqual(new byte[]{0})&&frames[2].SequenceEqual(new byte[]{9})&&
                Msg.Parser.ParseFrom(frames[1]).NetworkTransforms.Count==2&&
                Msg.Parser.ParseFrom(frames[1]).NetworkTransforms.Single(t=>t.ObjectId==10).PositionX==3&&
                Msg.Parser.ParseFrom(frames[3]).NetworkTransforms.Single().PositionX==4,
                "deferred transform merge preserves objects and cannot cross lifecycle control barrier");
        }

        var spatial=new SpatialInterestIndex<int>(40);
        for(int i=0;i<10000;i++)spatial.Update(i,"server-a",i*200,0,0);
        spatial.Update(10001,"server-b",0,0,0);
        var nearby=new HashSet<int>();spatial.CollectNearby("server-a",0,0,0,nearby);
        Check(nearby.SetEquals(new[]{0}),"spatial candidates exclude 9999 distant players and other logical server");
        spatial.Update(3,"server-a",-0.1f,0,0);nearby.Clear();spatial.CollectNearby("server-a",0.1f,0,0,nearby);
        Check(nearby.Contains(3),"negative cell boundary remains visible");
        spatial.Update(3,"server-b",0,0,0);nearby.Clear();spatial.CollectNearby("server-a",0,0,0,nearby);
        Check(!nearby.Contains(3),"moving scope removes old spatial membership");
        spatial.Remove(3);spatial.Remove(3);Check(spatial.Count==10000,"spatial cleanup is idempotent");
        spatial.Update(3,"extreme",float.MaxValue,float.MinValue,0);nearby.Clear();spatial.CollectNearby("extreme",float.MaxValue,float.MinValue,0,nearby);
        Check(nearby.Contains(3),"finite extreme coordinates do not overflow neighbor iteration");
        spatial.Clear();Check(spatial.Count==0&&spatial.CellCount==0,"spatial clear releases cells");

        var manager=PlayerSessionManager.Instance;var players=new List<PlayerSession>();
        PlayerSession Player(string name,string server,out TestSocket socket)
        {
            socket=new TestSocket();var player=new PlayerSession(name,socket);players.Add(player);
            manager.TryAddSession(name,player);Set(player,nameof(PlayerSession.UserId),name);Set(player,nameof(PlayerSession.ServerId),server);
            manager.EnsureNetworkObjectId(player);Set(player,nameof(PlayerSession.NetworkRoomId),NetworkRoomManager.LobbyRoomId);
            return player;
        }
        try
        {
            var first=Player("aoi-first","a",out var firstSocket);
            var second=Player("aoi-second","a",out var secondSocket);
            var far=Player("aoi-far","a",out var farSocket);
            var otherScope=Player("aoi-other","b",out var otherSocket);
            var invalidTransform = new NetworkTransformData { HasScale=true, ScaleX=float.NaN };
            Check(!NetworkStateValidation.IsValid(invalidTransform), "nonfinite scale rejected");
            invalidTransform.HasScale=false;
            invalidTransform.Children.Add(new NetworkChildTransformData { PositionX=float.PositiveInfinity });
            Check(!NetworkStateValidation.IsValid(invalidTransform), "nonfinite child rejected");
            invalidTransform.Children.Clear();
            for(int i=0;i<=NetworkStateValidation.MaxChildren;i++) invalidTransform.Children.Add(new NetworkChildTransformData());
            Check(!NetworkStateValidation.IsValid(invalidTransform), "child count bounded");
            Check(!NetworkStateValidation.IsValid(new NetworkAnimationData { TrackIndex=32 }), "track count bounded");
            await manager.RelayNetworkAnimationAsync(first,new NetworkAnimationData { ObjectId=first.NetworkObjectId, TrackIndex=0, Sequence=2 });
            await manager.RelayNetworkAnimationAsync(first,new NetworkAnimationData { ObjectId=first.NetworkObjectId, TrackIndex=1, Sequence=2, AnimationName="overlay" });
            await manager.RelayNetworkAnimationAsync(first,new NetworkAnimationData { ObjectId=first.NetworkObjectId, TrackIndex=1, Sequence=1, AnimationName="stale" });
            var animations=(ConcurrentDictionary<int,NetworkAnimationData>)typeof(PlayerSession).GetProperty("NetworkAnimations",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(first)!;
            Check(animations.Count==2 && animations[1].AnimationName=="overlay", "tracks retained and stale sequence ignored");
            var sync=NetworkSyncVarManager.Instance;
            for(uint i=0;i<NetworkSyncVarManager.MaxFieldsPerObject+5;i++)
                await sync.RelayAsync(first,new NetworkSyncVarData { ObjectId=first.NetworkObjectId, FieldId=i, Sequence=1 });
            var states=new List<NetworkSyncVarData>();sync.AppendObjectStates(first.NetworkRoomId,first.NetworkObjectId,states);
            Check(states.Count==NetworkSyncVarManager.MaxFieldsPerObject,"sync field count bounded");
            await sync.RelayAsync(first,new NetworkSyncVarData { ObjectId=first.NetworkObjectId, FieldId=0, Sequence=2, Value=ByteString.CopyFromUtf8("updated") });
            states.Clear();sync.AppendObjectStates(first.NetworkRoomId,first.NetworkObjectId,states);
            Check(states.Single(x=>x.FieldId==0).Value.ToStringUtf8()=="updated","field replacement accepted at count cap");
            sync.RemoveObject(first.NetworkObjectId);
            for(uint i=0;i<8;i++) await sync.RelayAsync(first,new NetworkSyncVarData { ObjectId=first.NetworkObjectId, FieldId=i, Sequence=1, Value=ByteString.CopyFrom(new byte[64*1024]) });
            states.Clear();sync.AppendObjectStates(first.NetworkRoomId,first.NetworkObjectId,states);
            Check(states.Count==3 && states.Sum(x=>x.CalculateSize())<=NetworkSyncVarManager.MaxObjectBytes,"sync serialized byte budget enforced");
            sync.RemoveObject(first.NetworkObjectId);
            Task Move(PlayerSession player,float x,ulong sequence)=>manager.RelayNetworkTransformAsync(player,new NetworkTransformData{ObjectId=player.NetworkObjectId,RoomId=NetworkRoomManager.LobbyRoomId,PositionX=x,RotationW=1,Sequence=sequence});
            await Move(first,0,1);await Move(second,5,1);await Move(far,200,1);await Move(otherScope,5,1);
            await Until(()=>Messages(firstSocket,ServerMsgType.NetworkObjectSpawn).Length>=1&&Messages(secondSocket,ServerMsgType.NetworkObjectSpawn).Length>=1);
            Check(first.VisibleNetworkObjects.ContainsKey(second.NetworkObjectId)&&second.VisibleNetworkObjects.ContainsKey(first.NetworkObjectId),"nearby lobby peers see each other");
            await Until(()=>Messages(secondSocket,ServerMsgType.NetworkAnimationUpdate).Count(x=>x.NetworkAnimation.ObjectId==first.NetworkObjectId)>=2);
            Check(Messages(secondSocket,ServerMsgType.NetworkAnimationUpdate).Where(x=>x.NetworkAnimation.ObjectId==first.NetworkObjectId).Select(x=>x.NetworkAnimation.TrackIndex).Distinct().Count()==2,"visibility entry restores every player animation track");
            Set(first,nameof(PlayerSession.LastNetworkAnimation),null!);
            Check(animations.IsEmpty,"animation reset clears all tracks");

            Check(!first.VisibleNetworkObjects.ContainsKey(far.NetworkObjectId)&&!first.VisibleNetworkObjects.ContainsKey(otherScope.NetworkObjectId),"precise lobby visibility preserves distance and server isolation");
            await Move(second,1000,2);
            await Until(()=>Messages(firstSocket,ServerMsgType.NetworkObjectDespawn).Any(m=>m.NetworkObject.ObjectId==second.NetworkObjectId));
            Check(!first.VisibleNetworkObjects.ContainsKey(second.NetworkObjectId)&&!second.VisibleNetworkObjects.ContainsKey(first.NetworkObjectId),"teleport removes old observers outside new spatial cells");
            await Move(second,40,3);Check(first.VisibleNetworkObjects.ContainsKey(second.NetworkObjectId),"exact visibility radius is inclusive");
            await Move(second,40.1f,4);Check(!first.VisibleNetworkObjects.ContainsKey(second.NetworkObjectId),"adjacent cells still use precise radius filter");
            await Move(second,5,5);
            await manager.LeaveNetworkRoomAsync(second);
            await Until(()=>!first.VisibleNetworkObjects.ContainsKey(second.NetworkObjectId));
            Check(second.VisibleNetworkObjects.IsEmpty,"leaving clears reciprocal visibility");
            Set(second,nameof(PlayerSession.NetworkRoomId),NetworkRoomManager.LobbyRoomId);await Move(second,5,1);
            Check(first.VisibleNetworkObjects.ContainsKey(second.NetworkObjectId),"rejoin accepts reset sequence and recreates observers");
            uint disconnectedId = second.NetworkObjectId;
            await manager.RemoveSessionAsync(second.PlayerId);
            await Until(()=>!first.VisibleNetworkObjects.ContainsKey(disconnectedId));
            Check(!second.IsConnected,"disconnect cleans indexed session and notifies observers");
        }
        finally
        {
            foreach(var player in players)await manager.RemoveSessionAsync(player.PlayerId);
        }

        int priorPending=OfflineSaveQueue.PendingCount;
        var graceWatch=Stopwatch.StartNew();
        for(int i=0;i<32;i++) OfflineSaveQueue.TryEnqueue("grace-check-"+i,"test");
        OfflineSaveQueue.TryEnqueue("grace-check-0","test");
        Check(OfflineSaveQueue.PendingCount==priorPending+32,"pending offline users deduplicated and counted");
        using(var graceTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            await using var reader=OfflineSaveQueue.ReadAllAsync(graceTimeout.Token).GetAsyncEnumerator();
            int delivered=0;
            for(int i=0;i<priorPending+32;i++) { if(await reader.MoveNextAsync()) delivered++; }
            Check(delivered==priorPending+32,"all eligible offline users delivered");
        }
        Check(graceWatch.Elapsed>=TimeSpan.FromSeconds(1.9) && graceWatch.Elapsed<TimeSpan.FromSeconds(4),"burst grace periods overlap instead of occupying save slots");
        Check(OfflineSaveQueue.PendingCount==0,"offline queue pending metric drains");
        Console.WriteLine($"PASS: {checks} server replication checks");
    }
}

sealed record ProbePayload(byte Value,Action Encoded) : ISessionPacketPayload
{
    public int ByteCount=>1;
    public byte[] GetBytes(){Encoded();return new[]{Value};}
    public ISessionPacketPayload MergeFrom(ISessionPacketPayload previous)=>this;
}
sealed class TestSocket : WebSocket
{
    private WebSocketState state=WebSocketState.Open;
    public ConcurrentQueue<byte[]> Sent=new();
    public bool BlockMarker;
    public readonly TaskCompletionSource<bool> MarkerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource<bool> ReleaseMarker = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override WebSocketCloseStatus? CloseStatus=>null;
    public override string? CloseStatusDescription=>null;
    public override WebSocketState State=>state;
    public override string? SubProtocol=>null;
    public override void Abort()=>state=WebSocketState.Aborted;
    public override Task CloseAsync(WebSocketCloseStatus status,string? description,CancellationToken token){state=WebSocketState.Closed;return Task.CompletedTask;}
    public override Task CloseOutputAsync(WebSocketCloseStatus status,string? description,CancellationToken token)=>CloseAsync(status,description,token);
    public override void Dispose()=>state=WebSocketState.Closed;
    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,CancellationToken token){await Task.Delay(Timeout.Infinite,token);throw new OperationCanceledException(token);}
    public override async Task SendAsync(ArraySegment<byte> buffer,WebSocketMessageType type,bool endOfMessage,CancellationToken token)
    {
        if (BlockMarker && buffer.Count==1 && buffer.Array![buffer.Offset]==0)
        {
            MarkerStarted.TrySetResult(true);
            await ReleaseMarker.Task.WaitAsync(token);
        }
        Sent.Enqueue(buffer.ToArray());
    }
}
