using System;
using UnityEngine;

namespace FrameWork
{
    [RequireComponent(typeof(CharacterController))]
    public class MoveComponent: MonoBehaviour
    {
        private float _speed;
        public Vector3 dir;
        private Camera _camera;
        private Vector3 _cameraForward;
        private CharacterController _characterController;
        public float moveSpeed;
        public float fallingSpeed;

        private Vector3 _moveVelocity;
        private void Awake()
        {
            _camera=Camera.main;
            _characterController = GetComponent<CharacterController>();
        }


        private void Update()
        {
            _cameraForward = _camera.transform.forward;
            dir.x =Input.GetAxis("Horizontal");
            dir.z = Input.GetAxis("Vertical");
            dir=_camera.transform.TransformDirection(dir);
            dir.y = 0;
            
            _speed = 5 + Input.GetAxis("Shift")*10;
            //MyLog.Log(dir+"-"+speed+"-"+Time.deltaTime);
            transform.Translate(dir*_speed*Time.deltaTime,Space.World);
            _characterController.Move(dir * _speed*Time.deltaTime);
            _moveVelocity.x = _characterController.velocity.x;
            _moveVelocity.z = _characterController.velocity.z;
            fallingSpeed = _characterController.velocity.y;
            moveSpeed = _moveVelocity.magnitude;
           // MyLog.Log(moveSpeed.ToString());
            if (dir.x!=0|| dir.z!=0)
            {
               //  dir.x = 0;
               // // dir.z = 0;
               transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.LookRotation(dir),Time.deltaTime*3);
            }
            
            
            
        }
        
    }
}