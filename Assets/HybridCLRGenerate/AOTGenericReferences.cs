using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"Newtonsoft.Json.dll",
		"System.Core.dll",
		"System.dll",
		"UnityEngine.AssetBundleModule.dll",
		"UnityEngine.CoreModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// System.Action<BestHTTP.Extensions.BufferDesc>
	// System.Action<BestHTTP.Extensions.BufferStore>
	// System.Action<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Action<BestHTTP.SignalRCore.Messages.Message>
	// System.Action<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Action<LitJson.PropertyMetadata>
	// System.Action<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Action<byte>
	// System.Action<float,float,object,object>
	// System.Action<int,int>
	// System.Action<int>
	// System.Action<object,BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Action<object,object,object,object>
	// System.Action<object,object,object>
	// System.Action<object,object>
	// System.Action<object,ushort,object>
	// System.Action<object>
	// System.Collections.Concurrent.ConcurrentDictionary.<GetEnumerator>d__32<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.DictionaryEnumerator<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.Node<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary.Tables<object,object>
	// System.Collections.Concurrent.ConcurrentDictionary<object,object>
	// System.Collections.Concurrent.ConcurrentQueue.<Enumerate>d__27<object>
	// System.Collections.Concurrent.ConcurrentQueue.Segment<object>
	// System.Collections.Concurrent.ConcurrentQueue<object>
	// System.Collections.Generic.ArraySortHelper<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.ArraySortHelper<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.ArraySortHelper<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.ArraySortHelper<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.ArraySortHelper<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.ArraySortHelper<LitJson.PropertyMetadata>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ArraySortHelper<byte>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.Comparer<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.Comparer<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.Comparer<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.Comparer<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.Comparer<LitJson.PropertyMetadata>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.Comparer<byte>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.Dictionary.Enumerator<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.Dictionary.Enumerator<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.Enumerator<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.Dictionary.Enumerator<ulong,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<ulong,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.Dictionary.KeyCollection<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.Dictionary.KeyCollection<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.Dictionary.KeyCollection<ulong,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<ulong,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.Dictionary.ValueCollection<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.Dictionary.ValueCollection<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.Dictionary.ValueCollection<ulong,object>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<long,object>
	// System.Collections.Generic.Dictionary<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.Dictionary<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.Dictionary<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.Dictionary<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.Dictionary<ulong,object>
	// System.Collections.Generic.EqualityComparer<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.EqualityComparer<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.EqualityComparer<BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.EqualityComparer<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.EqualityComparer<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.EqualityComparer<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.EqualityComparer<LitJson.ArrayMetadata>
	// System.Collections.Generic.EqualityComparer<LitJson.ObjectMetadata>
	// System.Collections.Generic.EqualityComparer<LitJson.PropertyMetadata>
	// System.Collections.Generic.EqualityComparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.EqualityComparer<byte>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<long>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.EqualityComparer<ulong>
	// System.Collections.Generic.EqualityComparer<ushort>
	// System.Collections.Generic.ICollection<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.ICollection<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.ICollection<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.ICollection<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.ICollection<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.ICollection<LitJson.PropertyMetadata>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,LitJson.ArrayMetadata>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,LitJson.ObjectMetadata>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,LitJson.PropertyMetadata>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<ulong,BestHTTP.SignalR.Messages.ClientMessage>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<ulong,object>>
	// System.Collections.Generic.ICollection<byte>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.ICollection<ushort>
	// System.Collections.Generic.IComparer<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.IComparer<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.IComparer<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.IComparer<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.IComparer<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.IComparer<LitJson.PropertyMetadata>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IComparer<byte>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IDictionary<int,object>
	// System.Collections.Generic.IDictionary<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.IDictionary<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.IDictionary<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.IDictionary<object,object>
	// System.Collections.Generic.IEnumerable<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.IEnumerable<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.IEnumerable<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.IEnumerable<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.IEnumerable<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.IEnumerable<LitJson.PropertyMetadata>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,LitJson.ArrayMetadata>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,LitJson.ObjectMetadata>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,LitJson.PropertyMetadata>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ulong,BestHTTP.SignalR.Messages.ClientMessage>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ulong,object>>
	// System.Collections.Generic.IEnumerable<byte>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerable<ushort>
	// System.Collections.Generic.IEnumerator<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.IEnumerator<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.IEnumerator<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.IEnumerator<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.IEnumerator<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.IEnumerator<LitJson.PropertyMetadata>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,LitJson.ArrayMetadata>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,LitJson.ObjectMetadata>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,LitJson.PropertyMetadata>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<ulong,BestHTTP.SignalR.Messages.ClientMessage>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<ulong,object>>
	// System.Collections.Generic.IEnumerator<byte>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEnumerator<ushort>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<long>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IEqualityComparer<ulong>
	// System.Collections.Generic.IEqualityComparer<ushort>
	// System.Collections.Generic.IList<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.IList<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.IList<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.IList<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.IList<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.IList<LitJson.PropertyMetadata>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IList<byte>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<long,object>
	// System.Collections.Generic.KeyValuePair<object,LitJson.ArrayMetadata>
	// System.Collections.Generic.KeyValuePair<object,LitJson.ObjectMetadata>
	// System.Collections.Generic.KeyValuePair<object,LitJson.PropertyMetadata>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.KeyValuePair<ulong,BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.KeyValuePair<ulong,object>
	// System.Collections.Generic.List.Enumerator<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.List.Enumerator<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.List.Enumerator<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.List.Enumerator<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.List.Enumerator<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.List.Enumerator<LitJson.PropertyMetadata>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.List.Enumerator<byte>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List.SynchronizedList<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.List.SynchronizedList<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.List.SynchronizedList<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.List.SynchronizedList<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.List.SynchronizedList<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.List.SynchronizedList<LitJson.PropertyMetadata>
	// System.Collections.Generic.List.SynchronizedList<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.List.SynchronizedList<byte>
	// System.Collections.Generic.List.SynchronizedList<int>
	// System.Collections.Generic.List.SynchronizedList<object>
	// System.Collections.Generic.List<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.List<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.List<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.List<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.List<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.List<LitJson.PropertyMetadata>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.List<byte>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.ObjectComparer<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.ObjectComparer<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.ObjectComparer<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.ObjectComparer<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.ObjectComparer<LitJson.PropertyMetadata>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ObjectComparer<byte>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<BestHTTP.Extensions.BufferDesc>
	// System.Collections.Generic.ObjectEqualityComparer<BestHTTP.Extensions.BufferStore>
	// System.Collections.Generic.ObjectEqualityComparer<BestHTTP.SignalR.Messages.ClientMessage>
	// System.Collections.Generic.ObjectEqualityComparer<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.Generic.ObjectEqualityComparer<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.Generic.ObjectEqualityComparer<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.Generic.ObjectEqualityComparer<LitJson.ArrayMetadata>
	// System.Collections.Generic.ObjectEqualityComparer<LitJson.ObjectMetadata>
	// System.Collections.Generic.ObjectEqualityComparer<LitJson.PropertyMetadata>
	// System.Collections.Generic.ObjectEqualityComparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ObjectEqualityComparer<byte>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<long>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<ulong>
	// System.Collections.Generic.ObjectEqualityComparer<ushort>
	// System.Collections.Generic.Stack.Enumerator<int>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<int>
	// System.Collections.Generic.Stack<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<BestHTTP.Extensions.BufferDesc>
	// System.Collections.ObjectModel.ReadOnlyCollection<BestHTTP.Extensions.BufferStore>
	// System.Collections.ObjectModel.ReadOnlyCollection<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Collections.ObjectModel.ReadOnlyCollection<BestHTTP.SignalRCore.Messages.Message>
	// System.Collections.ObjectModel.ReadOnlyCollection<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Collections.ObjectModel.ReadOnlyCollection<LitJson.PropertyMetadata>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<byte>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Comparison<BestHTTP.Extensions.BufferDesc>
	// System.Comparison<BestHTTP.Extensions.BufferStore>
	// System.Comparison<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Comparison<BestHTTP.SignalRCore.Messages.Message>
	// System.Comparison<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Comparison<LitJson.PropertyMetadata>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Comparison<byte>
	// System.Comparison<int>
	// System.Comparison<object>
	// System.EventHandler<object>
	// System.Func<byte>
	// System.Func<int>
	// System.Func<object,BestHTTP.SignalRCore.Messages.Message,byte>
	// System.Func<object,byte>
	// System.Func<object,object,object,byte>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object>
	// System.Func<ushort,byte>
	// System.IComparable<object>
	// System.IEquatable<object>
	// System.Linq.Buffer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Nullable<System.DateTime>
	// System.Nullable<System.TimeSpan>
	// System.Nullable<int>
	// System.Nullable<ushort>
	// System.Predicate<BestHTTP.Extensions.BufferDesc>
	// System.Predicate<BestHTTP.Extensions.BufferStore>
	// System.Predicate<BestHTTP.SignalRCore.CallbackDescriptor>
	// System.Predicate<BestHTTP.SignalRCore.Messages.Message>
	// System.Predicate<BestHTTP.WebSocket.Frames.WebSocketFrameReader>
	// System.Predicate<LitJson.PropertyMetadata>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Predicate<byte>
	// System.Predicate<int>
	// System.Predicate<object>
	// }}

	public void RefMethods()
	{
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string)
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string,Newtonsoft.Json.JsonSerializerSettings)
		// object System.Activator.CreateInstance<object>()
		// object[] System.Array.Empty<object>()
		// int System.Array.IndexOf<object>(object[],object,int,int)
		// int System.Array.IndexOfImpl<object>(object[],object,int,int)
		// System.Void System.Array.Resize<byte>(byte[]&,int)
		// System.Void System.Array.Resize<object>(object[]&,int)
		// System.Void System.Array.Sort<object>(object[])
		// System.Void System.Array.Sort<object>(object[],int,int,System.Collections.Generic.IComparer<object>)
		// bool System.Linq.Enumerable.Any<object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,bool>)
		// bool System.Linq.Enumerable.Contains<ushort>(System.Collections.Generic.IEnumerable<ushort>,ushort)
		// bool System.Linq.Enumerable.Contains<ushort>(System.Collections.Generic.IEnumerable<ushort>,ushort,System.Collections.Generic.IEqualityComparer<ushort>)
		// System.Collections.Generic.KeyValuePair<object,object>[] System.Linq.Enumerable.ToArray<System.Collections.Generic.KeyValuePair<object,object>>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>)
		// System.Collections.Generic.Dictionary<object,object> System.Linq.Enumerable.ToDictionary<object,object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Func<object,object>)
		// System.Collections.Generic.Dictionary<object,object> System.Linq.Enumerable.ToDictionary<object,object,object>(System.Collections.Generic.IEnumerable<object>,System.Func<object,object>,System.Func<object,object>,System.Collections.Generic.IEqualityComparer<object>)
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// object UnityEngine.AssetBundle.LoadAsset<object>(string)
		// UnityEngine.AssetBundleRequest UnityEngine.AssetBundle.LoadAssetAsync<object>(string)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object UnityEngine.Object.FindObjectOfType<object>()
		// object UnityEngine.Object.Instantiate<object>(object)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform)
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// object UnityEngine.Resources.Load<object>(string)
	}
}