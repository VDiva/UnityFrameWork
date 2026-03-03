
using Sirenix.OdinInspector;
using XNode;

namespace FrameWork
{
    [CreateNodeMenu("Video/VideoIfPlayerEbriety")]
    public class VideoIfPlayerEbriety:Node
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
            //return GameDataMrg.Instance.TmpPlayerJiuLian >= GameDataMrg.Instance.PlayerJiuLian;
        }
    }
}