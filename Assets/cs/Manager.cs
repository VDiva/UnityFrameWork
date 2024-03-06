using System;
using System.Collections.Generic;
using FrameWork;
using UnityEngine;

namespace cs
{
    public class Manager : NetWorkSystemMono
    {
        private List<ushort> _clientIds;

        private void Awake()
        {
            _clientIds = new List<ushort>();
        }

        protected override void OnInstantiate(ushort id, ushort objId, string spawnName, Vector3 position, Vector3 rotation, bool isAb)
        {
            base.OnInstantiate(id, objId, spawnName, position, rotation, isAb);
            if (_clientIds.Contains(objId))return;
            
            _clientIds.Add(objId);
            var go = AssetBundlesLoad.LoadAsset<GameObject>(AssetType.Prefab, spawnName);
            var cube = Instantiate(go, position, Quaternion.Euler(rotation));
            cube.AddComponent<Move>();
            cube.AddComponent<SyncTransform>();
            var identity=cube.GetComponent<Identity>();
            identity.SetObjId(objId);
            identity.SetClientId(id);
        }

        protected override void OnPlayerLeft(ushort id)
        {
            base.OnPlayerLeft(id);
            NetWorkSystem.Destroy(id);
            _clientIds.Remove(id);
        }


        protected override void OnNetDestroy(ushort objId)
        {
            base.OnNetDestroy(objId);
            _clientIds.Remove(objId);
        }
    }
}