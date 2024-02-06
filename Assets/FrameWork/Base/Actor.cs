using UnityEngine;

namespace FrameWork
{
    public class Actor : MonoBehaviour
    {
        //public string ActorName;
        private int Index;

        public int GetIndex()
        {
            return Index;
        }
        
        public void SetIndex(int index)
        {
            Index = index;
            //return Index;
        }

    }
}