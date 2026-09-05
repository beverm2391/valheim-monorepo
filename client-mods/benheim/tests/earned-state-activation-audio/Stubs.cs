namespace UnityEngine
{
    public enum AudioRolloffMode
    {
        Logarithmic,
        Linear,
        Custom
    }

    public enum AudioSourceCurveType
    {
        CustomRolloff,
        SpatialBlend
    }

    public sealed class AnimationCurve
    {
        private AnimationCurve(float startValue, float endValue)
        {
            StartValue = startValue;
            EndValue = endValue;
        }

        public float StartValue { get; }
        public float EndValue { get; }

        public static AnimationCurve Linear(
            float startTime,
            float startValue,
            float endTime,
            float endValue)
        {
            return new AnimationCurve(startValue, endValue);
        }
    }

    public sealed class AudioSource
    {
        public float spatialBlend;
        public float maxDistance;
        public AudioRolloffMode rolloffMode;
        public AnimationCurve? SpatialBlendCurve { get; private set; }

        public void SetCustomCurve(
            AudioSourceCurveType type,
            AnimationCurve curve)
        {
            if (type == AudioSourceCurveType.SpatialBlend)
            {
                SpatialBlendCurve = curve;
            }
        }
    }
}
