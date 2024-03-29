using System;
using UnityEngine;

namespace FrameWork
{
    public class Manager : SingletonAsMono<Manager>
    {
        private void Start()
        {
            NetWorkSystem.Start("127.0.0.1:8888");
            GameObject go = new GameObject("net");
            go.AddComponent<net>();
            //UiManager.Instance.ShowUi<CsText>();
        }

        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                NetWorkSystem.CreateRoom("2222",10);
            }
            
            if (Input.GetKeyDown("2"))
            {
                NetWorkSystem.MatchingRoom("2222",10);
            }
            
            if (Input.GetKeyDown("3"))
            {
                NetWorkSystem.Instantiate<CsSphere>(Vector3.zero,Vector3.zero,true);
            }
        }
        
        
    }
}