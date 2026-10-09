// Bundled Lab prototype; local edits belong in the runtime registry.
using System;
using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;

public static class ValheimDevChange
{
    [Serializable]
    public sealed class Input
    {
        public float distance = 5f;
        public float height = -.19f;
        public float inward = .35f;
        public float scale = 1f;
    }

    [Serializable]
    public sealed class Result
    {
        public string donor;
        public Vector3 tub;
        public Vector3 resident;
        public string[] controllers;
    }

    static GameObject tub;
    static GameObject resident;
    static SoftReference<GameObject> reference;
    static bool held;

    public static string Run(string json)
    {
        var input = JsonUtility.FromJson<Input>(json);
        var player = Player.m_localPlayer;
        if (!player || !ZoneSystem.instance || !ZNetScene.instance)
            throw new InvalidOperationException("Enter the disposable Lab world first.");
        if (input.distance <= 0 || input.scale <= 0)
            throw new ArgumentException("Distance and scale must be positive.");

        var position = player.transform.position + player.transform.forward * input.distance;
        position.y = ZoneSystem.instance.GetGroundHeight(position);
        reference = ZoneSystem.instance.m_locations
            .First(location => location.m_prefabName == "BogWitch_Camp").m_prefab;
        reference.Load();
        held = true;
        // The donor has no ZNetView to retain its soft assets. Hold the location
        // reference until cleanup so the cloned Animator controller stays loaded.
        var donor = reference.Asset.GetComponentsInChildren<Transform>(true)
            .First(child => child.name == "Menu_greydwarf_george");
        tub = UnityEngine.Object.Instantiate(
            ZNetScene.instance.GetPrefab("piece_bathtub"), position, player.transform.rotation);
        resident = UnityEngine.Object.Instantiate(donor.gameObject);
        resident.name = "Lab_GreydwarfResident";
        var chair = tub.GetComponentsInChildren<Chair>(true)
            .Single(seat => seat.name == "SitPoint (3)");
        // The native human seat anchor puts George's hips across the rim.
        // Move the intact seated pose inward and down to fit the basin.
        resident.transform.SetParent(tub.transform, false);
        resident.transform.position = chair.m_attachPoint.position +
            Vector3.up * input.height + chair.m_attachPoint.forward * input.inward;
        resident.transform.rotation = chair.m_attachPoint.rotation;
        resident.transform.localScale = Vector3.one * input.scale;
        resident.SetActive(true);
        var view = tub.GetComponent<ZNetView>();
        for (int n = 0; n < 10; n++) view.InvokeRPC("RPC_AddFuel");
        return JsonUtility.ToJson(new Result {
            donor = donor.name, tub = tub.transform.position,
            resident = resident.transform.position,
            controllers = resident.GetComponentsInChildren<Animator>(true)
                .Select(animator => animator.runtimeAnimatorController
                    ? animator.runtimeAnimatorController.name : "none").ToArray()
        });
    }

    public static void Cleanup()
    {
        if (resident) UnityEngine.Object.DestroyImmediate(resident);
        resident = null;
        if (tub)
        {
            if (ZNetScene.instance) ZNetScene.instance.Destroy(tub);
            else UnityEngine.Object.Destroy(tub);
        }
        tub = null;
        if (held)
        {
            reference.Release();
            held = false;
        }
    }
}
