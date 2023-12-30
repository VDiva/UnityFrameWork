using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class Room
    {
        /// <summary>
        /// 房间id
        /// </summary>
        private int RoomId;

        /// <summary>
        /// 房间最大人数
        /// </summary>
        private int MaxNum;
        
        /// <summary>
        /// 房间昵称
        /// </summary>
        private string RoomName;

        /// <summary>
        /// 房间消息回调
        /// </summary>
        private Action<Data> roomAction;
        public Room(int MaxNum,string RoomName,int RoomId) { 
            this.MaxNum = MaxNum;
            this.RoomName = RoomName;
            this.RoomId = RoomId;
            RoomManager.Instance.RoomAction = RoomAction;
        }

        /// <summary>
        /// 房间消息广播
        /// </summary>
        /// <param name="data"></param>
        private void RoomAction(Data data)
        {
            if (data.RoomData.RoomId.Equals(RoomId))
            {

            }
        }

    }
}
