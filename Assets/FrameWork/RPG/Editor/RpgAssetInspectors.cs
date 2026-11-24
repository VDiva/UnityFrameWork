using FrameWork.FPS;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace FrameWork.RPG.Editor
{
    // Explicit editors keep LabelText active when an asset is selected in the Project window.
    [CustomEditor(typeof(RpgSkillDefinition)), CanEditMultipleObjects]
    internal sealed class SkillAssetInspector : OdinEditor { }

    [CustomEditor(typeof(RpgSkillExecution), true), CanEditMultipleObjects]
    internal sealed class SkillExecutionAssetInspector : OdinEditor { }

    [CustomEditor(typeof(RpgStatusEffect)), CanEditMultipleObjects]
    internal sealed class StatusEffectAssetInspector : OdinEditor { }

    [CustomEditor(typeof(RpgEnemyDefinition)), CanEditMultipleObjects]
    internal sealed class EnemyAssetInspector : OdinEditor { }

    [CustomEditor(typeof(RpgLevelCurve)), CanEditMultipleObjects]
    internal sealed class LevelCurveAssetInspector : OdinEditor { }

    [CustomEditor(typeof(FpsItemDefinition), true), CanEditMultipleObjects]
    internal sealed class ItemAssetInspector : OdinEditor { }

    [CustomEditor(typeof(FpsAmmoType)), CanEditMultipleObjects]
    internal sealed class AmmoAssetInspector : OdinEditor { }

    [CustomEditor(typeof(FpsWeaponDefinition)), CanEditMultipleObjects]
    internal sealed class WeaponAssetInspector : OdinEditor { }
}
