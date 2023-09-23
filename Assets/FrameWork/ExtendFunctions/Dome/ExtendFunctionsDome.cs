using System;
using UnityEngine;
using UnityEngine.UI;

namespace FrameWork.ExtendFunctions.Dome
{
    public class ExtendFunctionsDome : MonoBehaviour
    {
        public Image image;
        public Text text;
        private void Start()
        {
            image.SetActive(false);
            text.SetActive(false);
        }
    }
}