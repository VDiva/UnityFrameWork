using NetWorkClient;
using UnityEngine;

namespace FrameWork.Plugins.Net
{
    public class Identity : MonoBehaviour
    {
        public ushort objId;
        public ushort clientId;



        public bool IsLocal()
        {
            return NetClient.GetClientId().Equals(clientId);
        }
        
        
    }
}