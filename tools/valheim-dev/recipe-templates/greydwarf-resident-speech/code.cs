using System;
using System.Linq;
using UnityEngine;

public static class ValheimDevChange
{
    static GreydwarfResidentSpeech behaviour;
    public static string Run(string json)
    {
        var resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident) throw new Exception("Spawn the resident first.");
        if (!Chat.instance) throw new Exception("Native Chat is unavailable.");
        behaviour = resident.AddComponent<GreydwarfResidentSpeech>();
        behaviour.Configure();
        return "{\"trigger\":\"approach with line of sight\",\"display\":\"native NPC dialogue\",\"fixedLine\":true}";
    }
    public static void Cleanup()
    {
        if (behaviour) UnityEngine.Object.DestroyImmediate(behaviour);
        behaviour = null;
    }
}

public class GreydwarfResidentSpeech : MonoBehaviour
{
    Transform head;
    Player pending;
    float expires, nextCheck;
    int viewMask;
    public int approachCount, speechCount, expiredCount;
    public string state = "idle";
    const string Line = "The water's warm. Come sit.";

    public void Configure()
    {
        head = GetComponentsInChildren<Transform>(true).First(t => t.name == "head");
        // Match native AI's view-blocking layers, excluding character colliders.
        viewMask = LayerMask.GetMask("Default", "static_solid", "Default_small",
            "piece", "terrain", "vehicle", "viewblock");
    }

    public void OnResidentApproach(Player player)
    {
        // Local prototype: another player's presence cannot greet this client.
        if (!player || player != Player.m_localPlayer) return;
        pending = player;
        expires = Time.time + 12f;
        nextCheck = Time.time;
        state = "waiting for sightline";
        approachCount++;
    }

    void Update()
    {
        if (!pending || Time.time < nextCheck) return;
        nextCheck = Time.time + .2f;
        var delta = pending.transform.position - transform.position;
        if (pending.IsDead() || Time.time >= expires ||
            new Vector2(delta.x, delta.z).magnitude > 6f || Mathf.Abs(delta.y) > 3f)
        {
            pending = null;
            state = "idle";
            expiredCount++;
            return;
        }
        if (!Chat.instance || Physics.Linecast(pending.GetHeadPoint(), head.position,
            viewMask, QueryTriggerInteraction.Ignore)) return;
        // Native dialogue follows the resident, expires and obeys HUD visibility.
        Chat.instance.SetNpcText(gameObject, head.position - transform.position +
            Vector3.up * .35f, 20f, 10f, "George", Line, false);
        pending = null;
        state = "idle";
        speechCount++;
        Debug.Log("[GreydwarfResident] {\"action\":\"speech\",\"fixedLine\":true}");
    }

    void OnDisable()
    {
        pending = null;
        if (Chat.instance) Chat.instance.ClearNpcText(gameObject);
    }
}
