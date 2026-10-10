using System.Collections.Generic;
using Benheim.Resident;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

// Native destruction and sector unload own this component's lifetime. Rebuilds
// receive a fresh ZDO identity; no instance-ID cache can inherit an invitation.
internal sealed class ResidentTubClient : MonoBehaviour
{
    internal Smelter Station = null!;
    internal ZNetView View = null!;
    internal GreydwarfResidentBehaviour? Resident;
    internal readonly List<string> RecentRemarks = new();
    internal string LastSpeech = string.Empty;
    private float nextScan;
    private int generation = -1;
    private bool creationFailed, waitingForWorld;
    internal void Configure(Smelter station, ZNetView view)
    { Station = station; View = view; ResidentClient.Register(this); }
    private void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + .2f;
        if (!View || !View.IsValid()) { Remove(); return; }
        ZDO zdo = View.GetZDO();
        int current = ResidentTub.Generation(zdo);
        if (generation != current) { Remove(); generation = current; creationFailed = waitingForWorld = false; RecentRemarks.Clear(); }
        if (!ResidentClient.Available || !ResidentTub.IsInvited(zdo)) { Remove(); return; }
        if (!Resident && !creationFailed)
        {
            // Tub replicas can arrive before Game.SpawnPlayer assigns the local
            // player. That is temporary readiness, not a broken donor or seat:
            // keep the invitation and retry on our next normal scan.
            if (!GreydwarfResidentRuntime.IsWorldReady)
            {
                if (!waitingForWorld) ResidentDiagnostics.Emit("create_deferred", "world_not_ready");
                waitingForWorld = true;
                return;
            }
            if (waitingForWorld)
            {
                waitingForWorld = false;
                ResidentDiagnostics.Emit("creation_resumed", "world_ready");
            }
            Chair? seat = ResidentTub.FindSeat(gameObject);
            if (!seat) { creationFailed = true; ResidentDiagnostics.Emit("create_rejected", "native_seat_missing"); return; }
            if (Player.GetClosestPlayer(seat!.m_attachPoint.position, .05f) != null) return;
            try { Resident = GreydwarfResidentRuntime.CreateAtSeat(seat).GetComponent<GreydwarfResidentBehaviour>(); }
            catch { creationFailed = true; }
        }
    }
    private void Remove() { if (Resident) GreydwarfResidentRuntime.Remove(Resident.gameObject); Resident = null; }
    private void OnDestroy() { ResidentClient.Unregister(this); Remove(); }
}
