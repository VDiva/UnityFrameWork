using FrameWork.FPS;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace FrameWork.RPG.Editor
{
    internal static class RpgWeaponInspectorLayout
    {
        internal static void DrawAttack(SerializedObject settings, bool melee)
        {
            SirenixEditorGUI.BeginBox("攻击参数");
            try
            {
                Field(settings, "damage", "伤害");
                Field(settings, "range", melee ? "近战距离" : "射程");
                Field(settings, "shotInterval", "攻击间隔（秒）");
                if (melee) Field(settings, "meleeRadius", "近战判定半径");
                else if (settings.FindProperty("projectilePrefab")?.objectReferenceValue != null)
                    Field(settings, "projectileSpeed", "弹丸速度");
            }
            finally { SirenixEditorGUI.EndBox(); }
        }

        internal static void DrawDefinition(SerializedObject settings)
        {
            SirenixEditorGUI.BeginBox("基础信息");
            try
            {
                Field(settings, "displayName", "显示名称");
                Field(settings, "kind", "武器类型");
                Field(settings, "icon", "图标");
                Field(settings, "viewModelPrefab", "武器模型预制体");
                Field(settings, "animatorController", "动画控制器");
            }
            finally { SirenixEditorGUI.EndBox(); }

            if (settings.FindProperty("kind").enumValueIndex == (int)FpsWeaponKind.Melee) return;

            SirenixEditorGUI.BeginBox("枪械与弹药");
            try
            {
                Field(settings, "fireMode", "射击模式");
                Field(settings, "ammoType", "弹种");
                Field(settings, "magazineSize", "弹匣容量");
                Field(settings, "startingReserve", "初始备弹");
                Field(settings, "reserveCapacity", "备弹上限");
                Field(settings, "projectilePrefab", "弹丸预制体");
            }
            finally { SirenixEditorGUI.EndBox(); }

            SirenixEditorGUI.BeginBox("换弹");
            try
            {
                Field(settings, "autoReload", "自动换弹");
                Field(settings, "reloadCompletion", "换弹完成方式");
                if (settings.FindProperty("reloadCompletion").enumValueIndex == (int)FpsReloadCompletion.Timed)
                    Field(settings, "reloadDuration", "换弹时长（秒）");
            }
            finally { SirenixEditorGUI.EndBox(); }
        }

        internal static void DrawInstance(SerializedObject settings)
        {
            SirenixEditorGUI.BeginBox("场景武器");
            try
            {
                Field(settings, "definition", "武器配置");
                Field(settings, "muzzle", "枪口");
                Field(settings, "hitLayers", "命中层");
                if (settings.FindProperty("definition").objectReferenceValue != null) return;

                Field(settings, "automatic", "自动射击");
                Field(settings, "magazineSize", "弹匣容量");
                Field(settings, "startingReserve", "初始备弹");
                Field(settings, "reloadDuration", "换弹时长（秒）");
            }
            finally { SirenixEditorGUI.EndBox(); }
        }

        private static void Field(SerializedObject settings, string name, string label)
        {
            EditorGUILayout.PropertyField(settings.FindProperty(name), new GUIContent(label));
        }
    }
}
