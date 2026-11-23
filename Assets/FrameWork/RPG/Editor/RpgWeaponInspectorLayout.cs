using System;
using System.Collections.Generic;
using System.Reflection;
using FrameWork.FPS;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace FrameWork.RPG.Editor
{
    public sealed class RpgWeaponInspectorLayout : OdinAttributeProcessor<FpsWeaponDefinition>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty,
            MemberInfo member, List<Attribute> attributes)
        {
            (string label, string group, int order, FpsWeaponKind? kind) layout = member.Name switch
            {
                "displayName" => ("显示名称", "基础信息", 0, null),
                "icon" => ("图标", "基础信息", 0, null),
                "kind" => ("武器类型", "基础信息", 0, null),
                "viewModelPrefab" => ("武器模型预制体", "基础信息", 0, null),
                "damage" => ("伤害", "攻击参数", 1, null),
                "range" => ("有效距离", "攻击参数", 1, null),
                "shotInterval" => ("攻击间隔（秒）", "攻击参数", 1, null),
                "meleeRadius" => ("近战判定半径", "攻击参数", 1, FpsWeaponKind.Melee),
                "ammoType" => ("弹种", "枪械与弹药", 2, FpsWeaponKind.Firearm),
                "fireMode" => ("射击模式", "枪械与弹药", 2, FpsWeaponKind.Firearm),
                "magazineSize" => ("弹匣容量", "枪械与弹药", 2, FpsWeaponKind.Firearm),
                "reserveCapacity" => ("备弹上限", "枪械与弹药", 2, FpsWeaponKind.Firearm),
                "startingReserve" => ("初始备弹", "枪械与弹药", 2, FpsWeaponKind.Firearm),
                "autoReload" => ("自动换弹", "换弹", 3, FpsWeaponKind.Firearm),
                "reloadCompletion" => ("换弹完成方式", "换弹", 3, FpsWeaponKind.Firearm),
                "reloadDuration" => ("换弹时长（秒）", "换弹", 3, FpsWeaponKind.Firearm),
                "projectilePrefab" => ("弹丸预制体", "弹丸", 4, FpsWeaponKind.Firearm),
                "projectileSpeed" => ("弹丸速度", "弹丸", 4, FpsWeaponKind.Firearm),
                "animatorController" => ("Animator Controller", "动画配置", 5, null),
                _ => (null, null, 0, null)
            };

            var (label, group, order, kind) = layout;
            if (label == null) return;
            attributes.Add(new LabelTextAttribute(label));
            attributes.Add(new BoxGroupAttribute(group, order: order));
            if (kind.HasValue) attributes.Add(new ShowIfAttribute(nameof(FpsWeaponDefinition.Kind), kind.Value));
        }
    }
}
