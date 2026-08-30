using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FrameWork;
using FrameWork.Script.WebNet;
using UnityEngine;

namespace FrameWork.Script.Mrg
{
    public class RoleId : MonoBehaviour
    {
        public string objectName;
        public RoleType roleType;
    }
    
    public enum RoleType
    {
        Other,
    }
    
    public class GameObjectMrg : SingletonAsMono<GameObjectMrg>
    {
        Dictionary<string,ObjectPool<GameObject>>  _actorsPool=new Dictionary<string, ObjectPool<GameObject>>();
        private List<GameObject> _goList = new List<GameObject>();
        public async UniTask<GameObject> Dequeue(string id,RoleType type=RoleType.Other)
        {
            if (!_actorsPool.ContainsKey(id))
            {
                if (Application.isBatchMode)
                {
                    _actorsPool[id] =
                        new ObjectPool<GameObject>((async () => Instantiate(await ABMrg.LoadAsync<GameObject>(id))), 50);
                }
                else
                {
                    _actorsPool[id] = new ObjectPool<GameObject>((async () => Instantiate(await ABMrg.LoadAsync<GameObject>(id))));
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
                g.SetActiveAsCheck(true);
                roleId.objectName = id;
                roleId.roleType = type;
                return g;
            }
            
            var go = await ABMrg.InstantiateAsync(id);
            if (!go.TryGetComponent<RoleId>(out var roleType))
            {
                roleType=go.AddComponent<RoleId>();
            }
            go.transform.SetParent(null);
            go.SetActiveAsCheck(true);
            roleType.objectName = id;
            roleType.roleType = type;
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


        public List<GameObject> GetAllGo()
        {
            return _goList;
        }
        
    }
}
