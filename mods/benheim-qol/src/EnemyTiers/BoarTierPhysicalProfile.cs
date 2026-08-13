namespace BenheimQoL.EnemyTiers;

internal readonly struct BoarTierPhysicalProfile
{
    internal const float NativeColliderCenterY = 0.7f;
    internal const float NativeColliderRadius = 0.5f;
    internal const float NativeColliderHeight = 1.4f;

    private BoarTierPhysicalProfile(float scale)
    {
        VisualScale = scale;
        ColliderCenterY = NativeColliderCenterY * scale;
        ColliderRadius = NativeColliderRadius * scale;
        ColliderHeight = NativeColliderHeight * scale;
    }

    internal float VisualScale { get; }
    internal float ColliderCenterY { get; }
    internal float ColliderRadius { get; }
    internal float ColliderHeight { get; }

    internal static bool TryForLevel(int level, out BoarTierPhysicalProfile profile)
    {
        switch (level)
        {
            case 2:
                profile = new BoarTierPhysicalProfile(1.4f);
                return true;
            case 3:
                profile = new BoarTierPhysicalProfile(1.7f);
                return true;
            default:
                profile = default;
                return false;
        }
    }
}

internal sealed class BoarTierApplicationState
{
    internal bool ProfileApplied { get; private set; }

    internal void MarkApplied()
    {
        ProfileApplied = true;
    }

    internal void MarkRestored()
    {
        ProfileApplied = false;
    }
}
