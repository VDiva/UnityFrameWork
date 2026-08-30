using UnityEditor;

/// <summary>
/// Makes newly imported Sprite textures use Single mode by default.
/// Existing import settings and intentional Multiple Sprite sheets are untouched.
/// </summary>
internal sealed class SingleSpriteImportDefaults : AssetPostprocessor
{
    public override int GetPostprocessOrder()
    {
        // Apply after ordinary preprocessors so this project default wins.
        return int.MaxValue;
    }

    private void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing)
        {
            return;
        }

        if (assetImporter is TextureImporter textureImporter)
        {
            textureImporter.spriteImportMode = SpriteImportMode.Single;
        }
    }
}
