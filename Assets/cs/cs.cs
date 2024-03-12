
using System;
using FrameWork;
using UnityEngine;

namespace cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            //NetWorkSystem.Start("192.168.31.131:8888");

            var uiActor=(InputCs)UiManager.Instance.ShowUi<InputCs>();
            // uiActor.ButtonButton.onClick.AddListener((() =>
            // {
            //     NetWorkSystem.Start(uiActor.InputFieldInputField.text);
            // }));
            //
            // if (typeof(BtnGroup).GetProperties().Length>0)return;
            // if (Application.platform==RuntimePlatform.Android)
            // {
            //     var btn=(BtnGroup)UiManager.Instance.ShowUi<BtnGroup>();
            //     btn.ButtonBtn1.onClick.AddListener((() => {EventManager.DispatchEvent(5,1,new object[]{-1.0f});}));
            //     btn.ButtonBtn2.onClick.AddListener((() => {EventManager.DispatchEvent(5,1,new object[]{1.0f});}));
            //     btn.ButtonBtn3.onClick.AddListener((() => {EventManager.DispatchEvent(5,1,new object[]{0f});}));
            //     btn.ButtonBtn4.onClick.AddListener((() =>
            //     {
            //         NetWorkSystem.MatchingRoom("tttt",10);
            //         
            //     }));
            //     btn.ButtonBtn5.onClick.AddListener((() => {NetWorkSystem.Instantiate<CsCube>(Vector3.zero,Vector3.zero,true);}));
            // }
        }

        private void Update()
        {
            if (Input.GetKeyDown("1"))
            {
                NetWorkSystem.CreateRoom("tttt",10);
            }
            
            if (Input.GetKeyDown("2"))
            {
                NetWorkSystem.MatchingRoom("tttt",10);
            }
            
            if (Input.GetKeyDown("3"))
            {
                NetWorkSystem.Instantiate<CsCube>(Vector3.zero,Vector3.zero,true);
            }
        }
    }
}