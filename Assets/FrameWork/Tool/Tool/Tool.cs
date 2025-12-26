using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;


namespace FrameWork
{
    public static class Tool
    {
        
        public static string GetAbName(string name)
        {
            return GetMd5AsString(name);
        }

        public static object ConversionType(string type,string value)
        {
            if (type=="int")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0;
                }
                return int.Parse(value);
            }else if (type=="string")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return "";
                }
                return value;
            }else if (type=="float")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0f;
                }
                return float.Parse(value,CultureInfo.InvariantCulture);
            }else if (type=="long")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0;
                }
                return long.Parse(value);
            }else if (type=="double")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return 0f;
                }
                return double.Parse(value,CultureInfo.InvariantCulture);
            }else if (type=="Vector3")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                var v = value.Split(',');
                return new Vector3(float.Parse(v[0],CultureInfo.InvariantCulture),float.Parse(v[1],CultureInfo.InvariantCulture),float.Parse(v[2],CultureInfo.InvariantCulture));
            }else if (type=="Vector2")
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }
                var v = value.Split(',');
                return new Vector2(float.Parse(v[0],CultureInfo.InvariantCulture),float.Parse(v[1],CultureInfo.InvariantCulture));
            }else if (type=="string[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new string[]{};
                else
                    return value.Split(',');
            }else if (type=="int[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new int[]{};
                else
                    return value.Split(',').Select((s => int.Parse(s) )).ToArray();
                    
            }else if (type=="float[]")
            {
                if (string.IsNullOrEmpty(value))
                    return new float[]{};
                else
                    return value.Split(',').Select((s => float.Parse(s,CultureInfo.InvariantCulture) )).ToArray();
            }

            return value;
        }
        
        public static string GetMd5(string path)
        {
            using (FileStream fs=new FileStream(path,FileMode.Open))
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
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] md5Info = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < md5Info.Length; i++)
                sb.Append(md5Info[i].ToString("x2"));
            return sb.ToString();
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
        
        
        
        public static string Encrypt(string toEncrypt)
        {
            return Encrypt(toEncrypt, Key);
        }
        
        public static string Decrypt(string toEncrypt)
        {
            return Decrypt(toEncrypt, Key);
        }

        public static string Key => "kljsdkkdlo4454GG00155sajuklmbkdl";

        
        public static string GetFileDecryptName(string fileName, string end=".Png")
        {
            return Encrypt(fileName)+end;
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
        
        public static bool IsInSquare(int x, int y, int width, int height,int curX,int curY)
        {
            return curX >= x - width / 2 && curX <= x + width / 2 && curY >= y - height / 2 && curY <= y + height / 2;
        }
    
        public static Vector2 GetTargetLocalLoc(RectTransform target,Vector3 pos)
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
            var assembly=Assembly.GetExecutingAssembly();
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
    }
}