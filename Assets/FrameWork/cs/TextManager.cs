
using UnityEngine.UI;

namespace FrameWork
{
    public class TextManager : SingletonAsMono<TextManager>
    {
        public Text text;

        public void SetText(string info)
        {
            text.text = info;
        }
    }
}