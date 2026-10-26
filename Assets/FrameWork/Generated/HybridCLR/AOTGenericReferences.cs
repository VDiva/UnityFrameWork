using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

    // {{ AOT assemblies
    public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
    {
        "UniTask.dll",
        "UnityEngine.CoreModule.dll",
        "mscorlib.dll",
    };
    // }}

    // {{ constraint implement type
    // }} 

    // {{ AOT generic types
    // Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask.<>c<HotUpdate.GameEntry.<Start>d__0>
    // Cysharp.Threading.Tasks.CompilerServices.AsyncUniTask<HotUpdate.GameEntry.<Start>d__0>
    // Cysharp.Threading.Tasks.ITaskPoolNode<object>
    // Cysharp.Threading.Tasks.UniTaskCompletionSourceCore<Cysharp.Threading.Tasks.AsyncUnit>
    // System.Action<object>
    // System.Func<byte>
    // System.Func<int>
    // }}

    public void RefMethods()
    {
        // System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.AwaitUnsafeOnCompleted<Cysharp.Threading.Tasks.UniTask.Awaiter,HotUpdate.GameEntry.<Start>d__0>(Cysharp.Threading.Tasks.UniTask.Awaiter&,HotUpdate.GameEntry.<Start>d__0&)
        // System.Void Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder.Start<HotUpdate.GameEntry.<Start>d__0>(HotUpdate.GameEntry.<Start>d__0&)
        // object UnityEngine.Component.GetComponent<object>()
    }
}
