using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Kimchily.Networking.Editor
{
    [InitializeOnLoad]
    public sealed class NetworkingResources : IPreprocessBuildWithReport
    {
        const string Root = "Packages/com.kimchily.networking/Runtime/Resources/";
        public int callbackOrder => -1000;
        static NetworkingResources() { EditorApplication.delayCall += Ensure; }
        public void OnPreprocessBuild(BuildReport report) => Ensure();
        public static void Ensure()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "KimchilyChatFont.asset") != null) return;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(Root + "Fonts/NotoSansCJKkr-Regular.otf");
            if (source == null) return;
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 42, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "KimchilyChatFont"; font.isMultiAtlasTexturesEnabled = true;
            // Keep only a small initial atlas; runtime adds Korean characters as needed.
            font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:!?-_()/채팅연결입장닉네임월드메시지전송닫기준비방코드", out _);
            AssetDatabase.CreateAsset(font, Root + "KimchilyChatFont.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
            Debug.Log("KIMCHILY_CHAT_FONT_READY");
        }
    }
}
