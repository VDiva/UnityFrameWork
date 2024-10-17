using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Data
{
    public static class GameData
    {
        private static Dictionary<string, string> _dataDic = new Dictionary<string, string>();
        
        public static string GetString(string key,string defaultValue)
        {
            if (!_dataDic.ContainsKey(key))
                _dataDic.Add(key,PlayerPrefs.GetString(key,defaultValue));
            return _dataDic[key];
        }

        public static void SetString(string key, string value)
        {
            _dataDic[key] = value;
            PlayerPrefs.SetString(key,value);
        }


        public static int GetInt(string key, int defaultValue)
        {
            return int.Parse(GetString(key, defaultValue.ToString()));
        }
        
        public static float GetFloat(string key, float defaultValue)
        {
            return float.Parse(GetString(key, defaultValue.ToString()));
        }
        
        public static long GetLong(string key, long defaultValue)
        {
            return long.Parse(GetString(key, defaultValue.ToString()));
        }
        
        public static bool GetBool(string key, bool defaultValue)
        {
            
            return bool.Parse(GetString(key, defaultValue.ToString()));
        }
        
        
        public static void SetInt(string key, int defaultValue)
        {
            SetString(key, defaultValue.ToString());
        }
        
        public static void SetFloat(string key, float defaultValue)
        {
            SetString(key, defaultValue.ToString());
        }
        
        public static void SetLong(string key, long defaultValue)
        {
            SetString(key, defaultValue.ToString());
        }
        
        public static void SetBool(string key, bool defaultValue)
        {
            SetString(key,defaultValue.ToString());
        }
        
    }
}