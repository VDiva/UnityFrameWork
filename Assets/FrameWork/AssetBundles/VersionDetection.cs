using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FrameWork.Global;
using UnityEngine;

namespace FrameWork.AssetBundles
{
    public static class VersionDetection
    {
        public static void Detection(Action<List<string>> versionInfo)
        {
            DownLoad.DownLoadAsset(GlobalVariables.UpdateDownLoadUrl+GlobalVariables.ABConfigName,(
                (f, f1, arg3, arg4) =>
                { } ),(
                (bytes, s) =>
                {
                    List<string> infos = new List<string>();

                    string info = Encoding.UTF8.GetString(bytes);
                    string[] newInfo = info.Split('|');
                    
                    FileInfo fileInfo = new FileInfo(Application.persistentDataPath +"/"+  GlobalVariables.ABConfigName);
                    ConcurrentDictionary<string, string> newInfoDictionary;
                    ConcurrentDictionary<string, string> oldInfoDictionary;
                    
                    if (fileInfo.Exists)
                    {
                        newInfoDictionary = new ConcurrentDictionary<string, string>();
                        oldInfoDictionary = new ConcurrentDictionary<string, string>();
                        foreach (var item in newInfo)
                        {
                            string[] sinInfo = item.Split(' ');
                            newInfoDictionary.TryAdd(sinInfo[0], sinInfo[1]);
                        }

                        string oldInfo=File.ReadAllText(Application.persistentDataPath + "/" + GlobalVariables.ABConfigName,Encoding.UTF8);

                        string[] oldInfos = oldInfo.Split('|');
                        foreach (var item in oldInfos)
                        {
                            string[] sinInfo = item.Split(' ');
                            oldInfoDictionary.TryAdd(sinInfo[0], sinInfo[1]);
                        }

                        foreach (var item in newInfoDictionary)
                        {
                            if (oldInfoDictionary.TryGetValue(item.Key, out var value))
                            {
                                if (!value.Equals(item.Value))
                                {
                                    infos.Add(item.Key);
                                }
                            }
                            else
                            {
                                infos.Add(item.Key);
                            }
                        }
                        versionInfo(infos);
                    }
                    else
                    {
                        foreach (var item in newInfo)
                        {
                            infos.Add(item.Split(' ')[0]);
                        }
                        
                        versionInfo(infos);
                    }
                    
                    
                }));
            
            
        }
    }
}