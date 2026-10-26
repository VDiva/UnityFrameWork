using UnityEngine;
using YooAsset;

namespace FrameWork
{
    /// <summary>克隆对象持有独立句柄，销毁时释放，不受预制体缓存释放影响。</summary>
    [AddComponentMenu("")]
    public sealed class YooInstanceLease : MonoBehaviour
    {
        private AssetHandle _handle;
        internal void Bind(AssetHandle handle) { _handle = handle; }
        private void OnDestroy()
        {
            if (_handle != null && _handle.IsValid) _handle.Release();
            _handle = null;
        }
    }
}
