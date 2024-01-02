using GameData;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetWork.NetWork
{
    public class Room
    {
        #region 变量
        /// <summary>
        /// 房间id
        /// </summary>
        private int RoomId;

        /// <summary>
        /// 房间最大人数
        /// </summary>
        private int MaxNum;

        private int CurNum;
        
        /// <summary>
        /// 房间昵称
        /// </summary>
        private string RoomName;

        /// <summary>
        /// 房间消息回调
        /// </summary>
        private Action<Data.QueueData> roomAction;
        #endregion

        #region 构造函数
        public Room(int MaxNum,string RoomName,int RoomId) { 
            this.MaxNum = MaxNum;
            this.RoomName = RoomName;
            this.RoomId = RoomId;
            RoomManager.Instance.RoomAction += RoomAction;
        }
        #endregion

        #region 房间消息广播
        private void RoomAction(Data.QueueData data)
        {
            if (data.data.RoomData.RoomId.Equals(RoomId))
            {
                Parse(data);
            }
        }

        /// <summary>
        /// 解析消息
        /// </summary>
        private void Parse(Data.QueueData data)
        {
            roomAction?.Invoke(data);
        }


        #endregion

    }
}
