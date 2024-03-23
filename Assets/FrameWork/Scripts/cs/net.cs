using System;
using System.Reflection;
using UnityEngine;

namespace FrameWork
{
    public class net : NetWorkSystemMono
    {
        protected override void OnInstantiate(ushort id, ushort objId, string packName, string spawnName, string typeName, Vector3 position,
            Vector3 rotation, bool isAb)
        {
            base.OnInstantiate(id, objId, packName, spawnName, typeName, position, rotation, isAb);
            MyLog.Log("生成消息");
            var obj=(Actor)Activator.CreateInstance(Assembly.GetExecutingAssembly().GetType(typeName));
            MyLog.Log(obj.ToString());
            obj.GetIdentity().SetClientId(id);
            obj.GetIdentity().SetObjId(objId);
            obj.GetGameObject().AddComponent<SyncTransform>();
            obj.AddComponent<Move>().speed = 3;
            var trans = obj.GetGameObject().transform;
            trans.position = position;
            trans.eulerAngles = rotation;
        }
    }
}