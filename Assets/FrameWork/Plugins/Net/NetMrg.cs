using System;
using System.Collections.Generic;
using NetWorkClient;
using Riptide;
using UnityEngine;

namespace FrameWork
{
    public static class NetMrg
    {

        static NetMrg()
        {
            NetClient.Connected += OnConnectedToServer;
            NetClient.Disconnected += OnDisConnectedToServer;
            NetClient.JoinRoomAction += JoinRoom;
            NetClient.LeaveRoomAction += LeaveRoom;
            NetClient.InputDataAction += InputData;
        }


        private static void InputData(string json)
        {
            FrameSyncManager.Instance.ReceiveInput(FrameWork.InputData.Deserialize(json));
        }
        
        private static void LeaveRoom(ushort clientId)
        {
            RoomMrg.Instance.LeaveRoom(clientId);
        }

        private static void JoinRoom(ushort clientId)
        {
            RoomMrg.Instance.JoinRoom(clientId);
        }
        
        
        private static void OnConnectedToServer(object sender, EventArgs e)
        {
            RoomMrg.Instance.OnServerConnect();
        }

        
        private static void OnDisConnectedToServer(object sender, EventArgs e)
        {
            RoomMrg.Instance.OnServerDisconnect();
        }
        
        public static void LinkServer()
        {
            NetClient.Start(Config.ServerIp,Config.ServerPort);
            var updateServer = UpdateServer.Instance;
        }
    }
}