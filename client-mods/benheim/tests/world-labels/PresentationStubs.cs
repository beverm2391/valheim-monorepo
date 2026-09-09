using UnityEngine;

namespace TMPro
{
    public sealed class TMP_FontAsset : UnityEngine.Object
    {
    }

    public enum FontStyles { Normal }
    public enum TextAlignmentOptions { Center }
    public enum TextWrappingModes { NoWrap, Normal }
    public enum TextOverflowModes { Overflow }

    public sealed class TextMeshProUGUI : Component
    {
        public TMP_FontAsset? font;
        public Material? fontSharedMaterial;
        public float fontSize;
        public bool enableAutoSizing;
        public float fontSizeMin;
        public float fontSizeMax;
        public FontStyles fontStyle;
        public TextAlignmentOptions alignment;
        public TextWrappingModes textWrappingMode;
        public TextOverflowModes overflowMode;
        public Vector4 margin;
        public float characterSpacing;
        public float wordSpacing;
        public float lineSpacing;
        public float paragraphSpacing;
        public Color color;
        public bool richText = true;
        public bool raycastTarget = true;
        public string text = string.Empty;
        public RectTransform rectTransform => (RectTransform)transform;
        public Bounds textBounds;
        public bool isTextOverflowing;
        public bool havePropertiesChanged;
        public TMP_TextInfo textInfo = new();
        public int MeshUpdateCalls;
        public bool ThrowOnMeshUpdate;
        public void ForceMeshUpdate(bool ignoreActiveState = false)
        {
            MeshUpdateCalls++;
            if (ThrowOnMeshUpdate) throw new System.InvalidOperationException("TMP observation failed");
            // Observations are supplied by each test. This stub does not lay
            // out text or infer glyph geometry from a font or string.
        }
    }

    public sealed class TMP_TextInfo
    {
        public int characterCount;
        public int lineCount;
        public TMP_CharacterInfo[] characterInfo = System.Array.Empty<TMP_CharacterInfo>();
    }

    public struct TMP_CharacterInfo
    {
        public bool isVisible;
    }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { On }
    public enum LightProbeUsage { BlendProbes }
    public enum ReflectionProbeUsage { BlendProbes }
    public enum MotionVectorGenerationMode { Object }
}
