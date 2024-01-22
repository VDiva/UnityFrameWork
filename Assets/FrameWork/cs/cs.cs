
using UnityEngine;


namespace FrameWork
{
    public class cs : MonoBehaviour
    {

        private void Start()
        {
            NetWorkSystem.Start("127.0.0.1:8888");
        }

        private void OnEnable()
        {

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


            if (Input.GetKeyDown(KeyCode.P))
            {
                NetWorkSystem.Instantiate("Cube",Vector3.zero,Vector3.zero,true,true);
            }


            if (Input.GetKeyDown(KeyCode.E))
            {
                NetWorkSystem.Instantiate("C",new Vector3(5,0,0),Vector3.zero,false,true);
                //NetWorkSystem.Rpc();
            }


            if (Input.GetKeyDown(KeyCode.O))
            {
                var net = FindObjectOfType<CsMove>();
                NetWorkSystem.Destroy(net);
            }
            
        }
        
        
        
        

    }
}