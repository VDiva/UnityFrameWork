using UnityEngine;

namespace FrameWork
{
    public class Identity : MonoBehaviour
    {
        [SerializeField]
        private ushort _clientSpawnId;
        [SerializeField]
        private ushort _id;

        public void SetObjId(ushort id)
        {
            _id = id;
        }
        
        public ushort GetObjId()
        {
            return _id;
        }
        
        public void SetClientId(ushort id)
        {
            _clientSpawnId = id;
        }
        
        public ushort GetClientId()
        {
            return _clientSpawnId;
        }
        

        public bool IsLocal()
        {
            return _id == NetWorkSystem.GetClientId();
        }

        public bool IsLocalSpawn()
        {
            return _clientSpawnId == NetWorkSystem.GetClientId();
        }
    }
}