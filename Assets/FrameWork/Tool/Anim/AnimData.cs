using System;
using UnityEngine;

namespace FrameWork
{
    [Serializable]
    public struct AnimData
    {
        public float strikesTime;
        public AnimationClip animStart;
    }
    
    public struct AnimDatas
    {
        public AnimData[] animDatas;
    }
}