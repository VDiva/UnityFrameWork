using System;
using FrameWork;
using UnityEngine;

namespace cs
{
    public class Manager : NetWorkSystemMono
    {
        protected override void OnInstantiate(ushort id, ushort objId, string spawnName, Vector3 position, Vector3 rotation, bool isAb)
        {
            base.OnInstantiate(id, objId, spawnName, position, rotation, isAb);
            var go = AssetBundlesLoad.LoadAsset<GameObject>(AssetType.Prefab, spawnName);
            var cube = Instantiate(go, position, Quaternion.Euler(rotation));
            cube.AddComponent<Move>();
            cube.AddComponent<SyncTransform>();
            var identity=cube.GetComponent<Identity>();
            identity.SetId(id);
            identity.SetSpawnId(objId);
        }

        protected override void OnPlayerLeft(ushort id)
        {
            base.OnPlayerLeft(id);
            
        }
    }
}