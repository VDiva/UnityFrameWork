using System;
using System.Net.Sockets;
using System.Text;

namespace FrameWork.NetManager.Socket
{
    public class Client
    {
        public int ID;
        public Action<byte[],object, SocketAsyncEventArgs> ReceiveSuccessAction;
        public Action<object, SocketAsyncEventArgs> ReceiveErrAction;
        public Action<object, SocketAsyncEventArgs> SendAction;
        
        
        
        private System.Net.Sockets.Socket _socket;
        
        private SocketAsyncEventArgs _receive;

        private SocketAsyncEventArgs _send;

        private byte[] _buffer;
        
        public Client(System.Net.Sockets.Socket socket,int count)
        {
            _buffer = new byte[count];
            
            _receive = new SocketAsyncEventArgs();
            _receive.SetBuffer(_buffer,0,count);
            _send = new SocketAsyncEventArgs();
            _socket = socket;

            _receive.Completed += ReceiveCompleted;
            _send.Completed += SendCompleted;
            WaitReceive();
        }

        
        

        public void SendMessage(object obj)
        {
            _socket.Send(Tool.Tool.Serialize(obj));
        }

        public void SendMessageAsync(object obj)
        {
            byte[] data = Tool.Tool.Serialize(obj);
            _send.SetBuffer(data,0,data.Length);
            bool success=_socket.SendAsync(_send);
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
            bool success=_socket.ReceiveAsync(_receive);
            if (!success)
            {
                SuccessReceive();
            }
        }

        private void SuccessReceive()
        {
            if(_receive.BytesTransferred>0&& _receive.SocketError == SocketError.Success)
            {
                byte[] bytes=new byte[_receive.BytesTransferred];
                Buffer.BlockCopy(_receive.Buffer,0,bytes,0,_receive.BytesTransferred);
                ReceiveSuccessAction?.Invoke(bytes,_receive.UserToken,_receive);
                
            }else if (_receive.BytesTransferred>0&& _receive.SocketError != SocketError.Success)
            {
                ReceiveErrAction?.Invoke(_receive.UserToken,_receive);
            }
            else
            {
                ReceiveErrAction?.Invoke(_receive.UserToken,_receive);
            }
            WaitReceive();
        }

        private void ReceiveCompleted(object obj, SocketAsyncEventArgs args)
        {
            SuccessReceive();
        }
        
        
    }
}