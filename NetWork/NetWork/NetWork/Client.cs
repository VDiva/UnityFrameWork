using System;
using System.Net;
using System.Net.Sockets;
using NetWork.NetWork;
using NetWork.NetWork.UserInfo;
using NetWork.NetWork.Message;
using NetWork.NetWork.Data;
using GameData;

namespace NetWork
{
    public class Client
    {


        #region 变量
        /// <summary>
        /// 客户端id
        /// </summary>
        public int ID;
        /// <summary>
        /// 消息接受回调
        /// </summary>
        public Action<byte[],Client, SocketAsyncEventArgs> ReceiveSuccessAction;
        /// <summary>
        /// 消息接收失败回调
        /// </summary>
        public Action<object, SocketAsyncEventArgs,string> ReceiveErrAction;
        /// <summary>
        /// 消息发送成功回调
        /// </summary>
        public Action<object, SocketAsyncEventArgs> SendAction;
        
        
        /// <summary>
        /// socket对象
        /// </summary>
        public System.Net.Sockets.Socket socket;
        
        /// <summary>
        /// 接受消息套接字
        /// </summary>
        private SocketAsyncEventArgs _receive;

        /// <summary>
        /// 发送消息套接字
        /// </summary>
        private SocketAsyncEventArgs _send;

        /// <summary>
        /// 保存接受消息的字节数组
        /// </summary>
        private byte[] _buffer;


        private GameData.RoomData roomData;
        /// <summary>
        /// 用户信息
        /// </summary>
        private UserInfo userInfo;


        #endregion


        #region 构造函数
        /// <summary>
        /// 初始化类
        /// </summary>
        /// <param name="socket">socket对象</param>
        /// <param name="count">接受消息的最大大小</param>
        public Client(System.Net.Sockets.Socket socket,int count)
        {
            _buffer = new byte[count];
            
            _receive = new SocketAsyncEventArgs();
            _receive.SetBuffer(_buffer,0,count);
            _send = new SocketAsyncEventArgs();
            this.socket = socket;
            lobby.Instance.lobbyAction += LobbyMsg;
            _receive.Completed += ReceiveCompleted;
            _send.Completed += SendCompleted;
            WaitReceive();
        }
        #endregion


        #region 大厅消息

        public void LobbyMsg(QueueData data)
        {
            if (!socket.Connected)
            {
                lobby.Instance.lobbyAction -= LobbyMsg;
                return;
            }
        }

        #endregion


        #region 游戏消息

        /// <summary>
        /// 游戏消息
        /// </summary>
        /// <param name="data"></param>
        public void GameMsg(QueueData data)
        {
            if (!socket.Connected)
            {
                Game.Instance.GameAction -= GameMsg;
                return;
            }
        }

        #endregion


        #region 发送消息


        /// <summary>
        /// 发送消息tcp
        /// </summary>
        /// <param name="data"></param>
        public void SendMessage(Data data)
        {
            socket.Send(Tool.Tool.Serialize(data));
            MessageData.GameData.EnQueue(data);
        }

        /// <summary>
        /// 发送消息tcp
        /// </summary>
        /// <param name="data"></param>
        public void SendMessage(byte[] data)
        {
            socket.Send(data);
        }

        /// <summary>
        /// 发送消息udp
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="data"></param>
        public void SendMessage(EndPoint endPoint, Data data)
        {
            socket.SendTo(Tool.Tool.Serialize(data), endPoint);
            MessageData.GameData.EnQueue(data);
        }

        /// <summary>
        /// 发送消息udp
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="data"></param>
        public void SendMessage(EndPoint endPoint, byte[] data)
        {
            socket.SendTo(data, endPoint);
        }

        /// <summary>
        /// 异步发送消息tcp
        /// </summary>
        /// <param name="data"></param>
        public void SendMessageAsync(Data data)
        {
            var dataBytes = Tool.Tool.Serialize(data);
            _send.SetBuffer(dataBytes, 0, dataBytes.Length);
            bool success = socket.SendAsync(_send);
            MessageData.GameData.EnQueue(data);
            if (!success)
            {
                SendSuccess();
            }
        }

