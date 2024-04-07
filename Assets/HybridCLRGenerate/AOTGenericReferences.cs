using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"System.dll",
		"Tool.dll",
		"UnityEngine.CoreModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// FrameWork.SingletonAsMono<object>
	// System.Action<object>
	// System.Collections.Generic.Stack.Enumerator<int>
	// System.Collections.Generic.Stack<int>
	// }}

	public void RefMethods()
	{
		// object FrameWork.Actor.AddComponent<object>()
		// System.Void FrameWork.NetWorkSystem.Instantiate<object>(UnityEngine.Vector3,UnityEngine.Vector3,bool,bool)
		// FrameWork.UiActor FrameWork.UiManager.ShowUi<object>()
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.GameObject.AddComponent<object>()
	}
}