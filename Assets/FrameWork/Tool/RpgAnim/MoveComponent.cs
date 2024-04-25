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
            dir.x =Input.GetAxisRaw("Horizontal");
            dir.z = Input.GetAxisRaw("Vertical");
            dir=_camera.transform.TransformDirection(dir);
            dir.y = 0;

            moveSpeed = Mathf.Clamp(dir.magnitude,0,.99f)+Input.GetAxis("Shift");
            //MyLog.Log(dir+"-"+speed+"-"+Time.deltaTime);
            //_characterController.Move(dir * _speed*Time.deltaTime);
            // _moveVelocity.x = _characterController.velocity.x;
            // _moveVelocity.z = _characterController.velocity.z;
            // fallingSpeed = _characterController.velocity.y;
            //moveSpeed = _moveVelocity.magnitude;
           // MyLog.Log(moveSpeed.ToString());
            if (dir.x!=0|| dir.z!=0)
            {
               //  dir.x = 0;
               // // dir.z = 0;
               transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.LookRotation(dir),Time.deltaTime*3);
            }
            
            MyLog.Log(Mathf.Abs(Input.GetAxisRaw("Horizontal"))+"-"+Mathf.Abs(Input.GetAxisRaw("Vertical")));
            
        }
        
    }
}