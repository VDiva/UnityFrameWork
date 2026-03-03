using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Data
{
    public static class GameData
    {
        private static Dictionary<string, string> _dataDic = new Dictionary<string, string>();

        public static void SetString(string key, string value)
        {
            _dataDic[key] = value;
            PlayerPrefs.SetString(key,value);
        }
        
        public static void AddValue(string key, string value)
        {
            _dataDic[key] = value;
            PlayerPrefs.SetString(key,value);
        }
        
        public static string GetValue(string key, string defaultValue)
        {
            if (!_dataDic.ContainsKey(key))
                _dataDic.Add(key,PlayerPrefs.GetString(key,defaultValue));
            return _dataDic[key];
        }


        public static int GetProperty(string propertyName,string type="Nor",int defaultValue=0)
        {
            var v = GetValue($"property_{type}_" + propertyName, defaultValue + "");
            return v.ToInt();
        }
        
        public static void AddProperty(string propertyName,int count,string type="Nor")
        {
            var key = $"property_{type}_" + propertyName;
            var v = (GetProperty(propertyName, type) + count);
            v = Mathf.Max(0, v);
            SetString(key,v+"");
        }
        
        public static void SetProperty(string propertyName,int count,string type="Nor")
        {
            var key = $"property_{type}_" + propertyName;
            SetString(key,count+"");
        }
    }
}