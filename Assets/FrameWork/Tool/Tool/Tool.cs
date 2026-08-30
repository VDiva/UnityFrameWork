using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using LitJson;
using UnityEngine;


namespace FrameWork
{
    public static class Tool
    {
        public static string GetAbName(string name)
        {
            return GetMd5AsString(name);
        }

        public static object ConversionType(string type, string value)
        {
            if (type == "int")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0;
                }

                return int.Parse(value);
            }
            else if (type == "string")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return "";
                }

                return value;
            }
            else if (type == "float")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0f;
                }

                return float.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (type == "long")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0;
                }

                return long.Parse(value);
            }
            else if (type == "double")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0f;
                }

                return double.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (type == "Vector3")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                var v = value.Split(',');
                return new Vector3(float.Parse(v[0], CultureInfo.InvariantCulture),
                    float.Parse(v[1], CultureInfo.InvariantCulture), float.Parse(v[2], CultureInfo.InvariantCulture));
            }
            else if (type == "Vector2")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                var v = value.Split(',');
                return new Vector2(float.Parse(v[0], CultureInfo.InvariantCulture),
                    float.Parse(v[1], CultureInfo.InvariantCulture));
            }
            else if (type == "string[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new string[] { };
                else
                    return value.Split(',').Select((s =>s.Trim())).ToArray();
            }
            else if (type == "int[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new int[] { };
                else
                    return value.Split(',').Select((s => int.Parse(s,CultureInfo.InvariantCulture))).ToArray();

            }
            else if (type == "float[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new float[] { };
                else
                    return value.Split(',').Select((s => float.Parse(s, CultureInfo.InvariantCulture))).ToArray();
            }

            return value;
        }

        public static string GetMd5(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] md5Info = md5.ComputeHash(fs);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < md5Info.Length; i++)
                    sb.Append(md5Info[i].ToString("x2"));
                return sb.ToString();
            }
        }


        public static string GetMd5AsString(string key)
        {
            return GetShortIdentity(key);
            // MD5 md5 = new MD5CryptoServiceProvider();
            // byte[] md5Info = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            // StringBuilder sb = new StringBuilder();
            // for (int i = 0; i < md5Info.Length; i++)
            //     sb.Append(md5Info[i].ToString("x2"));
            // return sb.ToString();
        }

        public static string GetShortIdentity(string key)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));

                // 只取前 8 个字节 (64 bit)
                long lowPart = BitConverter.ToInt64(hashBytes, 0);
                ulong value = (ulong)Math.Abs(lowPart);

                // Base62 编码
                string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
                char[] chars = new char[11]; // 64位在Base62下最多11位
                int index = 0;
                while (value > 0)
                {
                    chars[index++] = alphabet[(int)(value % 62)];
                    value /= 62;
                }

                return new string(chars, 0, index);
            }
        }


        public static long ConvertDateTimep(DateTime time)
        {
            return ((time.ToUniversalTime().Ticks - 621355968000000000) / 10000000);
            //等价于：
            //return ((time.ToUniversalTime().Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks) / 10000000) * 1000;
        }


        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="toEncryptArray">明文</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static byte[] Encrypt(byte[] toEncryptArray, string key)
        {
            //byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);        
            byte[] keyArray = Convert.FromBase64String(key);
            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = keyArray;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;
            ICryptoTransform cTransform = rDel.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return resultArray;
        }



        /// <summary>
        /// AES解密
        /// </summary>
        /// <param name="toEncryptArray">密文</param>
        /// <param name="key">密钥</param>
        /// <returns></returns>
        public static byte[] Decrypt(byte[] toEncryptArray, string key)
        {
            byte[] keyArray = Convert.FromBase64String(key);
            RijndaelManaged rDel = new RijndaelManaged();
            rDel.Key = keyArray;
            rDel.Mode = CipherMode.ECB;
            rDel.Padding = PaddingMode.PKCS7;
            ICryptoTransform cTransform = rDel.CreateDecryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            return resultArray;
        }



        public static string GetPathMd5(string value)
        {
            var dir = Path.GetDirectoryName(value);
            var fileName = GetMd5AsString(Path.GetFileName(value));

            var dirNameList = dir.Split("\\").Where((s => !string.IsNullOrEmpty(s))).Select(GetMd5AsString).ToList();
            var path = "";
            for (int i = 0; i < dirNameList.Count; i++)
            {
                path += "/" + dirNameList[i];
            }

            path += "/" + fileName;

            return path;
        }

        public static string GetVideoPath(string videoName, bool isNor = true)
        {
            var path = Application.dataPath + "/";
// #if UNITY_EDITOR
//             if (isNor)
//                 path += "Video/"+videoName+".mp4";
//             else
//                 path += "Video/"+videoName+".mp4";
// #else
//             path = Application.streamingAssetsPath + "/";
//             if (isNor)
//                 path += $"StreamingAssets/{Tool.GetMd5AsString("Video")}/{GetPathMd5(videoName)}.Png";
//             else
//                 path += $"StreamingAssets/{Tool.GetMd5AsString("Video")}/{GetPathMd5(videoName)}.Png";
// #endif


            path = Application.streamingAssetsPath + "/";
            if (isNor)
                path += $"{GetMd5AsString("Video")}/{GetPathMd5(videoName)}.Png";
            else
                path += $"{GetMd5AsString("Video")}/{GetPathMd5(videoName)}.Png";

            return path;
        }


        public static string Encrypt(string toEncrypt)
        {
            return Encrypt(toEncrypt, Key);
        }

        public static string Decrypt(string toEncrypt)
        {
            return Decrypt(toEncrypt, Key);
        }

        public static string Key => "kljsdkkdlo4454GG00155sajuklmbkdl";


        public static string GetFileDecryptName(string fileName, string end = ".Png")
        {
            return Encrypt(fileName) + end;
        }




        // 加密方法
        public static string Encrypt(string plainText, string secretKey)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            if (string.IsNullOrEmpty(secretKey)) throw new ArgumentException("Key cannot be empty");

            // 1. 准备 Key 和 IV (为了简化，这里使用 Key 的 MD5 值同时作为 Key 和 IV)
            // 实际生产中建议 IV 随机生成并拼接到密文中，但为了保证只输出字母数字，固定 IV 比较好处理
            byte[] keyBytes = GetMD5Hash(secretKey);
            byte[] ivBytes = keyBytes; // 这里复用 Key 作为 IV，或者你可以指定另一个固定的 16字节数组

            byte[] encryptedBytes;

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC; // CBC 模式比较安全
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }

                        encryptedBytes = ms.ToArray();
                    }
                }
            }

            // 2. 将二进制转换为 16进制字符串 (只包含 0-9, A-F)
            return BytesToHexString(encryptedBytes);
        }

        // 解密方法
        public static string Decrypt(string encryptedHexString, string secretKey)
        {
            if (string.IsNullOrEmpty(encryptedHexString)) return "";
            if (string.IsNullOrEmpty(secretKey)) throw new ArgumentException("Key cannot be empty");

            try
            {
                // 1. 将 16进制字符串 转回 二进制
                byte[] cipherTextBytes = HexStringToBytes(encryptedHexString);

                // 2. 准备 Key 和 IV
                byte[] keyBytes = GetMD5Hash(secretKey);
                byte[] ivBytes = keyBytes;

                string plaintext = null;

                using (Aes aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream ms = new MemoryStream(cipherTextBytes))
                    {
                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader sr = new StreamReader(cs))
                            {
                                plaintext = sr.ReadToEnd();
                            }
                        }
                    }
                }

                return plaintext;
            }
            catch (Exception)
            {
                // 解密失败（通常是 Key 不对或者字符串被篡改）
                return "解密失败：Key 错误或密文损坏";
            }
        }

        // 辅助：计算 MD5 (用于将任意长度的 Key 变成固定的 16字节数组)
        private static byte[] GetMD5Hash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }

        // 辅助：字节数组转 16进制字符串 (结果类似 "4A12B...")
        private static string BytesToHexString(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2")); // X2 表示两位大写十六进制
            }

            return sb.ToString();
        }

        // 辅助：16进制字符串转字节数组
        private static byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex string length must be even.");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        public static bool InCircle(int x, int y, Vector2Int circleCenter, int r)
        {
            return Mathf.Pow(x - circleCenter.x, 2) + Mathf.Pow(y - circleCenter.y, 2) < Mathf.Pow(r, 2);
        }

        public static bool IsInSquare(int x, int y, int width, int height, int curX, int curY)
        {
            return curX >= x - width / 2 && curX <= x + width / 2 && curY >= y - height / 2 && curY <= y + height / 2;
        }

        public static Vector2 GetTargetLocalLoc(RectTransform target, Vector3 pos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(target, pos, null, out var position);
            return position;
        }

        public static bool IsAndroid()
        {
            return CheckPlatform(RuntimePlatform.Android);
        }

        public static bool IsWindows()
        {
            return CheckPlatform(RuntimePlatform.WindowsEditor) || CheckPlatform(RuntimePlatform.WindowsPlayer);
        }

        public static bool CheckPlatform(RuntimePlatform runtimePlatform)
        {
            return Application.platform == runtimePlatform;
        }


        public static Type ByClassNameGetType(string className)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetType($"FrameWork.{className}.{className}");
            return type;
        }

        public static void HideAllChild(Transform transform)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).SetActive(false);
            }
        }
        
        public static string GetTime(long time)
        {
            System.DateTime startTime =
                System.TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0)); //获取时间戳
            System.DateTime dt = startTime.AddSeconds(time / 1000f);
            string t = dt.ToString("yyyy/MM/dd HH:mm"); //转化为日期时间

            return t;
        }

        public static ByteString ToByteString(this object obj)
        {
            return ByteString.CopyFromUtf8(JsonMapper.ToJson(obj));
        }

        public static T ToObject<T>(this ByteString bytes)
        {
            return JsonMapper.ToObject<T>(bytes.ToStringUtf8());
        }


        /// <summary>
        /// 获取转向一个物体的角度默认自己朝上
        /// </summary>
        /// <param name="self"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static Vector3 ToTargetAngel(this Vector3 self, Vector3 target)
        {
            // 计算从自身指向目标的方向向量
            Vector2 directionToTarget = (target - self).normalized;
            // 计算当前朝上方向与目标方向的夹角（带符号）
            float angle = Vector2.SignedAngle(Vector2.up, directionToTarget);
            return new Vector3(0, 0, angle);
        }

        /// <summary>
        /// 设置自己朝向一个物体
        /// </summary>
        /// <param name="self"></param>
        /// <param name="target"></param>
        public static void ToTargetAngel(this Transform self, Transform target)
        {
            var angle=self.localPosition.ToTargetAngel(target.localPosition);
            self.eulerAngles = angle;
        }


        public static string FormatChinese(this float number, int decimalPlaces=1)
        {
            return FormatChinese((decimal)number, decimalPlaces);
        }
        
        public static string FormatChinese(this double number, int decimalPlaces=1)
        {
            return FormatChinese((decimal)number, decimalPlaces);
        }
        
        public static string FormatChinese(this int number, int decimalPlaces=1)
        {
            return FormatChinese((decimal)number, decimalPlaces);
        }
        
        public static string FormatChinese(this long number, int decimalPlaces=1)
        {
            return FormatChinese((decimal)number, decimalPlaces);
        }
        
        public static string FormatChinese(this decimal number, int decimalPlaces=1)
        {
            if (number < 0)
                return "-" + FormatChinese(-number, decimalPlaces);

            // 万亿（10^12）
            if (number >= 1000000000000m)
            {
                decimal result = number / 1000000000000m;
                return result.ToString("F" + decimalPlaces) + "万亿";
            }
            // 亿（10^8）
            else if (number >= 100000000m)
            {
                decimal result = number / 100000000m;
                return result.ToString("F" + decimalPlaces) + "亿";
            }
            // 万（10^4）
            else if (number >= 10000m)
            {
                decimal result = number / 10000m;
                return result.ToString("F" + decimalPlaces) + "万";
            }
            else
            {
                // 小于1万直接显示，如果是整数则不显示小数
                if (number == Math.Floor(number))
                    return ((long)number).ToString();
                else
                    return number.ToString("F" + decimalPlaces);
            }
        }
        
        /// <summary>
        ///使用示例 var list = new List<int> { 1, 2, 3, 4, 5, 6 }; var result1 = list.GetListRange(2..5);  // 返回 [3, 4, 5] var result2 = list.GetListRange(^3..^1); // 返回 [4, 5]（从倒数第3到倒数第1）
        /// </summary>
        /// <param name="list"></param>
        /// <param name="range"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static List<T> GetListRange<T>(this IList<T> list, Range range)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            // Range.GetOffsetAndLength 会在范围超过集合边界时抛出异常，
            // 这里将两端裁剪到有效边界，使短列表也能安全取值。
            int start = range.Start.IsFromEnd
                ? list.Count - range.Start.Value
                : range.Start.Value;
            int end = range.End.IsFromEnd
                ? list.Count - range.End.Value
                : range.End.Value;

            start = Math.Max(0, Math.Min(start, list.Count));
            end = Math.Max(0, Math.Min(end, list.Count));
            int length = Math.Max(0, end - start);
            var result = new List<T>(length);

            for (int i = 0; i < length; i++)
            {
                result.Add(list[start + i]);
            }

            return result;
        }
        
        public static T ToEnum<T>(this int value)
        {
            return (T)Enum.ToObject(typeof(T), value);
        }

        public static void CheckAdd(this Dictionary<string, int> dict, string key, int value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, value);
            }
            else
            {
                var v = dict[key];
                dict[key]= (v+value);
            }
        }
        
        public static void CheckAdd(this Dictionary<string, string> dict, string key, string value)
        {
            dict[key] = value;
        }
        
        
        private static DateTime _startDate = new DateTime(2026, 8, 17);
        /// <summary>
        /// 获取从起始日期到今天的累计天数和周数
        /// </summary>
        public static void GetAccumulatedDaysAndWeeks(out int totalDays, out int totalWeeks)
        {
            DateTime today = DateTime.Now;
        
            // 计算累计天数
            TimeSpan diff = today - _startDate;
            totalDays = diff.Days; // 总天数（不包括今天）或 +1 包括今天
        
            // 计算累计周数（向下取整）
            totalWeeks = totalDays / 7;
        
            // 如果需要包括今天，取消注释下一行
            // totalDays += 1;
        }

        public static int GetDay()
        {
            DateTime today = DateTime.Now;
        
            // 计算累计天数
            TimeSpan diff = today - _startDate;
            return diff.Days; // 总天数（不包括今天）或 +1 包括今天
        }
        
        public static int GetWeek()
        {
            DateTime today = DateTime.Now;
            // 计算累计天数
            TimeSpan diff = today - _startDate;
            return diff.Days / 7;; // 总天数（不包括今天）或 +1 包括今天
        }
    
        public static T GetListIndex<T>(this List<T> list, int index)
        {
            if (index>=list.Count)
            {
                return list.Last();
            }
            else
            {
                return list[index];
            }
        }
        
        public static T GetListIndex<T>(this T[] list, int index)
        {
            if (list.Length<=0)
            {
                return default(T);
            }
            if (index>=list.Length)
            {
                return list.Last();
            }
            else
            {
                return list[index];
            }
        }

        public static string ToBfb(this float v,bool e=true)
        {
            if (e)
            {
                return (v * 100)+ "%";
            }
            else
            {
                return v + "";
            }
            
        }
        
        static Dictionary<string,string> _xlsxDic=new Dictionary<string, string>();
        public static async UniTask LoadAllXlsx()
        {
            var allXlsx = await ABMrg.LoadAsync<TextAsset>("AllXlsx");
            var list = JsonMapper.ToObject<List<string>>(allXlsx.text);
            for (int i = 0; i < list.Count; i++)
            {
                var text=await ABMrg.LoadAsync<TextAsset>(list[i]);
                _xlsxDic.TryAdd(list[i],text.text);
            }
        }
        
        public static string LoadXlsx(string key)
        {
            return _xlsxDic.GetValueOrDefault(key, "");
        }
    }
}
