using UnityEditor;

/// <summary>
/// Makes newly imported Sprite textures use Single mode by default when no explicit mode is set.
/// Non-sprite textures, existing import settings, and intentional Multiple Sprite sheets are untouched.
/// </summary>
internal sealed class SingleSpriteImportDefaults : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing)
        {
            return;
        }

        if (assetImporter is TextureImporter textureImporter &&
            textureImporter.textureType == TextureImporterType.Sprite &&
            textureImporter.spriteImportMode == SpriteImportMode.None)
        {
            textureImporter.spriteImportMode = SpriteImportMode.Single;
        }
    }
}
