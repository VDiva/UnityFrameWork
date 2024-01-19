using System;
using System.Collections.Concurrent;
using FrameWork.NetWork.Component;
using NetWork.System;
using Riptide;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FrameWork.cs
{
    public class cs : MonoBehaviour
    {
        
        private int roomId;
        public GameObject prefab;
        private ConcurrentDictionary<ushort, Player> player;
        private void Start()
        {
            player = new ConcurrentDictionary<ushort, Player>();
            NetWorkSystem.Start("127.0.0.1:8888");
        }

        private void OnEnable()
        {
            NetWorkSystem.OnPlayerJoinRoom += OnJoin;
            NetWorkSystem.OnPlayerLeftRoom += OnLeft;
        }

        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                NetWorkSystem.CreateRoom("你好",10);
            }
            
            if (Input.GetKeyDown("2"))
            {
                NetWorkSystem.LeftRoom();
            }
            
            if (Input.GetKeyDown("3"))
            {
                NetWorkSystem.JoinRoom(1);
            }
            
            if (Input.GetKeyDown("4"))
            {
                NetWorkSystem.MatchingRoom("你好",10);
            }
        }
        
        private void OnJoin(ushort id,int roomId)
        {
            this.roomId = roomId;
            var go=Instantiate(prefab, Vector3.zero, Quaternion.identity);
            var p = go.AddComponent<Player>();
            var identity = go.AddComponent<Identity>();
            var sync = go.AddComponent<SyncTransform>();
            sync.positionSyncSpeed = 3;
            identity.SetId(id);
            p.Init(id);
            //NetWorkSystem.OnTransform += p.SyncTransform;
            player.TryAdd(id, p);
        }
        
        private void OnLeft(ushort id)
        {
            if (player.TryRemove(id, out Player go))
            {
                //NetWorkSystem.OnTransform -= go.SyncTransform;
                Destroy(go.gameObject);
            }
        }
    }
}