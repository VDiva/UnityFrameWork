using System;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = false,Inherited = false)]
    public class ActorInfoAttribute: Attribute
    {
        public string PackName;
        public string PrefabName;

        public ActorInfoAttribute(string pack,string prefabName)
        {
            PackName = pack;
            PrefabName = prefabName;
        }
    }
}