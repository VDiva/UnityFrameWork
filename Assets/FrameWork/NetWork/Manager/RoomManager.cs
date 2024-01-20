using NetWork.System;
using UnityEngine;

namespace FrameWork.NetWork.Manager
{
    public class RoomManager : NetWorkSystemMono
    {
        protected override void OnPlayerJoin(ushort id, int roomId)
        {
            base.OnPlayerJoin(id, roomId);
        }

        protected override void OnPlayerLeft(ushort id)
        {
            base.OnPlayerLeft(id);
        }
        
        protected override void OnConnected()
        {
            base.OnConnected();
        }

        protected override void OnInstantiate(ushort id, ushort objId, string spawnName, Vector3 position, Vector3 rotation)
        {
            base.OnInstantiate(id, objId, spawnName, position, rotation);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }
        
        
    }
}