using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GameData;
using NetWork.NetWork;

namespace NetWork
{
    public class Client
    {
        public int ID;
        public Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;
        public Action<object, SocketAsyncEventArgs,string> ReceiveErrAction;
        public Action<object, SocketAsyncEventArgs> SendAction;
        
        
        
        public System.Net.Sockets.Socket socket;
        
        private SocketAsyncEventArgs _receive;

        private SocketAsyncEventArgs _send;

        private byte[] _buffer;
        
        public Client(System.Net.Sockets.Socket socket,int count)
        {
            _buffer = new byte[count];
            
            _receive = new SocketAsyncEventArgs();
            _receive.SetBuffer(_buffer,0,count);
            _send = new SocketAsyncEventArgs();
            this.socket = socket;

            lobby.Instance.lobbyAction += Lobby;

            _receive.Completed += ReceiveCompleted;
            _send.Completed += SendCompleted;
            WaitReceive();
        }

        
        public void Lobby(Data data)
        {

        }

        

        public void SendMessage(Data data)
        {
            socket.Send(Tool.Tool.Serialize(data));
        }
        
        public void SendMessage(byte[] data)
        {
            socket.Send(data);
        }


        public void SendMessage(EndPoint endPoint,Data data)
        {
            socket.SendTo(Tool.Tool.Serialize(data), endPoint);
        }

        public void SendMessage(EndPoint endPoint, byte[] data)
        {
            socket.SendTo(data,endPoint);
        }


        public void SendMessageAsync(Data data)
        {
            var dataBytes = Tool.Tool.Serialize(data);
            _send.SetBuffer(dataBytes,0,dataBytes.Length);
            bool success= socket.SendAsync(_send);
            if (!success)
            {
                SendSuccess();
            }
        }
        
        
        public void SendMessageAsync(byte[] data)
        {
            _send.SetBuffer(data,0,data.Length);
            bool success= socket.SendAsync(_send);
            if (!success)
            {
               
                SendSuccess();
            }
        }

        public void SendMessageAsync(EndPoint endPoint, Data data)
        {
            var dataBytes = Tool.Tool.Serialize(data);
            _send.SetBuffer(dataBytes, 0, dataBytes.Length);
            _send.RemoteEndPoint = endPoint;
            bool success = socket.SendToAsync(_send);
            if (!success)
            {
                SendSuccess();
            }
        }


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



        private void SendSuccess()
        {
            SendAction?.Invoke(_send.UserToken,_send);
        }
        
        private void SendCompleted(object obj, SocketAsyncEventArgs args)
        {
            SendSuccess();
        }


        private void WaitReceive()
        {
            bool success= socket.ReceiveAsync(_receive);
            if (!success)
            {
                SuccessReceive();
            }
        }


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
                    ReceiveSuccessAction?.Invoke(_receive.Buffer, socket, _receive);
                }
                else if (_receive.BytesTransferred == 0)
                {
                    if (_receive.SocketError == SocketError.Success)
                    {
                        //SocketManager.Instance.RemoveClient(ID);
                        ReceiveErrAction?.Invoke(socket, _receive, "玩家主动断开链接");
                    }
                    else
                    {
                        //SocketManager.Instance.RemoveClient(ID);
                        ReceiveErrAction?.Invoke(socket, _receive, "玩家由于网络问题断开链接");
                    }
                }
                WaitReceive();
            }catch (Exception ex)
            {
                //SocketManager.Instance.RemoveClient(ID);
                Console.WriteLine(ex.ToString());
            }
        }

        private void ReceiveCompleted(object obj, SocketAsyncEventArgs args)
        {
            SuccessReceive();
        }



        
        
        
    }
}