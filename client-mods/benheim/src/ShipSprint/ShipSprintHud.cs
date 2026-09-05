using TMPro;
using UnityEngine;

namespace BenheimQoL.ShipSprint;

// The gauge shares the wind indicator's stable parent and copies the native
// wind anchor. The indicator itself rotates with ship heading, so parenting
// below it would rotate the text. Its only visual source is a clone of an
// existing HUD text element.
internal static class ShipSprintHud
{
    private const float LabelHeight = 28f;
    private const float WindGap = 8f;

    private static TMP_Text? label;
    private static Hud? owner;
    private static Ship? displayedShip;

    internal static void Update(Hud hud)
    {
        Player? player = Player.m_localPlayer;
        Ship? ship = player == null ? null : player.GetControlledShip();
        if (ZNet.instance == null
            || ZNetScene.instance == null
            || player == null
            || ship == null
            || !hud.IsVisible()
            || !hud.m_shipHudRoot.activeSelf)
        {
            Hide();
            return;
        }

        Rigidbody? body = ship.GetComponent<Rigidbody>();
        if (body == null || !EnsureLabel(hud))
        {
            Hide();
            return;
        }

        Vector3 velocity = ShipSprintRuntime.GaugeVelocity(ship, body);
        float planarSpeed = ShipSprintGaugeRules.PlanarSpeed(velocity.x, velocity.z);
        label!.text = ShipSprintGaugeRules.Format(
            planarSpeed,
            ShipSprintRuntime.IsLocalRequestActive(ship));
        label.gameObject.SetActive(true);
        displayedShip = ship;
    }

    internal static void Hide(Ship? ship = null)
    {
        if (ship != null && displayedShip != ship)
        {
            return;
        }

        if (label)
        {
            label.gameObject.SetActive(false);
        }
        displayedShip = null;
    }

    // ShipSprintRuntime.Reset calls this, which makes the existing plugin and
    // world teardown paths own the UI lifecycle without another global hook.
    internal static void Destroy()
    {
        if (label)
        {
            label.gameObject.SetActive(false);
            Object.Destroy(label.gameObject);
        }

        label = null;
        owner = null;
        displayedShip = null;
    }

    internal static void Destroy(Hud hud)
    {
        if (owner == hud)
        {
            Destroy();
        }
    }

    private static bool EnsureLabel(Hud hud)
    {
        RectTransform windAnchor = hud.m_shipWindIndicatorRoot;
        if (!windAnchor || !windAnchor.parent)
        {
            Destroy();
            return false;
        }

        Transform parent = windAnchor.parent;
        if (label && owner == hud && label.transform.parent == parent)
        {
            ApplyLayout(label.rectTransform, windAnchor);
            return true;
        }

        Destroy();
        TMP_Text donor = hud.m_healthText;
        if (!donor)
        {
            return false;
        }

        label = Object.Instantiate(donor, parent, worldPositionStays: false);
        label.name = "Benheim_ShipSprintGauge";
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Normal;
        label.fontSize *= 0.75f;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        label.gameObject.SetActive(false);

        ApplyLayout(label.rectTransform, windAnchor);
        owner = hud;
        return true;
    }

    private static void ApplyLayout(RectTransform rect, RectTransform windAnchor)
    {
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = windAnchor.anchorMin;
        rect.anchorMax = windAnchor.anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        float windCenterX = windAnchor.anchoredPosition.x
            + (windAnchor.rect.width * (0.5f - windAnchor.pivot.x));
        float windBottom = windAnchor.anchoredPosition.y
            - (windAnchor.rect.height * windAnchor.pivot.y);
        rect.anchoredPosition = new Vector2(
            windCenterX,
            windBottom - WindGap - (LabelHeight * 0.5f));
        rect.sizeDelta = new Vector2(180f, LabelHeight);
    }
}
