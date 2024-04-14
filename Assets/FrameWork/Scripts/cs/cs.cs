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
            if (Input.GetKeyDown("1"))
            {
                _animationController.SetAnim("Run");
            }
            
            if (Input.GetKeyDown("2"))
            {
                _animationController.SetAnim("Walk");
            }
            
            if (Input.GetKeyDown("3"))
            {
                
            }
        }

      
    }
}