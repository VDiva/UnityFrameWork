using UnityEngine;

namespace FrameWork
{
    public class Identity : MonoBehaviour
    {
        public ushort _clientSpawnId;
        public ushort _id;

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