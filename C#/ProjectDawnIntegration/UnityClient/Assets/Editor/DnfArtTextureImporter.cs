using UnityEditor;
using UnityEngine;

/// <summary>Preserves decoded DNF canvas dimensions and alpha for GUI rendering.</summary>
public sealed class DnfArtTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Art/", System.StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.compressionQuality = 100;
        importer.sRGBTexture = true;
        importer.isReadable = false;
    }
}
