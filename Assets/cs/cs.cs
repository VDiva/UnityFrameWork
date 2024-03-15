
using System;
using System.IO;
using FrameWork;
using UnityEngine;

namespace cs
{
    public class cs : MonoBehaviour
    {
        private void Start()
        {
            
            VersionDetection.Detection(((list, bytes) =>
            {
               
                if (list.Count>0)
                {
                    MyLog.Log("发现"+list.Count+"个更新包,正在更新");
                    DownLoadAbPack.AddPackDownTack(list,((f, f1, arg3, arg4) =>
                    {
                        MyLog.Log(string.Format("进度：{0}-速度：{1}-{2}/{3}",f,f1,arg3,arg4));
                    } ),(dates =>
                    {
                        foreach (var item in dates)
                        {
                            File.WriteAllBytes(Application.persistentDataPath+"/"+item.Name,item.PackData);
                        }
                        File.WriteAllBytes(Application.persistentDataPath+"/"+GlobalVariables.Configure.ConfigName,bytes);
                    } ));
                }
                else
                {
                    MyLog.Log("没有发现更新文件");
                }
                
                
            }));
            new CsCube();
            //NetWorkSystem.Start("192.168.31.131:8888");

            // var uiActor=(InputCs)UiManager.Instance.ShowUi<InputCs>();
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
            // if (Input.GetKeyDown("1"))
            // {
            //     NetWorkSystem.CreateRoom("tttt",10);
            // }
            //
            // if (Input.GetKeyDown("2"))
            // {
            //     NetWorkSystem.MatchingRoom("tttt",10);
            // }
            //
            // if (Input.GetKeyDown("3"))
            // {
            //     NetWorkSystem.Instantiate<CsCube>(Vector3.zero,Vector3.zero,true);
            // }
        }
    }
}