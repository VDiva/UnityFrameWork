
using FrameWork;
using UnityEngine;

namespace cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            NetWorkSystem.Start("127.0.0.1:8888");
            
        }

        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                NetWorkSystem.CreateRoom("1",10);
            }
            
            if (Input.GetKeyDown("2"))
            {
                NetWorkSystem.MatchingRoom("1",10);
            }
            
            if (Input.GetKeyDown("3"))
            {
                NetWorkSystem.Instantiate<CsCube>(Vector3.zero,Vector3.zero,true);
            }
        }
        

    }
}