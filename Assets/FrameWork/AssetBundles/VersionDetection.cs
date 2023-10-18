using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FrameWork.Global;
using UnityEngine;

namespace FrameWork.AssetBundles
{
    public static class VersionDetection
    {
        public static void Detection(Action<List<AbPackDate>,byte[]> versionInfo)
        {
            DownLoad.DownLoadAsset(GlobalVariables.UpdateDownLoadUrl+GlobalVariables.ABConfigName,(
                (f, f1, arg3, arg4) =>
                { } ),(
                (bytes, s) =>
                {
                    List<AbPackDate> infos = new List<AbPackDate>();

                    string info = Encoding.UTF8.GetString(bytes);
                    string[] newInfo = info.Split('|');
                    
                    FileInfo fileInfo = new FileInfo(Application.persistentDataPath +"/"+  GlobalVariables.ABConfigName);
                    List<AbPackDate> newInfoList=new List<AbPackDate>();
                    List<AbPackDate> oldInfoList=new List<AbPackDate>();
                    
                    if (fileInfo.Exists)
                    {
                        string oldInfo=File.ReadAllText(Application.persistentDataPath + "/" + GlobalVariables.ABConfigName,Encoding.UTF8);
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

        public static bool ListHasValue(List<AbPackDate> da1, string da2)
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


        // public static string GetAllPackSize(List<string> infos)
        // {
        //     long lenght = 1;
        //     foreach (var item in infos)
        //     {
        //         long len=DownLoad.GetPackSize(GlobalVariables.UpdateDownLoadUrl+"/"+item);
        //         lenght += len;
        //     }
        //
        //     return DownLoad.GetFileSize(lenght);
        // }
        //
        // public static void GetAllPackSize(List<List<string>> infos,Action<long> lenght)
        // {
        //     Mono.Instance.StartCoroutine(GetAllPackSizeIEnumerator(infos, lenght));
        // }
        //
        // public static IEnumerator GetAllPackSizeIEnumerator(List<List<string>> infos,Action<long> lenght)
        // {
        //     long len = 1;
        //     int index = 0;
        //     foreach (var item in infos)
        //     {
        //         DownLoad.GetPackSize(GlobalVariables.UpdateDownLoadUrl+"/"+item[0],(l =>
        //         {
        //             len += l;
        //             index += 1;
        //         } ));
        //     }
        //
        //     yield return 0;
        //
        //     while (true)
        //     {
        //         if (index==infos.Count)
        //         {
        //             lenght(len);
        //             yield break;
        //         }
        //
        //         yield return 0;
        //     }
        // }
    }
}