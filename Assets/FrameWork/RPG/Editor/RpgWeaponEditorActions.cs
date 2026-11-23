using System.IO;
using FrameWork.FPS;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FrameWork.RPG.Editor
{
    internal static class RpgWeaponEditorActions
    {
        internal static void DrawActions(FpsWeaponDefinition weapon, ref FpsLoadout loadout)
        {
            EditorGUILayout.Space(8f);
            SirenixEditorGUI.BeginBox("武器动画树");
            try
            {
                if (weapon.AnimatorController == null)
                {
                    if (weapon.Kind == FpsWeaponKind.Firearm && weapon.ReloadCompletion == FpsReloadCompletion.External)
                        EditorGUILayout.HelpBox("外部完成模式需要动画事件或代码回调。", MessageType.Warning);
                    if (GUILayout.Button("创建并绑定动画树模板", GUILayout.Height(26f)))
                        EditorApplication.delayCall += () => { if (weapon != null) CreateController(weapon); };
                }
                else if (GUILayout.Button("打开 Animator 动画树", GUILayout.Height(26f)))
                {
                    var controller = weapon.AnimatorController;
                    EditorApplication.delayCall += () => { if (controller != null) AssetDatabase.OpenAsset(controller); };
                }
            }
            finally { SirenixEditorGUI.EndBox(); }

            EditorGUILayout.Space(8f);
            SirenixEditorGUI.BeginBox("场景装备栏");
            try
            {
                loadout = (FpsLoadout)EditorGUILayout.ObjectField("目标装备栏", loadout, typeof(FpsLoadout), true);
                using var disabled = new EditorGUI.DisabledScope(!CanEditLoadout(loadout));
                if (GUILayout.Button("添加武器到装备栏", GUILayout.Height(26f)))
                {
                    var target = loadout;
                    EditorApplication.delayCall += () =>
                    {
                        if (weapon != null && CanEditLoadout(target)) AddToLoadout(weapon, target);
                    };
                }
            }
            finally { SirenixEditorGUI.EndBox(); }
        }

        private static bool CanEditLoadout(FpsLoadout loadout) => !Application.isPlaying &&
            loadout != null && loadout.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(loadout);

        private static void CreateController(FpsWeaponDefinition weapon)
        {
            string path = EditorUtility.SaveFilePanelInProject("创建武器动画树", weapon.name + " Animator",
                "controller", "选择 Animator Controller 的保存位置。");
            if (!CanCreate(path)) return;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null) return;
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle", new Vector3(200f, 0f));
            machine.defaultState = idle;

            if (weapon.Kind == FpsWeaponKind.Melee)
            {
                AddAction(controller, machine, idle, FpsWeaponAnimator.MeleeTrigger, 100f);
            }
            else
            {
                AddAction(controller, machine, idle, FpsWeaponAnimator.ReloadTrigger, 100f);
                AddAction(controller, machine, idle, FpsWeaponAnimator.FireTrigger, 160f);
            }
            AddAction(controller, machine, idle, FpsWeaponAnimator.EquipTrigger, 220f);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            var settings = new SerializedObject(weapon);
            settings.FindProperty("animatorController").objectReferenceValue = controller;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(weapon);
            AssetDatabase.OpenAsset(controller);
        }

        private static void AddAction(AnimatorController controller, AnimatorStateMachine machine,
            AnimatorState idle, string trigger, float y)
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            var state = machine.AddState(trigger, new Vector3(420f, y));
            var enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.05f;
            enter.canTransitionToSelf = trigger == FpsWeaponAnimator.FireTrigger ||
                trigger == FpsWeaponAnimator.MeleeTrigger;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);

            var exit = state.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = 1f;
            exit.hasFixedDuration = true;
            exit.duration = 0.05f;
        }

        private static void AddToLoadout(FpsWeaponDefinition definition, FpsLoadout loadout)
        {
            var settings = new SerializedObject(loadout);
            var slots = settings.FindProperty("weapons");
            int freeSlot = -1;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var current = slots.GetArrayElementAtIndex(i).objectReferenceValue as FpsWeapon;
                if (current != null && current.Definition == definition)
                {
                    EditorUtility.DisplayDialog("添加武器", "此装备栏已绑定该武器配置。", "确定");
                    return;
                }
                if (current == null && freeSlot < 0) freeSlot = i;
            }

            int limit = Mathf.Max(1, settings.FindProperty("slotLimit").intValue);
            if (freeSlot < 0 && slots.arraySize >= limit)
            {
                EditorUtility.DisplayDialog("添加武器", "装备栏已满，可先调整 Slot Limit。", "确定");
                return;
            }

            var mount = settings.FindProperty("weaponMount").objectReferenceValue as Transform;
            if (mount == null || !mount.IsChildOf(loadout.transform)) mount = loadout.transform;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("添加 RPG 武器");
            var instance = new GameObject(definition.DisplayName);
            Undo.RegisterCreatedObjectUndo(instance, "创建武器节点");
            Undo.SetTransformParent(instance.transform, mount, "绑定武器挂点");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            if (definition.ViewModelPrefab != null &&
                PrefabUtility.InstantiatePrefab(definition.ViewModelPrefab, instance.transform) is GameObject model)
                Undo.RegisterCreatedObjectUndo(model, "添加武器模型");

            var component = Undo.AddComponent<FpsWeapon>(instance);
            var weapon = new SerializedObject(component);
            weapon.FindProperty("definition").objectReferenceValue = definition;
            weapon.FindProperty("muzzle").objectReferenceValue = instance.transform;
            weapon.ApplyModifiedProperties();
            if (freeSlot < 0)
            {
                freeSlot = slots.arraySize;
                slots.arraySize++;
            }
            slots.GetArrayElementAtIndex(freeSlot).objectReferenceValue = component;
            settings.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
        }

        internal static bool CanCreate(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return true;

            EditorUtility.DisplayDialog("文件已存在", "请选择一个新的文件名。", "确定");
            return false;
        }
    }
}
