using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.Networking;

namespace FrameWork
{
    public static class VersionDetection
    {
        
        /// <summary>
        /// 版本检测并对比md5码检测那些ab包需要更新 并返回最新的版本文件自己数据
        /// </summary>
        /// <param name="versionInfo">参数1:需要更新得资源数组 参数2:版本config文件</param>
        public static void Detection(string abConfigPath,Action<List<AbPackDate>,byte[]> versionInfo,Action<string> err=null)
        {
            DownLoad.DownLoadAsset(Config.DownLoadUrl+abConfigPath+Config.configName,(
                (f1,f2,s1,s2) =>
                { } ),(
                (bytes, s) =>
                {
                    List<AbPackDate> infos = new List<AbPackDate>();

                    string info = Encoding.UTF8.GetString(bytes);
                    string[] newInfo = info.Split('|');
                    FileInfo oldFileInfo = new FileInfo(abConfigPath+  Config.configName);

                    if (oldFileInfo.Exists)
                    {
                        string oldInfo=File.ReadAllText(abConfigPath+ Config.configName,Encoding.UTF8);
                        string[] oldInfos = oldInfo.Split('|');
                        List<AbPackDate> infoList=new List<AbPackDate>();

                        var newId = newInfo.Select((s1 => s1.Split(' ')[2])).ToArray();
                        var oldId = oldInfos.Select((s1 => s1.Split(' ')[2])).ToArray();
                        List<string> downloadId = new List<string>();
                        
                        if (File.Exists(Application.persistentDataPath+abConfigPath+Config.configName))
                        {
                            var txt=File.ReadAllText(Application.persistentDataPath+abConfigPath+Config.configName).Split('|');
                            downloadId=txt.Select((s1 => s1.Split(' ')[2])).ToList();
                        }
                        
                        for (int i = 0; i < newId.Length; i++)
                        {
                            if (!oldId.Contains(newId[i])&&!downloadId.Contains(newId[i]))
                            {
                                string[] strings = newInfo[i].Split(' ');
                                infos.Add(new AbPackDate(){Name = strings[0],Size = long.Parse(strings[1]),Md5 = strings[2]});
                            }
                        }
                        versionInfo(infos,bytes);
                    }
                    else
                    {
                        foreach (var item in newInfo)
                        {
                            string[] strings = item.Split(' ');
                            infos.Add(new AbPackDate(){Name = strings[0],Size = long.Parse(strings[1]),Md5 = strings[2]});
                        }
                        versionInfo(infos,bytes);
                    }
                }),err);
            
            
        }

        public static void Detection(Action<List<AbPackDate>, byte[]> versionInfo, Action<string> err = null)
        {
            var newPath =Config.GetAbPath();
            Detection(newPath,versionInfo,err);
        }

        
        
        /// <summary>
        /// 对比md5码
        /// </summary>
        /// <param name="da1"></param>
        /// <param name="da2"></param>
        /// <returns></returns>
        private static bool ListHasValue(List<AbPackDate> da1, string da2)
        {
            foreach (var item in da1)
            {
                if (item.Name.Equals(da2))
                {
                    return true;
                }
            }

            return false;
        }
        
    }
}