        /// <summary>
        /// 异步发送消息tcp
        /// </summary>
        /// <param name="data"></param>
        public void SendMessageAsync(byte[] data)
        {
            _send.SetBuffer(data, 0, data.Length);
            bool success = socket.SendAsync(_send);
            if (!success)
            {

                SendSuccess();
            }
        }

        /// <summary>
        /// 异步发送消息udp
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="data"></param>
        public void SendMessageAsync(EndPoint endPoint, Data data)
        {
            var dataBytes = Tool.Tool.Serialize(data);
            _send.SetBuffer(dataBytes, 0, dataBytes.Length);
            _send.RemoteEndPoint = endPoint;
            bool success = socket.SendToAsync(_send);
            MessageData.GameData.EnQueue(data);
            if (!success)
            {
                SendSuccess();
            }
        }


        /// <summary>
        /// 异步发送消息udp
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="data"></param>
        public void SendMessageAsync(EndPoint endPoint, byte[] data)
        {
            _send.SetBuffer(data, 0, data.Length);
            _send.RemoteEndPoint = endPoint;
            bool success = socket.SendToAsync(_send);
            if (!success)
            {

                SendSuccess();
            }
        }


        /// <summary>
        /// 消息发送成功
        /// </summary>
        private void SendSuccess()
        {
            SendAction?.Invoke(_send.UserToken, _send);
        }

        /// <summary>
        /// 消息发送成功套接字回调
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        private void SendCompleted(object obj, SocketAsyncEventArgs args)
        {
            SendSuccess();
        }


        #endregion


        # region 接受消息
        /// <summary>
        /// 等待消息
        /// </summary>
        private void WaitReceive()
        {
            bool success = socket.ReceiveAsync(_receive);
            if (!success)
            {
                SuccessReceive();
            }
        }


        /// <summary>
        /// 接受消息成功
        /// </summary>
        private void SuccessReceive()
        {
            try
            {
                //Console.WriteLine(_receive.BytesTransferred);
                //foreach (var item in _receive.Buffer)
                //{
                //    Console.WriteLine(item);
                //}
                if (_receive.BytesTransferred > 0 && _receive.SocketError == SocketError.Success)
                {
                    ReceiveSuccessAction?.Invoke(_receive.Buffer, this, _receive);
                }
                else if (_receive.BytesTransferred == 0)
                {
                    if (_receive.SocketError == SocketError.Success)
                    {
                        //SocketManager.Instance.RemoveClient(ID);
                        socket.Close();
                        ReceiveErrAction?.Invoke(socket, _receive, "玩家主动断开链接");
                    }
                    else
                    {
                        //SocketManager.Instance.RemoveClient(ID);
                        socket.Close();
                        ReceiveErrAction?.Invoke(socket, _receive, "玩家由于网络问题断开链接");
                    }
                }
                WaitReceive();
            }
            catch (Exception ex)
            {
                //SocketManager.Instance.RemoveClient(ID);
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 消息套接字回调
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        private void ReceiveCompleted(object obj, SocketAsyncEventArgs args)
        {
            SuccessReceive();
        }
        #endregion


        #region 房间消息
        private void Room(Data data)
        {
            if (!socket.Connected)
            {
                socket.Close();
                return;
            }

            ParseRoom(data);
        }


        private void ParseRoom(Data data)
        {
            switch (data.RoomCMD)
            {
                case RoomCMD.Join:
                    break;
                case RoomCMD.Quit:
                    break;
                case RoomCMD.Ready:
                    break;
                case RoomCMD.NoReady:
                    break;
                case RoomCMD.Start:
                    break;
            }
        }

        #endregion


        #region Rpc消息

        public void RpcTcp(MessageSendType messageSendType, string MethodName)
        {

            Data data = MessageData.GameData.DeQueue();
            data.MessageSendType = messageSendType;
            data.MessageProtocol = MessageProtocol.Tcp;
            data.MessageType = MessageType.Room;
            data.RoomData = roomData;
            SendMessage(data);
        }

        public void RpcUdp(MessageSendType messageSendType, EndPoint endPoint, string MethodName)
        {
            Data data = MessageData.GameData.DeQueue();
            data.MessageSendType = messageSendType;
            data.MessageProtocol = MessageProtocol.Tcp;
            data.MessageType = MessageType.Room;
            data.RoomData = roomData;
            SendMessage(endPoint, data);
        }

        #endregion


    }
}