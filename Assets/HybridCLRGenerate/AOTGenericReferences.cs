using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"Tool.dll",
		"UnityEngine.CoreModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// FrameWork.SingletonAsMono<object>
	// }}

	public void RefMethods()
	{
		// System.Void FrameWork.NetWorkSystem.Instantiate<object>(UnityEngine.Vector3,UnityEngine.Vector3,bool,bool)
		// object System.Reflection.CustomAttributeExtensions.GetCustomAttribute<object>(System.Reflection.MemberInfo)
		// object UnityEngine.Component.GetComponent<object>()
	}
}