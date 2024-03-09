using System;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = true,Inherited = true)]
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