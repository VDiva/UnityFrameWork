using System;

namespace FrameWork
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = false,Inherited = true)]
    public class ActorInfoAttribute: Attribute
    {
        public string PackName;
        public string PrefabName;
        public bool IsRelease = true;
        public ActorInfoAttribute(string pack,string prefabName,bool isRelease=true)
        {
            PackName = pack;
            PrefabName = prefabName;
            IsRelease = isRelease;
        }
    }
}