using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class Room
    {
        private int RoomId;
        private int MaxNum;
        private string RoomName;
        private Action<Data> roomAction;
        public Room(int MaxNum,string RoomName,int RoomId) { 
            this.MaxNum = MaxNum;
            this.RoomName = RoomName;
            this.RoomId = RoomId;
            RoomManager.Instance.RoomAction = RoomAction;
        }

        private void RoomAction(Data data)
        {
            if (data.RoomData.RoomId.Equals(RoomId))
            {

            }
        }

    }
}
