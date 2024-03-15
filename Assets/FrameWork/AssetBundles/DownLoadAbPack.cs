using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace FrameWork
{
    public static class DownLoadAbPack
    {
        private static ConcurrentQueue<AbPackDate> _abPackDates = new ConcurrentQueue<AbPackDate>();
        
        /// <summary>
        /// 下载ab包任务
        /// </summary>
        /// <param name="abPackDates"></param>
        /// <param name="progress"></param>
        /// <param name="end"></param>
        public static void AddPackDownTack(List<AbPackDate> abPackDates,Action<float,float,string,string> progress,Action<List<AbPackDate>> end)
        {
            
            long lenght = 0;
            
            foreach (var item in abPackDates)
            {
                lenght += item.Size;
                _abPackDates.Enqueue(item);
            }
            DownLoadAsset(lenght,abPackDates,progress,(() =>
            {
                end(abPackDates);
            }));
        }

        
        /// <summary>
        /// 下载资源并提供下载速度和进度
        /// </summary>
        /// <param name="lenght"></param>
        /// <param name="abPackDates"></param>
        /// <param name="progress"></param>
        /// <param name="end"></param>
        private static void DownLoadAsset(long lenght,List<AbPackDate> abPackDates,Action<float,float,string,string> progress,Action end)
        {
            if (_abPackDates.TryDequeue(out AbPackDate abPackDate))
            {
                DownLoad.DownLoadAsset(GlobalVariables.Configure.DownLoadUrl+"/"+abPackDate.Name,((f1,f2,s1,s2) =>
                {
                    long len = 0;
                    abPackDate.CurDownLoadSize = (long)(abPackDate.Size * f1);
                    foreach (var item in abPackDates)
                    {
                        len += item.CurDownLoadSize;
                    }
                    progress((float)len / lenght, f1, DownLoad.GetFileSize(len), DownLoad.GetFileSize(lenght));
                } ),((bytes, info) =>
                {
                    abPackDate.PackData = bytes;
                    DownLoadAsset(lenght,abPackDates,progress,end);
                } ));
            }
            else
            {
                end();
            }
        }
    }
}