using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEngine;

namespace FrameWork
{
    public static class VersionDetection
    {
        
        /// <summary>
        /// 版本检测并对比md5码检测那些ab包需要更新 并返回最新的版本文件自己数据
        /// </summary>
        /// <param name="versionInfo">参数1:需要更新得资源数组 参数2:版本config文件</param>
        public static void Detection(Action<List<AbPackDate>,byte[]> versionInfo)
        {
            DownLoad.DownLoadAsset(Config.DownLoadUrl+Config.GetAbPath()+Config.configName,(
                (f1,f2,s1,s2) =>
                { } ),(
                (bytes, s) =>
                {
                    List<AbPackDate> infos = new List<AbPackDate>();

                    string info = Encoding.UTF8.GetString(bytes);
                    string[] newInfo = info.Split('|');

                    FileInfo fileInfo = new FileInfo(Application.persistentDataPath + "/" + Config.GetAbPath() +
                                                     Config.configName);
                    List<AbPackDate> newInfoList=new List<AbPackDate>();
                    List<AbPackDate> oldInfoList=new List<AbPackDate>();
                    
                    if (fileInfo.Exists)
                    {
                        string oldInfo=File.ReadAllText(Application.persistentDataPath + "/" +Config.GetAbPath()+ Config.configName,Encoding.UTF8);
                        string[] oldInfos = oldInfo.Split('|');
                        
                        
                        foreach (var item in newInfo)
                        {
                            string[] strings = item.Split(' ');
                            newInfoList.Add(new AbPackDate(){Name = strings[0],Size = long.Parse(strings[1]),Md5 = strings[2]});
                        }
                        foreach (var item in oldInfos)
                        {
                            string[] strings = item.Split(' ');
                            oldInfoList.Add(new AbPackDate(){Name = strings[0],Size = long.Parse(strings[1]),Md5 = strings[2]});
                        }

                        foreach (var newIn in newInfoList)
                        {
                            foreach (var oldIn in oldInfoList)
                            {
                                if (!ListHasValue(oldInfoList, newIn.Name))
                                {
                                    infos.Add(newIn);
                                }
                                if (newIn.Name.Equals(oldIn.Name)&& !newIn.Md5.Equals(oldIn.Md5))
                                {
                                    infos.Add(newIn);
                                }
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
                }));
            
            
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