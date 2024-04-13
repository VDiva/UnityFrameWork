using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork
{
    [Serializable]
    public struct AnimLayerData
    {
        public List<AnimationClip> AnimData;
        public AvatarMask AvatarMask;
    }
}