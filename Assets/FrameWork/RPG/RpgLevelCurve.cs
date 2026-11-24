using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.FPS
{
    [CreateAssetMenu(menuName = "FrameWork/RPG/Level Curve")]
    public sealed class RpgLevelCurve : ScriptableObject
    {
        [Tooltip("每项表示从当前等级升到下一等级需要的经验。")]
        [LabelText("各级升级所需经验")] [SerializeField] private int[] experiencePerLevel = { 100, 200, 350 };

        public int MaxLevel => (experiencePerLevel?.Length ?? 0) + 1;

        public int GetRequiredExperience(int level) =>
            level >= 1 && level < MaxLevel ? Mathf.Max(1, experiencePerLevel[level - 1]) : 0;
    }
}
