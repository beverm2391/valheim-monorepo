using TMPro;
using UnityEngine;

namespace BenheimQoL.ShipSprint;

// The gauge is a child of Valheim's moving ship-control anchor, so it follows
// the native helm readout instead of introducing a second HUD positioning
// system. Its only visual source is a clone of an existing HUD text element.
internal static class ShipSprintHud
{
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
        Transform parent = hud.m_shipControlsRoot.transform;
        if (label && owner == hud && label.transform.parent == parent)
        {
            return true;
        }

        Destroy();
        TMP_Text donor = hud.m_healthText;
        if (!donor || !parent)
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

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -52f);
        rect.sizeDelta = new Vector2(180f, 28f);
        owner = hud;
        return true;
    }
}
