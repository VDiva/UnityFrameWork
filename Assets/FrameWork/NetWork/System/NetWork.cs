
using System.Collections.Concurrent;
using UnityEngine;


namespace FrameWork
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
            NetWorkSystem.OnBelongingClient += SetBelongingClient;
            NetWorkSystem.OnDestroy += NetDestroy;
        }

        private void OnDisable()
        {
            NetWorkSystem.OnInstantiate -= Spawn;
            NetWorkSystem.OnPlayerLeftRoom -= OnLeft;
            NetWorkSystem.OnRpc -= Rpc;
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
        
        private void Spawn(ushort clientId, ushort objId, string spawnName, Vector3 position, Vector3 rotation,bool isAb)
        {
            GameObject prefab = null;
            if (!_prefabs.TryGetValue(spawnName,out prefab))
            {
                
                if (!isAb)
                {
                    var go = Resources.Load<GameObject>(spawnName);
                    _prefabs.TryAdd(spawnName, go);
                    prefab = go;
                }
                else
                {
                    // FrameWork.BuildTarget buildTarget = BuildTarget.Windows;
                    // RuntimePlatform platform = Application.platform;
                    //
                    // switch (platform)
                    // {
                    //     case RuntimePlatform.WindowsEditor:
                    //         buildTarget = BuildTarget.Windows;
                    //         break;
                    //     case RuntimePlatform.WindowsPlayer:
                    //         buildTarget = BuildTarget.Windows;
                    //         break;
                    //     case RuntimePlatform.Android:
                    //         buildTarget = BuildTarget.Android;
                    //         break;
                    //     case RuntimePlatform.IPhonePlayer:
                    //         buildTarget = BuildTarget.Ios;
                    //         break;
                    // }
                    prefab=AssetBundlesLoad.LoadAsset<GameObject>(GlobalVariables.Configure.AbModePrefabName, spawnName);
                }
            }
            
            var obj=Instantiate(prefab, position, Quaternion.Euler(rotation));
            var syncTransform=obj.AddComponent<SyncTransform>();
            var identity=obj.GetComponent<Identity>();
            identity.SetId(objId);
            identity.SetSpawnId(clientId);

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


        private void SetBelongingClient(ushort newId, ushort[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (_objects.ContainsKey(ids[i]))
                {
                    var identity=_objects[ids[i]].GetComponent<Identity>();
                    identity.SetSpawnId(newId);
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