using System;
using UnityEngine;

namespace FrameWork
{
    [RequireComponent(typeof(CharacterController))]
    public class MoveComponent: MonoBehaviour
    {
        private float _speed=5;
        public Vector3 dir;
        private Camera _camera;
        private CharacterController _characterController;
        public float moveSpeed;
        private Vector3 _moveVelocity;
        private void Awake()
        {
            _camera=Camera.main;
            _characterController = GetComponent<CharacterController>();
        }


        private void Update()
        {

            dir.x =Input.GetAxisRaw("Horizontal");
            dir.z = Input.GetAxisRaw("Vertical");
            dir=_camera.transform.TransformDirection(dir);
            dir.y = 0;
            moveSpeed = Mathf.Clamp(dir.magnitude,0,.99f)+Input.GetAxis("Shift");
            if (dir.x!=0|| dir.z!=0)
            {
               transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.LookRotation(dir),Time.deltaTime*3);
            }
        }
        
    }
}