using FrameWork.FPS;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork.RPG.Editor
{
    internal static class RpgAssetCreation
    {
        internal static Object CreateWeapon(FpsWeaponKind kind)
        {
            string name = kind == FpsWeaponKind.Melee ? "新近战武器" : "新枪械";
            return CreateAsset<FpsWeaponDefinition>(name, weapon =>
            {
                using var settings = new SerializedObject(weapon);
                settings.FindProperty("kind").enumValueIndex = (int)kind;
                settings.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        internal static T CreateAsset<T>(string name, System.Action<T> initialize = null) where T : ScriptableObject
        {
            string path = EditorUtility.SaveFilePanelInProject("创建配置资产", name, "asset", "选择配置资产的保存位置。");
            if (!RpgWeaponEditorActions.CanCreate(path)) return null;

            var asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static Object CreateProjectile()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建弹丸预制体", "新弹丸", "prefab", "选择弹丸预制体的保存位置。");
            if (!RpgWeaponEditorActions.CanCreate(path)) return null;

            var root = new GameObject("Projectile");
            try
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(0.08f, 0.08f, 0.2f);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                root.AddComponent<FpsProjectile>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return prefab != null ? prefab.GetComponent<FpsProjectile>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
