using System;
using UnityEngine;

namespace FrameWork
{
    public class cs : MonoBehaviour
    {

        private AnimationController _animationController;

        private void Awake()
        {
            _animationController = GetComponent<AnimationController>();
        }

        private void Update()
        {
            
        }

      
    }
}