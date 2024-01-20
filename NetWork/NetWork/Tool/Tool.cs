using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetWork.Data;
using Newtonsoft.Json;
using Riptide;

namespace NetWork.Tool
{
    public static class Tool
    {
        public static T CopyClass<T>(T t)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(t))));
        }  


        public static void AddGameObject(this Message message,ObjDate gameObject)
        {
            message.AddString(gameObject.SpawnName);
            message.AddVector3(gameObject.Position);
            message.AddVector3(gameObject.Rotation);
            
        }

        public static void AddVector3(this Message message, Vector3 vector3)
        {
            message.AddFloat(vector3.x);
            message.AddFloat(vector3.y);
            message.AddFloat(vector3.z);
        }


        public static Vector3 GetVector3(this Message message)
        {
            return new Vector3() { x=message.GetFloat(),y=message.GetFloat(), z=message.GetFloat() };
        }
    }
}
