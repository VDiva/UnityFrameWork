using System;
using Riptide;

namespace Library.Msg
{
    public static class MsgMrg
    {
        public static Message CreateMsg(MessageSendMode sendMode,Enum id)
        {
            return Message.Create(sendMode,id);
        }
    }
}