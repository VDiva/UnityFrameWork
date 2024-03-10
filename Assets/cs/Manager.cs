using System;
using System.Collections.Generic;
using System.Reflection;
using FrameWork;
using UnityEngine;
using Object = System.Object;

namespace cs
{
    public class Manager : NetWorkSystemMono
    {
        private List<ushort> _clientIds;
        protected void Start()
        {
            _clientIds = new List<ushort>();
        }

        protected override void OnInstantiate(ushort id, ushort objId, string packName, string spawnName, string typeName, Vector3 position,
            Vector3 rotation, bool isAb)
        {
            base.OnInstantiate(id, objId, packName, spawnName, typeName, position, rotation, isAb);
            var actor=(Actor)Assembly.GetExecutingAssembly().CreateInstance(typeName);
            actor?.AddComponent<Move>();
            actor?.AddComponent<SyncTransform>();
            actor?.GetIdentity().SetClientId(id);
            actor?.GetIdentity().SetObjId(objId);
        }

        protected override void OnPlayerLeft(ushort id)
        {
            base.OnPlayerLeft(id);
            EventManager.DispatchEvent(MessageType.NetMessage,NetMessageType.Destroy,new Object[]{id});
        }
    }
}