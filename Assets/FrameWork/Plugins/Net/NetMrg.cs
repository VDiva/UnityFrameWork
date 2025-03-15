using System;
using System.Collections.Generic;
using NetWorkClient;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FrameWork.Plugins.Net
{
    public class NetMrg : NetBehaviour
    {
        
        public static NetMrg Instance;
        
        public GameObject prefab;
        
        protected ObjectPool<GameObject> Pool=new ObjectPool<GameObject>();
        protected List<ushort> Players=new List<ushort>();
        protected Dictionary<ushort,GameObject> PlayerGos=new Dictionary<ushort, GameObject>();
        protected override void JoinRoom(ushort id)
        {
            base.JoinRoom(id);
            if (prefab==null)return;
            Players.Add(id);
            SpawnPlayer(id);
        }

        protected override void LeaveRoom(ushort id)
        {
            base.LeaveRoom(id);
            Players.Remove(id);
            PlayerGos.Remove(id);
        }

        public virtual void SpawnPlayer(ushort id)
        {
            if (prefab==null)return;
            var pos=NetPosition.Transforms[Random.Range(0, NetPosition.Transforms.Count)];
            var go = Instantiate(prefab,pos.position,Quaternion.identity);
            var identity=go.GetComponent<Identity>();
            if (identity==null)
            {
                identity=go.AddComponent<Identity>();
            }
            
            PlayerGos.TryAdd(id, go);
        }
        
        public override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(this.gameObject);
            Instance = this;
            NetClient.RunTime = (() => Time.time);
        }
        

        private void Update()
        {
            NetClient.Update();
        }
    }
}