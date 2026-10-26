using FrameWork.Script.WebNet;

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
    
    public class RoleId : WebNetworkBehaviour
    {
        [WebSyncVar(1)] 
        public string objectName;
        [WebSyncVar(2)] 
        public RoleType roleType;
    }
}
