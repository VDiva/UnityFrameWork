using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetWork.Tool
{
    public static class Tool
    {
        public static T CopyClass<T>(T t)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(t))));
        }  
    }
}
