using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FrameWork;
using FrameWork.Script.Role;
using FrameWork.Script.WebNet;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameWork.Script.Mrg
{
    public class GameObjectMrg : SingletonAsMono<GameObjectMrg>
    {
        Dictionary<string,ObjectPool<GameObject>>  _actorsPool=new Dictionary<string, ObjectPool<GameObject>>();
        private List<GameObject> _goList = new List<GameObject>();
        private Transform _inactiveSpawnRoot;

        private async UniTask<GameObject> CreateInactive(string id)
        {
            if (_inactiveSpawnRoot == null)
            {
                var root = new GameObject("InactiveSpawnRoot");
                root.SetActive(false);
                root.transform.SetParent(transform, false);
                _inactiveSpawnRoot = root.transform;
            }
            // 在禁用的父节点下实例化，避免 OnEnable 和特效在出生坐标设置前执行。
            var instance = await ABMrg.InstantiateAsync(id, _inactiveSpawnRoot);
            instance.SetActive(false);
            return instance;
        }

        public async UniTask<GameObject> Dequeue(string id,RoleType type=RoleType.Other, bool activate = true)
        {
            if (!_actorsPool.ContainsKey(id))
            {
                if (Application.isBatchMode)
                {
                    _actorsPool[id] =
                        new ObjectPool<GameObject>(() => CreateInactive(id), 50);
                }
                else
                {
                    _actorsPool[id] = new ObjectPool<GameObject>(() => CreateInactive(id));
                }
            }

            
            var g = await _actorsPool[id].DeQueue();
            if (g!=null)
            {
                if (!_goList.Contains(g))_goList.Add(g);
                
                if (!g.TryGetComponent<RoleId>(out var roleId))
                {
                    roleId=g.AddComponent<RoleId>();
                }
                g.transform.SetParent(null);
                roleId.objectName = id;
                roleId.roleType = type;
                g.SetActiveAsCheck(activate);
                return g;
            }
            
            var go = await CreateInactive(id);
            if (!go.TryGetComponent<RoleId>(out var roleType))
            {
                roleType=go.AddComponent<RoleId>();
            }
            go.transform.SetParent(null);
            roleType.objectName = id;
            roleType.roleType = type;
            go.SetActiveAsCheck(activate);
            if (!_goList.Contains(go))_goList.Add(go);
            //var roleType=go.transform.getc
            return go;
        }

        public void Enqueue(GameObject go)
        {
            
            // if (go.TryGetComponent<NetworkIdentity>(out var identity))
            // {
            //     WebNetworkRoomManager.Instance..UnSpawn(go);
            // }
            if (go==null)return;
            if (go.TryGetComponent<RoleId>(out var roleId) && roleId.roleType == RoleType.Player)
                DetachPlayerCameras(go);
            go.SetActiveAsCheck(false);
            if (_goList.Contains(go))_goList.Remove(go);
            if (go!=null&& go.TryGetComponent<RoleId>(out var roleType))
            {
                if (_actorsPool.ContainsKey(roleType.objectName))
                {
                    go.transform.SetParent(transform);
                    go.transform.localPosition = Vector3.zero;
                    var isCan=_actorsPool[roleType.objectName].EnQueue(go);
                    if (!isCan)
                    {
                        Destroy(go);
                    }
                }
                else
                {
                    Destroy(go);
                }
            }
            else
            {
                Destroy(go);
            }
        }


        /// <summary>场景摄像机不能跟随玩家进入跨场景对象池。</summary>
        public static void DetachPlayerCameras(GameObject player)
        {
            if (player == null) return;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.CompareTag("MainCamera") || camera.gameObject == player) continue;
                DetachSceneCamera(camera);
            }
        }

        public static void DetachSceneCamera(Camera camera)
        {
            if (camera == null) return;
            camera.transform.SetParent(null, true);
            // 兼容此前已随玩家进入 DontDestroyOnLoad 的摄像机，切图时由旧场景卸载。
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded && camera.gameObject.scene != scene)
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
        }

        public List<GameObject> GetAllGo()
        {
            return _goList;
        }
        
    }
}
