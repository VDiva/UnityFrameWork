using System;
using UnityEngine;

namespace FrameWork
{
    public class Manager : SingletonAsMono<Manager>
    {
        private void Start()
        {
            
            
            
            NetWorkSystem.Start("192.168.31.131:8888");
            GameObject go = new GameObject("net");
            go.AddComponent<net>();

            // if (Application.platform==RuntimePlatform.Android)
            // {
            //     
            // }
            UiManager.Instance.ShowUi<InputCs>();
            
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