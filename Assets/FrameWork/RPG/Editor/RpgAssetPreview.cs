using System;
using FrameWork.FPS;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FrameWork.RPG.Editor
{
    internal sealed class RpgAssetPreview : IDisposable
    {
        private UnityEditor.Editor modelEditor;
        private GameObject modelTarget;
        public bool IsLoading { get; private set; }

        public void Draw(Object selected)
        {
            IsLoading = false;
            Sprite icon = null;
            GameObject model = null;
            string modelLabel = "模型";

            switch (selected)
            {
                case FpsWeaponDefinition definition:
                    icon = definition.Icon;
                    model = definition.ViewModelPrefab;
                    if (model == null && definition.ProjectilePrefab != null)
                    {
                        model = definition.ProjectilePrefab.transform.root.gameObject;
                        modelLabel = "弹丸模型";
                    }
                    break;
                case FpsWeapon weapon:
                    icon = weapon.Definition != null ? weapon.Definition.Icon : null;
                    model = weapon.Definition != null && weapon.Definition.ViewModelPrefab != null
                        ? weapon.Definition.ViewModelPrefab : weapon.gameObject;
                    break;
                case FpsProjectile projectile:
                    model = EditorUtility.IsPersistent(projectile)
                        ? projectile.transform.root.gameObject : projectile.gameObject;
                    modelLabel = "弹丸模型";
                    break;
                case FpsWeaponItem weaponItem:
                    icon = weaponItem.Icon;
                    if (weaponItem.WeaponPrefab != null) model = weaponItem.WeaponPrefab.gameObject;
                    break;
                case FpsItemDefinition item:
                    icon = item.Icon;
                    break;
            }

            if (modelTarget != model)
            {
                DisposeModelEditor();
                modelTarget = model;
                if (model != null)
                    UnityEditor.Editor.CreateCachedEditor(model, null, ref modelEditor);
            }
            if (icon == null && model == null) return;

            SirenixEditorGUI.BeginBox("外观预览");
            try
            {
                if (icon != null)
                {
                    EditorGUILayout.LabelField("图标", EditorStyles.miniBoldLabel);
                    DrawIcon(icon, GUILayoutUtility.GetRect(0f, 92f, GUILayout.ExpandWidth(true)));
                }
                if (model != null)
                {
                    EditorGUILayout.LabelField(modelLabel, EditorStyles.miniBoldLabel);
                    Rect rect = GUILayoutUtility.GetRect(0f, 160f, GUILayout.ExpandWidth(true));
                    if (modelEditor != null && modelEditor.HasPreviewGUI())
                        modelEditor.OnInteractivePreviewGUI(rect, EditorStyles.helpBox);
                    else
                        DrawThumbnail(model, rect);
                }
            }
            finally { SirenixEditorGUI.EndBox(); }
        }

        private void DrawIcon(Sprite icon, Rect rect)
        {
            var texture = icon.texture;
            if (icon.packed || texture == null)
            {
                DrawThumbnail(icon, rect);
                return;
            }

            DrawBackground(rect);
            Rect region = icon.textureRect;
            if (region.width <= 0f || region.height <= 0f) return;
            float scale = Mathf.Min(rect.width / region.width, rect.height / region.height);
            Vector2 size = region.size * scale;
            var image = new Rect(rect.center - size * 0.5f, size);
            var uv = new Rect(region.x / texture.width, region.y / texture.height,
                region.width / texture.width, region.height / texture.height);
            GUI.DrawTextureWithTexCoords(image, texture, uv, true);
        }

        private void DrawThumbnail(Object asset, Rect rect)
        {
            DrawBackground(rect);
            var image = AssetPreview.GetAssetPreview(asset);
            if (image == null)
            {
                IsLoading |= AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID());
                image = AssetPreview.GetMiniThumbnail(asset);
            }
            if (image != null) GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit, true);
        }

        private static void DrawBackground(Rect rect) => EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.17f, 0.19f) : new Color(0.86f, 0.88f, 0.91f));

        public void Dispose()
        {
            DisposeModelEditor();
            modelTarget = null;
            IsLoading = false;
        }

        private void DisposeModelEditor()
        {
            if (modelEditor != null) Object.DestroyImmediate(modelEditor);
            modelEditor = null;
        }
    }
}
