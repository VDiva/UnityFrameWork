using UnityEngine;

namespace FrameWork.NetWork.Component
{
    public class Identity : MonoBehaviour
    {
        private int _id;

        public void SetId(int id)
        {
            _id = id;
        }
        
        public int GetId()
        {
            return _id;
        }
    }
}