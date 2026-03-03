using Sirenix.OdinInspector;
using UnityEngine;
using XNode;

namespace FrameWork.ChatTool
{
    [Node.CreateNodeMenu("Chat/StartChat")]
    public class StartChat : Node
    {
        [Output] 
        public string outPut;
        
        [LabelText("最大回合数")]
        public int maxRound=-1;
    }
}