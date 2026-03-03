using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FrameWork.ChatTool;
using FrameWork.Data;
using UnityEditor;
using UnityEngine;
using XNode;


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

        public static List<ButtonNode> GetAllButtonNodesOutNor(this VideoNode videoNode)
        {
            return videoNode.GetOutputPort("outVideoNode").GetAllButtonNodes();
        }


        public static List<ButtonNode> GetAllButtonNodes(this NodePort node, bool isEditor = false)
        {
            var outData = node.GetOutNode();
            var allBtn = outData.Select((node1 =>
            {
                if (node1 is VideoIf videoIf)
                {
                    var video = videoIf.GetVideoNode();
                    return video.GetInputValue<ButtonNode>("buttonNode");
                }
                else if (node1 is ShowIf showIf)
                {
                    if (isEditor)
                    {
                        return showIf.GetOutputPort("output").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode");
                    }
                    else
                    {
                        var isShow = showIf.IsSuc();
                        if (isShow)
                        {
                            var videos = showIf.GetOutputPort("output").GetAllVideoNodes();
                            var buttonNode = videos[0].GetInputValue<ButtonNode>("buttonNode");
                            return buttonNode;
                        }
                        else
                        {
                            return null;
                        }
                        
                    }

                }
                else if (node1 is VideoIfPlayerEbriety videoIfPlayerEbriety)
                {
                    return videoIfPlayerEbriety.IsSuc() ? videoIfPlayerEbriety.GetOutputPort("outputSuccess").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode") : videoIfPlayerEbriety.GetOutputPort("outputFail").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode");
                }
                else if (node1 is VideoIfTargetEbriety videoIfTargetEbriety)
                {
                    return videoIfTargetEbriety.IsSuc() ? videoIfTargetEbriety.GetOutputPort("outputSuccess").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode") : videoIfTargetEbriety.GetOutputPort("outputFail").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode");
                }
                else if (node1 is ColorSurmise colorSurmise)
                {
                    return colorSurmise.IsSuc() ? colorSurmise.GetOutputPort("outputSuccess").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode") : colorSurmise.GetOutputPort("outputFail").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode");
                }
                else if (node1 is IsWineList isWineList)
                {
                    return isWineList.IsSuc() ? isWineList.GetOutputPort("outputSuccess").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode") : isWineList.GetOutputPort("outputFail").GetAllVideoNodes()[0].GetInputValue<ButtonNode>("buttonNode");
                }
                else
                {
                    var videoNode = node1 as VideoNode;
                    return videoNode.GetInputValue<ButtonNode>("buttonNode");
                }


            })).Where((buttonNode => buttonNode != null)).ToList();

            return allBtn;
        }


        public static List<VideoNode> GetAllVideoNodesOutNor(this VideoNode videoNode)
        {
            return videoNode.GetOutputPort("outVideoNode").GetAllVideoNodes();
        }

        public static List<VideoNode> GetAllVideoNodesOutQueSuc(this VideoNode videoNode)
        {
            return videoNode.GetOutputPort("qteSuc").GetAllVideoNodes();
        }

        public static List<VideoNode> GetAllVideoNodesOutQueLose(this VideoNode videoNode)
        {
            return videoNode.GetOutputPort("qteFail").GetAllVideoNodes();
        }


        public static List<VideoNode> GetAllVideoNodesOutNor(this VideoNode videoNode, string nodeName)
        {
            return videoNode.GetOutputPort(nodeName).GetAllVideoNodes();
        }

        public static List<VideoNode> GetAllVideoNodes(this NodePort node)
        {
            var outData = node.GetOutNode();
            var allBtn = outData.Select((node1 =>
            {
                if (node1 is VideoIf videoIf)
                {

                    return videoIf.GetVideoNode();
                }
                else if (node1 is ShowIf showIf)
                {
                    return showIf.IsSuc() ? (VideoNode)showIf.GetOutputPort("output").GetOutNode()[0] : null;
                }
                else if (node1 is VideoIfPlayerEbriety videoIfPlayerEbriety)
                {
                    return videoIfPlayerEbriety.IsSuc() ? videoIfPlayerEbriety.GetOutputPort("outputSuccess").GetAllVideoNodes()[0] : videoIfPlayerEbriety.GetOutputPort("outputFail").GetAllVideoNodes()[0];
                }
                else if (node1 is VideoIfTargetEbriety videoIfTargetEbriety)
                {
                    return videoIfTargetEbriety.IsSuc() ? videoIfTargetEbriety.GetOutputPort("outputSuccess").GetAllVideoNodes()[0] : videoIfTargetEbriety.GetOutputPort("outputFail").GetAllVideoNodes()[0];
                }
                else if (node1 is ColorSurmise colorSurmise)
                {
                    return colorSurmise.IsSuc() ? colorSurmise.GetOutputPort("outputSuccess").GetAllVideoNodes()[0] : colorSurmise.GetOutputPort("outputFail").GetAllVideoNodes()[0];
                }
                else if (node1 is IsWineList isWineList)
                {
                    return isWineList.IsSuc() ? isWineList.GetOutputPort("outputSuccess").GetAllVideoNodes()[0] : isWineList.GetOutputPort("outputFail").GetAllVideoNodes()[0];
                }
                else
                {
                    return node1 as VideoNode;
                }

            })).Where((videoNode => videoNode != null)).ToList();

            return allBtn;
        }

        public static VideoNode GetEvenFistNode(this VideoGraph videoGraph)
        {
            for (int i = 0; i < videoGraph.nodes.Count; i++)
            {
                if (videoGraph.nodes[i] is ChapterNode)
                {
                    var chapterNode = videoGraph.nodes[i] as ChapterNode;
                    var nodes = chapterNode.GetOutputPort("chapterNode").GetOutNode();
                    return (nodes[0] as VideoNode);
                }
            }

            return null;
        }

        public static ChapterNode GetEvenChapterNode(this VideoGraph videoGraph)
        {
            for (int i = 0; i < videoGraph.nodes.Count; i++)
            {
                if (videoGraph.nodes[i] is ChapterNode)
                {
                    return videoGraph.nodes[i] as ChapterNode;
                }
            }

            return null;
        }

        public static VideoNode GetOutVideo(this ButtonNode buttonNode)
        {
            return buttonNode.GetOutputPort("button").GetAllVideoNodes()[0];
        }

        public static string GetTime(long time)
        {
            System.DateTime startTime =
                System.TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1, 0, 0, 0)); //获取时间戳
            System.DateTime dt = startTime.AddSeconds(time / 1000f);
            string t = dt.ToString("yyyy/MM/dd HH:mm"); //转化为日期时间

            return t;
        }

        
        public static bool IsSuc(this List<PropertyData> list)
        {
            if (list.Count == 0) return true;
            
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];

                if (item.videoIfType == VideoIfType.Gre)
                {
                    if (item.PropertyType is PropertyType.Item)
                    {
                        var v=GameData.GetProperty(item.itemType.ToString(), nameof(PropertyType.Item));
                        if (v<=item.PropertyValue)
                        {
                            return false;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Property)
                    {
                        var v=GameData.GetProperty(item.propertyTypeValue.ToString(), nameof(PropertyType.Property));
                        
                        if (v<=item.PropertyValue)
                        {
                            return false;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Value)
                    {
                        
                        var v=GameData.GetProperty(item.TypeName.ToString(), nameof(PropertyType.Value));
                        
                        if (v<=item.PropertyValue)
                        {
                            return false;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Other)
                    {
                        
                        var v=GameData.GetProperty(item.otherType.ToString(), nameof(PropertyType.Other));
                        
                        if (v<=item.PropertyValue)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (item.PropertyType is PropertyType.Item)
                    {
                        
                        var v=GameData.GetProperty(item.itemType.ToString(), nameof(PropertyType.Item));
                        
                        if (v>= item.PropertyValue)
                        {
                            return false;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Property)
                    {
                        
                        var v=GameData.GetProperty(item.propertyTypeValue.ToString(), nameof(PropertyType.Property));
                        
                        if (v>= item.PropertyValue)
                        {
                            return false;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Value)
                    {
                        var v=GameData.GetProperty(item.TypeName.ToString(), nameof(PropertyType.Value));
                        
                        if (v>= item.PropertyValue)
                        {
                            return false;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Other)
                    {
                        var v=GameData.GetProperty(item.otherType.ToString(), nameof(PropertyType.Value));
                        
                        if (v>= item.PropertyValue)
                        {
                            return false;
                        }
                    }
                }


            }

            return true;
        }
        
        
        
        public static bool IsSucOr(this List<PropertyData> list)
        {
            if (list.Count == 0) return true;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];

                if (item.videoIfType == VideoIfType.Gre)
                {
                    if (item.PropertyType is PropertyType.Item)
                    {
                        var v=GameData.GetProperty(item.itemType.ToString(), nameof(PropertyType.Item));
                        if ( v> item.PropertyValue)
                        {
                            return true;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Property)
                    {
                        var v=GameData.GetProperty(item.propertyTypeValue.ToString(), nameof(PropertyType.Property));
                        
                        if ( v> item.PropertyValue)
                        {
                            return true;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Value)
                    {
                        
                        var v=GameData.GetProperty(item.TypeName.ToString(), nameof(PropertyType.Value));
                        
                        if ( v> item.PropertyValue)
                        {
                            return true;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Other)
                    {
                        
                        var v=GameData.GetProperty(item.otherType.ToString(), nameof(PropertyType.Other));
                        
                        if ( v> item.PropertyValue)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    if (item.PropertyType is PropertyType.Item)
                    {
                        
                        var v=GameData.GetProperty(item.itemType.ToString(), nameof(PropertyType.Item));
                        
                        if (v< item.PropertyValue)
                        {
                            return true;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Property)
                    {
                        
                        var v=GameData.GetProperty(item.propertyTypeValue.ToString(), nameof(PropertyType.Property));
                        
                        if (v< item.PropertyValue)
                        {
                            return true;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Value)
                    {
                        var v=GameData.GetProperty(item.TypeName.ToString(), nameof(PropertyType.Value));
                        
                        if (v< item.PropertyValue)
                        {
                            return true;
                        }
                    }
                    else if (item.PropertyType is PropertyType.Other)
                    {
                        var v=GameData.GetProperty(item.otherType.ToString(), nameof(PropertyType.Value));
                        
                        if (v< item.PropertyValue)
                        {
                            return true;
                        }
                    }
                }


            }

            return false;
        }
        


       
        

        public static void AddTypeValue(this List<PropertyData> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                item.AddTypeValue();
            }
        }
        
        public static void AddTypeValue(this PropertyData propertyData)
        {
            var item = propertyData;
            switch (item.PropertyType)
            {
                case PropertyType.Value:
                    GameData.AddProperty(item.TypeName,item.PropertyValue,item.PropertyType.ToString());
                    break;
                case PropertyType.Item:
                    GameData.AddProperty(item.itemType.ToString(),item.PropertyValue,item.PropertyType.ToString());
                    break;
                case PropertyType.Property:
                    GameData.AddProperty(item.propertyTypeValue.ToString(),item.PropertyValue,item.PropertyType.ToString());
                    break;
                case PropertyType.Other:
                    GameData.AddProperty(item.otherType.ToString(),item.PropertyValue,item.PropertyType.ToString());
                    break;
            }
            
            
        }
        
        
        /// <summary>
        /// 递归获取所有消息
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public static List<ChatNode> GetAllChat(this MessageData msg)
        {
            var abAsset = ABMrg.Load<ChatGraph>(msg.assetName);
            
            List<ChatNode> chatNodes = new List<ChatNode>();

            ChatNode fistNode = null;

            for (int i = 0; i < abAsset.nodes.Count; i++)
            {
                if (abAsset.nodes[i] is StartChat)
                {
                    var startChat = abAsset.nodes[i] as StartChat;
                    if (startChat.maxRound!=-1&&msg.msgType==ChatMsgType.UnRead)
                    {
                        break;
                    }
                    
                    fistNode = (ChatNode)abAsset.nodes[i].GetOutputPort("outPut").GetConnections()[0].node;
                    break;
                }
            }

            if (fistNode != null)
            {
                //chatNodes.Add(fistNode);
                GetChatNodes(fistNode);
            }
            
            
            
            void GetChatNodes(ChatNode chatNode)
            {
                chatNodes.Add(chatNode);
                if (chatNode.isHasBtn&&chatNode.selectNode!=null)
                {
                    // var nodes=chatNode.GetOutputPort("selectBtnNode").GetConnections();
                    // if (nodes.Count>0)
                    // {
                    //     chatNodes.Add((ChatNode)nodes[0].node);
                    //     GetChatNodes((ChatNode)nodes[0].node);
                    // }
                    GetChatNodes(chatNode.selectNode);
                }
                else
                {
                    var nodes=chatNode.GetOutputPort("outPut").GetConnections();
                    if (nodes.Count>0)
                    {
                        GetChatNodes((ChatNode)nodes[0].node);
                    } 
                }
            }
            
            return chatNodes;
        }
        
        
        /// <summary>
        /// 获取第一条信息
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public static ChatNode GetFistNode(this string assetName)
        {
            var abAsset = ABMrg.Load<ChatGraph>(assetName);
            
            ChatNode fistNode = null;
            for (int i = 0; i < abAsset.nodes.Count; i++)
            {
                if (abAsset.nodes[i] is StartChat)
                {
                    fistNode = (ChatNode)abAsset.nodes[i].GetOutputPort("outPut").GetConnections()[0].node;
                    break;
                }
            }
            return fistNode;
        }
        
        /// <summary>
        /// 获取输出的节点
        /// </summary>
        /// <param name="chatNode"></param>
        /// <returns></returns>
        public static ChatNode GetGetOutChatNode(this ChatNode chatNode)
        {
            var nodes = chatNode.GetOutputPort("outPut").GetConnections();
            if (nodes.Count>0)
            {
                return (ChatNode)nodes[0].node;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 递归获取所有消息
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public static List<ChatNode> GetAllChatSelect(this ChatNode node)
        {
            var chatNodes=node.GetOutputPort("selectBtnNode").GetConnections().Select((port =>(ChatNode)port.node )).ToList();
            return chatNodes;
        }
        
    }
}