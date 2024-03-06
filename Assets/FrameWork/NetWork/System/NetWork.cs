
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace FrameWork
{
    public class NetWork : SingletonAsMono<NetWork>
    {
        
        private ConcurrentDictionary<ushort, GameObject> _objects;
        private void Awake()
        {
            
            _objects = new ConcurrentDictionary<ushort, GameObject>();
            DontDestroyOnLoad(this);
        }
        
        private void OnEnable()
        {
            //NetWorkSystem.OnInstantiate += Spawn;
            NetWorkSystem.OnPlayerLeftRoom += OnLeft;
            NetWorkSystem.OnBelongingClient += SetBelongingClient;
            NetWorkSystem.OnDestroy += NetDestroy;
        }

        private void OnDisable()
        {
            //NetWorkSystem.OnInstantiate -= Spawn;
            NetWorkSystem.OnPlayerLeftRoom -= OnLeft;
            NetWorkSystem.OnBelongingClient -= SetBelongingClient;
            NetWorkSystem.OnDestroy -= NetDestroy;
        }

        private void OnApplicationQuit()
        {
            NetWorkSystem.DisConnect();
        }

        private void FixedUpdate()
        {
            NetWorkSystem.UpdateMessage();
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


        private void SetBelongingClient(ushort newId, ushort[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (_objects.ContainsKey(ids[i]))
                {
                    var identity=_objects[ids[i]].GetComponent<Identity>();
                    identity.SetClientId(newId);
                }
            }
        }
        
        private void NetDestroy(ushort objId)
        {
            if (_objects.TryRemove(objId,out GameObject obj))
            {
                Destroy(obj);
            }
        }
    }
}