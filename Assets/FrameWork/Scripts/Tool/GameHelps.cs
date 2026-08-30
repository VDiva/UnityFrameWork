using UnityEngine;

namespace FrameWork.Script.Tool
{
    public static class GameHelps
    {
        public static Vector3 GetRndomPo(this Vector3 pos, float range)
        {
            return new Vector3(Random.Range(pos.x-range, pos.x + range), Random.Range(pos.y-range, pos.y + range),0);
        }
    }
}