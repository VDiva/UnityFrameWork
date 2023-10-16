using GameData;
using UnityEngine;

namespace FrameWork.NetManager.Convert
{
    public static class ConvertData
    {
        public static TransfromData ToTransformData(this Transform transform)
        {
            return new TransfromData()
            {
                PositionData = ToPositionData(transform.position),
                LocalPositionData = ToLocalPositionData(transform.localPosition),
                RotationData = ToRotationData(transform.rotation),
                LocalRotationData = ToLocalRotationData(transform.localRotation),
                LocalScaleData = ToLocalScaleData(transform.localScale)
            };
        }

        private static PositionData _pos;
        public static PositionData ToPositionData(this Vector3 position)
        {
            return new PositionData() { Vector3Data = position.ToVector3Data() };
        }
        
        private static LocalPositionData _localPos;
        public static LocalPositionData ToLocalPositionData(this Vector3 position)
        {
            return new LocalPositionData() { Vector3Data = position.ToVector3Data() };
        }
        

        private static RotationData _ros;

        public static RotationData ToRotationData(this Quaternion quaternion)
        {
            return new RotationData() { Vector3Data = quaternion.eulerAngles.ToVector3Data() };
        }
        
       

        public static LocalRotationData ToLocalRotationData(this Quaternion quaternion)
        {
            return new LocalRotationData() { Vector3Data = quaternion.eulerAngles.ToVector3Data() };
        }

       
        public static LocalScaleData ToLocalScaleData(this Vector3 scale)
        {
            
            return new LocalScaleData(){Vector3Data = scale.ToVector3Data()};
        }

        
        
        public static Vector3Data ToVector3Data(this Vector3 vec3)
        {
            return new Vector3Data()
            {
                X = vec3.x,
                Y = vec3.y,
                Z = vec3.z

            };
        }

        public static Vector2Data ToVector2Data(this Vector2Data vec2)
        {
            return new Vector2Data()
            {
                X = vec2.X,
                Y = vec2.Y,
            };
        }
        

        public static Vector3 ToVector3(this Vector3Data vec3)
        {
            return new Vector3()
            {
                x = vec3.X,
                y = vec3.Y,
                z = vec3.Z

            };
        }

        public static Vector2 ToVector2(this Vector2Data vec2)
        {
            return new Vector2()
            {
                x = vec2.X,
                y = vec2.Y
            };
        }
    }
}