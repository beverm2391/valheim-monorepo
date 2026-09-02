using UnityEngine;

namespace TMPro
{
    public sealed class TMP_FontAsset : UnityEngine.Object
    {
    }

    public enum FontStyles { Normal }
    public enum TextAlignmentOptions { Center }
    public enum TextWrappingModes { NoWrap }
    public enum TextOverflowModes { Overflow }

    public sealed class TextMeshProUGUI : Component
    {
        public TMP_FontAsset? font;
        public Material? fontSharedMaterial;
        public float fontSize;
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
    }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { On }
    public enum LightProbeUsage { BlendProbes }
    public enum ReflectionProbeUsage { BlendProbes }
    public enum MotionVectorGenerationMode { Object }
}
