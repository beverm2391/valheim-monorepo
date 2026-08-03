using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.InventoryFeature;

internal sealed class QuickStackContainerWrite
{
    private static readonly FieldInfo NetworkViewField =
        AccessTools.Field(typeof(Container), "m_nview");

    private readonly Container container;
    private readonly ZNetView networkView;
    private readonly uint revisionBeforeWrite;

    private QuickStackContainerWrite(
        Container container,
        ZNetView networkView,
        uint revisionBeforeWrite)
    {
        this.container = container;
        this.networkView = networkView;
        this.revisionBeforeWrite = revisionBeforeWrite;
    }

    internal static bool TryBegin(Container container, out QuickStackContainerWrite? write)
    {
        write = null;
        ZNetView? networkView = NetworkViewField.GetValue(container) as ZNetView;
        if (!networkView || !networkView.IsValid())
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_write_rejected",
                $"container=\"{container.gameObject.name}\" reason=invalid_network_view");
            return false;
        }

        bool ownerBefore = container.IsOwner();
        networkView.ClaimOwnership();
        bool ownerAfter = container.IsOwner();
        uint revision = networkView.GetZDO().DataRevision;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_ownership",
            $"container=\"{container.gameObject.name}\" owner_before={Diagnostics.Bool(ownerBefore)} " +
            $"owner_after={Diagnostics.Bool(ownerAfter)} revision={revision}");

        if (!ownerAfter)
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_write_rejected",
                $"container=\"{container.gameObject.name}\" reason=ownership_not_established");
            return false;
        }

        write = new QuickStackContainerWrite(container, networkView, revision);
        return true;
    }

    internal void Complete(int movedItems)
    {
        uint revisionAfterWrite = networkView.GetZDO().DataRevision;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_write_finished",
            $"container=\"{container.gameObject.name}\" moved={movedItems} " +
            $"owner={Diagnostics.Bool(container.IsOwner())} revision_before={revisionBeforeWrite} " +
            $"revision_after={revisionAfterWrite} revision_advanced={Diagnostics.Bool(revisionAfterWrite > revisionBeforeWrite)}");
    }
}
