using UnityEngine;

namespace FrameWork
{
    public class Identity : MonoBehaviour
    {
        private ushort _clientSpawnId;
        private ushort _id;

        public void SetId(ushort id)
        {
            _id = id;
        }
        
        public ushort GetId()
        {
            return _id;
        }
        
        public void SetSpawnId(ushort id)
        {
            _clientSpawnId = id;
        }
        
        public ushort GetSpawnId()
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