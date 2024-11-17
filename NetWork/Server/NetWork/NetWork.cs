using System;
using Riptide;
using Riptide.Utils;

namespace FrameWork
{
    public static class NetWork
    {
        public static void InitLog(RiptideLogger.LogMethod debugMethod,
            RiptideLogger.LogMethod infoMethod,
            RiptideLogger.LogMethod warningMethod,
            RiptideLogger.LogMethod errorMethod,
            bool includeTimestamps,
            string timestampFormat = "HH:mm:ss") 
        {
            RiptideLogger.Initialize(debugMethod, infoMethod, warningMethod, errorMethod, includeTimestamps, timestampFormat);
        }
        
        public static void InitLog(Action<string> action) 
        {
            RiptideLogger.Initialize(log => action?.Invoke(log),true);
        }



        public static Message GetMsg(MessageSendMode sendMode,Enum id)
        {
            Message msg = Message.Create(sendMode,id);
            return msg;
        }
        
        public static Message GetMsg(MessageSendMode sendMode,ushort id)
        {
            Message msg = Message.Create(sendMode,id);
            return msg;
        }
    }
}