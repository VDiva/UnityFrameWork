using FrameWork.Net.Network;

namespace FrameWork.Script.Role
{
    public enum RoleType
    {
        Other,
        Player,
        Monster,
        PoHuaiWu,
        Fx,
        DiaoLuo,
        Box
    }
    
    public class RoleId : NetworkBehaviour
    {
        [SyncVar(1)] 
        public string objectName;
        [SyncVar(2)] 
        public RoleType roleType;
    }
}
