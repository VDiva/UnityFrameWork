using FrameWork.Data;
using Sirenix.OdinInspector;
using XNode;

namespace FrameWork
{
    [CreateNodeMenu("Video/IsWineList")]
    public class IsWineList: Node
    {

        [Input]
        public VideoNode input;
        [Output]
        [LabelText("成功")]
        public VideoNode outputSuccess;
        [Output]
        [LabelText("失败")]
        public VideoNode outputFail;
        
        public string wineListKey;
        
        public bool IsSuc()
        {
            return false;
           //return GameData.xlsxWineListKey == wineListKey;
        }
    }
}