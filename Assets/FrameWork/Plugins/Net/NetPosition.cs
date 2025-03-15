using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Plugins.Net
{
    public class NetPosition : MonoBehaviour
    {
        public static List<Transform> Transforms = new List<Transform>();

        private void OnEnable()
        {
            Transforms.Add(transform);
        }

        private void OnDisable()
        {
            Transforms.Remove(transform);
        }
    }
}