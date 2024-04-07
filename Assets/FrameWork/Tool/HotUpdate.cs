
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

namespace FrameWork
{
    public class HotUpdate : MonoBehaviour
    {
        
        private void Awake()
        {
            
            VersionDetection.Detection(((list, bytes) =>
            {
                if (list.Count > 0)
                {
                    MyLog.Log("检测到更新正在下载更新包");
                    
                    DownLoadAbPack.AddPackDownTack(list,
                        ((f, f1, arg3, arg4) => { MyLog.Log($"进度:{f}--速度:{f1}--{arg3}/{arg4}｝"); }), (dates =>
                        {
                            MyLog.Log("下载完毕正在解压");

                            if (!Directory.Exists(Application.persistentDataPath + "/" + Config.GetAbPath()))
                            {
                                Directory.CreateDirectory(
                                    Application.persistentDataPath + "/" + Config.GetAbPath());
                            }

                            foreach (var item in dates)
                            {
                                File.WriteAllBytes(
                                    Application.persistentDataPath + "/" + Config.GetAbPath() + item.Name,
                                    item.PackData);
                            }

                            File.WriteAllBytes(
                                Application.persistentDataPath + "/" + Config.GetAbPath() + Config.configName,
                                bytes);
                            MyLog.Log("解压完毕正在读取资源");
                            Run();
                        }),(s =>
                        {
                            Run();
                            MyLog.Log("下载ab包文件错误");
                        } ));
                }
                else
                {
                    MyLog.Log("未检测到更新包");
                    Run();
                }
            }),(s =>
            {
                MyLog.Log("下载配置文件错误");
                Run();
            } ));
        }



        private void Run()
        {
            //var assembly=DllLoad.Load("RiptideNetworking.dll.bytes");
            var type=DllLoad.LoadType("HotUpdate.dll.bytes", "FrameWork.Main");
            type.GetMethod("Run")?.Invoke(null, null);  
        }
        
    }
}