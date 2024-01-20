using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetWork.Data
{
    public class ObjDate
    {

        public ushort BelongingClient;

        public string SpawnName;

        public Vector3 Position;

        public Vector3 Rotation;

        public bool isPlayer;

        public ObjDate() 
        { 
            SpawnName = string.Empty;
            Position=new Vector3();
            Rotation=new Vector3();
          
        }

    }
}
