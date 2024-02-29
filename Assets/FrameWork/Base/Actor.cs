using UnityEngine;

namespace FrameWork
{
    public class Actor : MonoBehaviour
    {
        //public string ActorName;
        private int Index=-999999;
        private string ActorName;

        public int GetIndex()
        {
            return Index;
        }
        
        public void SetIndex(int index)
        {
            Index = index;
            //return Index;
        }
        
        public string GetActorName()
        {
            return ActorName;
        }
        
        public void SetActorName(string name)
        {
            ActorName = name;
            //return Index;
        }
    }
}