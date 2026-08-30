using System;
using TMPro;
using UnityEngine;

namespace FrameWork.Script.Tool
{
    [ExecuteAlways]
    public class CheckTextRect : MonoBehaviour
    {
        private TMP_Text _text;
        private RectTransform _textRect;
        public float maxWidth;
        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _textRect = GetComponent<RectTransform>();
        }

        private void Update()
        {
            var size=_text.GetPreferredValues();
            size.x=Mathf.Min(maxWidth, size.x);
            //size.y=Mathf.Min(maxSize.y, size.y);
            _textRect.sizeDelta=size;
        }
    }
}