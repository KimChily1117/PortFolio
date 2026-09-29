# Chat font provenance

- File: NotoSansCJKkr-Regular.otf (unmodified)
- Source: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf
- Downloaded: 2026-09-29
- SHA-256: 6BCB2A0703AA137E874FC2DFFA85F6C21BA9A67FA329E81B8C801663AF7E992A
- License: SIL Open Font License 1.1, see LICENSE.txt alongside the font.

Editor/NetworkingResources.cs creates Resources/KimchilyChatFont.asset as a dynamic,
multi-atlas TMP font asset. The original font is included for Korean glyph generation
in the built player, so user-entered Hangul does not require a fixed character list.

Runtime/TMP contains the required resources extracted from Unity's TMP Essential
Resources.unitypackage, supplied by com.unity.ugui@2.0.0 in Unity 6000.3.24f1.
Original asset GUIDs and licenses are preserved; unused shadergraph samples are omitted.
See Runtime/TMP/Unity-UI-LICENSE.md and the font/emoji license files in that directory.

Unity TMP documentation: https://docs.unity.cn/Packages/com.unity.ugui%402.0/manual/TextMeshPro/index.html
