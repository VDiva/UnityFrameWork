using FrameWork.Singleton;
using UnityEngine;
using UnityEngine.UI;

namespace FrameWork.cs
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