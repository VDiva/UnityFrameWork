
using Sirenix.OdinInspector;
using XNode;

namespace FrameWork
{
    [CreateNodeMenu("Video/VideoIfTargetEbriety")]
    public class VideoIfTargetEbriety:Node
    {
        [Input]
        public VideoNode input;
        [Output]
        [LabelText("成功")]
        public VideoNode outputSuccess;
        [Output]
        [LabelText("失败")]
        public VideoNode outputFail;


        public bool IsSuc()
        {
            return false;
            //return GameDataMrg.Instance.TmpTargetJiuLian >= GameDataMrg.Instance.TargetJiuLian;
        }
    }
}