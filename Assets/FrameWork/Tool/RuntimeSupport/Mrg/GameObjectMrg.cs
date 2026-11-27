using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FrameWork;
using FrameWork.Script.Role;
using FrameWork.Net.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameWork.Script.Mrg
{
    public class GameObjectMrg : SingletonAsMono<GameObjectMrg>
    {
        private static readonly List<Camera> _cameraScratch = new List<Camera>(4);
        Dictionary<string, ObjectPool<GameObject>> _actorsPool = new Dictionary<string, ObjectPool<GameObject>>();
        private readonly List<GameObject> _goList = new List<GameObject>();
        private readonly Dictionary<GameObject, int> _goIndex = new Dictionary<GameObject, int>();
        private Transform _inactiveSpawnRoot;

        private void TrackActiveObject(GameObject go)
        {
            if (go == null || _goIndex.ContainsKey(go))
                return;
            _goIndex[go] = _goList.Count;
            _goList.Add(go);
        }

        private void UntrackActiveObject(GameObject go)
        {
            if (go == null || !_goIndex.TryGetValue(go, out int idx))
                return;
            int lastIdx = _goList.Count - 1;
            if (idx != lastIdx)
            {
                GameObject moved = _goList[lastIdx];
                _goList[idx] = moved;
                if (moved != null)
                    _goIndex[moved] = idx;
            }
            _goList.RemoveAt(lastIdx);
            _goIndex.Remove(go);
        }

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

        public async UniTask<GameObject> Dequeue(string id, RoleType type = RoleType.Other, bool activate = true)
        {
            if (!_actorsPool.TryGetValue(id, out ObjectPool<GameObject> pool))
            {
                pool = Application.isBatchMode
                    ? new ObjectPool<GameObject>(() => CreateInactive(id), 50)
                    : new ObjectPool<GameObject>(() => CreateInactive(id));
                _actorsPool[id] = pool;
            }

            var g = await pool.DeQueue();
            if (g != null)
            {
                TrackActiveObject(g);
                if (!g.TryGetComponent<RoleId>(out var roleId))
                    roleId = g.AddComponent<RoleId>();
                g.transform.SetParent(null);
                roleId.objectName = id;
                roleId.roleType = type;
                g.SetActiveAsCheck(activate);
                return g;
            }

            var go = await CreateInactive(id);
            if (!go.TryGetComponent<RoleId>(out var roleType))
                roleType = go.AddComponent<RoleId>();
            go.transform.SetParent(null);
            roleType.objectName = id;
            roleType.roleType = type;
            go.SetActiveAsCheck(activate);
            TrackActiveObject(go);
            return go;
        }

        public void Enqueue(GameObject go)
        {
            if (go == null) return;
            bool hasRole = go.TryGetComponent<RoleId>(out var roleId);
            if (hasRole && roleId.roleType == RoleType.Player)
                DetachPlayerCameras(go);
            go.SetActiveAsCheck(false);
            UntrackActiveObject(go);
            if (hasRole && !string.IsNullOrEmpty(roleId.objectName) &&
                _actorsPool.TryGetValue(roleId.objectName, out ObjectPool<GameObject> pool))
            {
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                if (!pool.EnQueue(go))
                    Destroy(go);
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
            _cameraScratch.Clear();
            player.GetComponentsInChildren(true, _cameraScratch);
            for (int i = 0; i < _cameraScratch.Count; i++)
            {
                Camera camera = _cameraScratch[i];
                if (camera == null || !camera.CompareTag("MainCamera") || camera.gameObject == player)
                    continue;
                DetachSceneCamera(camera);
            }
            _cameraScratch.Clear();
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
