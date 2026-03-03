using Sirenix.OdinInspector;
using XNode;

namespace FrameWork
{
    [CreateNodeMenu("Video/ColorSurmise")]
    public class ColorSurmise: Node
    {
        [Input]
        public VideoNode input;
        [Output]
        [LabelText("成功")]
        public VideoNode outputSuccess;
        [Output]
        [LabelText("失败")]
        public VideoNode outputFail;

        [LabelText("是否是红色")]
        public bool isRed;

        public bool IsSuc()
        {
            return false;
            //return GameDataMrg.Instance.isRed==isRed;
        }
    }
}