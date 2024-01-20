using System;
using System.Collections.Concurrent;
using FrameWork.NetWork.Component;
using FrameWork.Singleton;
using Riptide;
using UnityEditor;
using UnityEngine;

namespace NetWork.System
{
    public class NetWork : SingletonAsMono<NetWork>
    {
        private ConcurrentDictionary<string, GameObject> _prefabs;

        private ConcurrentDictionary<ushort, GameObject> _objects;
        private void Awake()
        {
            _prefabs = new ConcurrentDictionary<string, GameObject>();
            _objects = new ConcurrentDictionary<ushort, GameObject>();
            DontDestroyOnLoad(this);
        }
        
        private void OnEnable()
        {
            NetWorkSystem.OnInstantiate += Spawn;
            NetWorkSystem.OnPlayerLeftRoom += OnLeft;
            NetWorkSystem.OnRpc += Rpc;
        }

        private void OnDisable()
        {
            NetWorkSystem.OnInstantiate -= Spawn;
            NetWorkSystem.OnPlayerLeftRoom -= OnLeft;
            NetWorkSystem.OnRpc -= Rpc;
        }

        private void OnApplicationQuit()
        {
            NetWorkSystem.DisConnect();
        }

        private void FixedUpdate()
        {
            NetWorkSystem.UpdateMessage();
        }
        
        private void Spawn(ushort clientId, ushort objId, string spawnName, Vector3 position, Vector3 rotation)
        {
            GameObject prefab = null;
            if (!_prefabs.TryGetValue(spawnName,out prefab))
            {
                var go = Resources.Load<GameObject>(spawnName);
                _prefabs.TryAdd(spawnName, go);
                prefab = go;
            }
            
            var obj=Instantiate(prefab, position, Quaternion.Euler(rotation));
            var syncTransform=obj.AddComponent<SyncTransform>();
            var identity=obj.GetComponent<Identity>();
            identity.SetId(objId);


            _objects.TryAdd(objId, obj);
        }


        private void Rpc(string methodName, ushort id, object[] param)
        {
            if (_objects.TryGetValue(id,out GameObject obj))
            {
                obj.SendMessage(methodName,param);
            }
        }

        private void OnDisConnect(ushort id)
        {
            OnLeft(id);
        }

        private void OnLeft(ushort id)
        {
            if (_objects.TryRemove(id,out GameObject obj))
            {
                Destroy(obj);
            }
        }
    }
}