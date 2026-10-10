using System;
using System.Linq;
using UnityEngine;

public static class ValheimDevChange
{
    static GameObject throne;
    static Transform resident;
    static Vector3 originalPosition;
    static Quaternion originalRotation;

    [Serializable] public sealed class Input
    {
        public string prefab = "piece_throne01";
        public float height = -.19f, inward = .35f, distance = 7f;
    }
    [Serializable] public sealed class Result
    {
        public string prefab;
        public Vector3 seat, resident;
        public bool placementOnly = true;
    }

    public static string Run(string json)
    {
        var input = JsonUtility.FromJson<Input>(json);
        var model = GameObject.Find("Lab_GreydwarfResident");
        if (!model) throw new Exception("Spawn George first.");
        if (model.GetComponents<MonoBehaviour>().Any(x =>
            x.GetType().Name == "GreydwarfLoungeVariation" ||
            x.GetType().Name == "ResidentEmotePalette" ||
            x.GetType().Name == "GeorgeWalkPreview"))
            throw new Exception("Remove other pose writers before the throne preview.");
        var prefab = ZNetScene.instance.GetPrefab(input.prefab);
        if (!prefab || !prefab.GetComponent<Chair>())
            throw new Exception("Native throne chair missing.");
        if (float.IsNaN(input.distance) || float.IsInfinity(input.distance) ||
            input.distance < 3f || input.distance > 15f ||
            float.IsNaN(input.height) || float.IsInfinity(input.height) ||
            Mathf.Abs(input.height) > 1f ||
            float.IsNaN(input.inward) || float.IsInfinity(input.inward) ||
            Mathf.Abs(input.inward) > 1f)
            throw new ArgumentException("Choose distance 3–15 and seat offsets within one metre.");

        resident = model.transform;
        originalPosition = resident.localPosition;
        originalRotation = resident.localRotation;
        var position = resident.position + resident.right * input.distance;
        position.y = ZoneSystem.instance.GetGroundHeight(position);
        try
        {
            throne = UnityEngine.Object.Instantiate(prefab, position, resident.rotation);
            throne.name = "Lab_GeorgeThrone";
            var seat = throne.GetComponent<Chair>().m_attachPoint;
            // Keep the donor's native seated loop and tub-owned lifetime. This is
            // a placement preview; it does not attach George through Player APIs.
            resident.SetPositionAndRotation(seat.position + Vector3.up * input.height +
                seat.forward * input.inward, seat.rotation);
            return JsonUtility.ToJson(new Result {
                prefab = input.prefab, seat = seat.position, resident = resident.position });
        }
        catch { Cleanup(); throw; }
    }

    public static void Cleanup()
    {
        if (resident)
        {
            resident.localPosition = originalPosition;
            resident.localRotation = originalRotation;
        }
        if (throne) ZNetScene.instance.Destroy(throne);
        throne = null;
        resident = null;
    }
}
