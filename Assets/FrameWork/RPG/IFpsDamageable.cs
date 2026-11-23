using UnityEngine;

namespace FrameWork.FPS
{
    public interface IFpsDamageable
    {
        void ApplyDamage(float amount, GameObject instigator);
    }
}
