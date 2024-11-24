using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.Data
{
    public static class GameData
    {
        private static Dictionary<object, object> _dataDic = new Dictionary<object, object>();

        public static void SetString(object key, object value)
        {
            _dataDic[key] = value;
            PlayerPrefs.SetString(key.ToString(),value.ToString());
        }
        
        public static void AddValue(object key, object value)
        {
            _dataDic[key] = value;
            PlayerPrefs.SetString(key.ToString(),value.ToString());
        }
        
        public static T GetValue<T>(object key, object defaultValue) where T : Object
        {
            if (!_dataDic.ContainsKey(key.ToString()))
                _dataDic.Add(key.ToString(),PlayerPrefs.GetString(key.ToString(),defaultValue.ToString()));
            return (T)_dataDic[key.ToString()];
        }

        
        
        
    }
}