using System;
using System.Collections.Concurrent;
using System.Collections.Generic;


namespace FrameWork
{
    public static class DownLoadAbPack
    {
        private static ConcurrentQueue<AbPackDate> _abPackDates = new ConcurrentQueue<AbPackDate>();
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

        private static void DownLoadAsset(long lenght,List<AbPackDate> abPackDates,Action<float,float,string,string> progress,Action end)
        {
            if (_abPackDates.TryDequeue(out AbPackDate abPackDate))
            {
                DownLoad.DownLoadAsset(GlobalVariables.Configure.DownLoadUrl+"/"+abPackDate.Name,((f, f1,s3,s4) =>
                {
                    long len = 0;
                    abPackDate.CurDownLoadSize = (long)(abPackDate.Size * f);
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