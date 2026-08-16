using BenheimQoL.Infrastructure;
using HarmonyLib;
using System.Reflection;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Establishes the same requester-local ownership boundary that native Take All
/// uses before it changes a container inventory. Native Stack All omits that
/// step even though Container.OnContainerChanged saves only for the local ZDO
/// owner.
/// </summary>
internal sealed class QuickStackContainerWrite
{
    private static readonly FieldInfo? NetworkViewField =
        AccessTools.Field(typeof(Container), "m_nview");

    private readonly Container container;
    private readonly ZNetView networkView;
    private readonly string operationId;
    private readonly uint revisionBeforeWrite;

    private QuickStackContainerWrite(
        Container container,
        ZNetView networkView,
        string operationId,
        uint revisionBeforeWrite)
    {
        this.container = container;
        this.networkView = networkView;
        this.operationId = operationId;
        this.revisionBeforeWrite = revisionBeforeWrite;
    }

    internal static bool TryBegin(
        Container container,
        string operationId,
        out QuickStackContainerWrite? write)
    {
        write = null;
        ZNetView? networkView = NetworkViewField?.GetValue(container) as ZNetView;
        if (!networkView || !networkView.IsValid())
        {
            Reject(container, "invalid_network_view");
            return false;
        }

        bool ownerBefore = container.IsOwner();
        networkView.ClaimOwnership();
        bool ownerAfter = container.IsOwner();
        uint revision = networkView.GetZDO().DataRevision;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_ownership",
            $"operation_id={operationId} container=\"{container.gameObject.name}\" " +
            $"zdo_id={networkView.GetZDO().m_uid} owner_before={Diagnostics.Bool(ownerBefore)} " +
            $"owner_after={Diagnostics.Bool(ownerAfter)} revision={revision}");
        if (!ownerAfter)
        {
            Reject(container, "ownership_not_established");
            return false;
        }

        write = new QuickStackContainerWrite(container, networkView, operationId, revision);
        return true;
    }

    internal void Complete(int movedItems)
    {
        uint revisionAfterWrite = networkView.GetZDO().DataRevision;
        QuickStackDiagnostics.WriteSnapshot(
            operationId,
            container,
            networkView,
            movedItems,
            revisionBeforeWrite,
            revisionAfterWrite);
    }

    private static void Reject(Container container, string reason)
    {
        Diagnostics.Event(
            "Inventory",
            "quick_stack_write_rejected",
            $"container=\"{container.gameObject.name}\" reason={reason}");
    }
}
