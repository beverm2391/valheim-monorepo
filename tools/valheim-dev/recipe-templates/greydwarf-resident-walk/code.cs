using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class ValheimDevChange
{
    static GeorgeWalkPreview walk;
    public static string Run(string json)
    {
        var resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident || !GameObject.Find("Lab_GeorgeThrone"))
            throw new Exception("Spawn George and the throne preview first.");
        if (resident.GetComponents<MonoBehaviour>().Any(x =>
            x.GetType().Name == "GreydwarfLoungeVariation" ||
            x.GetType().Name == "ResidentEmotePalette"))
            throw new Exception("Remove other pose writers before the walking preview.");
        walk = resident.AddComponent<GeorgeWalkPreview>();
        try { walk.Configure(); }
        catch { Cleanup(); throw; }
        return "{\"navigation\":\"native\",\"multiplayer\":false,\"seatTransition\":false}";
    }
    public static void Cleanup()
    {
        if (walk) UnityEngine.Object.DestroyImmediate(walk);
        walk = null;
    }
}

public class GeorgeWalkPreview : MonoBehaviour
{
    Animator[] animators;
    bool[] events, rootMotion, vibing;
    float[] speeds, forward;
    Vector3 savedPosition;
    Quaternion savedRotation;
    bool captured;
    PlayableGraph graph;
    AnimationMixerPlayable locomotion;
    AnimationClipPlayable idleClip, walkClip;
    float walkWeight;
    readonly List<Vector3> path = new List<Vector3>();
    Vector3 from, to;
    float nextPath, deadline;
    int corner;
    public string state = "path_pending";
    public int attempts, corners, frames;
    public int navigationLayers;
    public Vector3[] route = Array.Empty<Vector3>();
    public Vector3 requestedStart, requestedEnd;
    public float startSnapError, endSnapError, startHeightError, endHeightError;
    public float distanceWalked, remaining;
    public string[] clips;

    public void Configure()
    {
        animators = GetComponentsInChildren<Animator>(true)
            .Where(a => a.runtimeAnimatorController).ToArray();
        if (animators.Length == 0) throw new Exception("George Animator missing.");
        events = animators.Select(a => a.fireEvents).ToArray();
        rootMotion = animators.Select(a => a.applyRootMotion).ToArray();
        speeds = animators.Select(a => a.speed).ToArray();
        vibing = animators.Select(a => a.GetBool("george_vibing")).ToArray();
        forward = animators.Select(a => a.GetFloat("forward_speed")).ToArray();
        savedPosition = transform.localPosition;
        savedRotation = transform.localRotation;
        captured = true;
        var throne = GameObject.Find("Lab_GeorgeThrone").transform;
        // Seat entry/exit is outside this proof. Furniture supplies staging
        // points at its own elevation; GetPath snaps them to a walkable surface.
        // A terrain-only ray would instead select the ground under a built floor.
        from = transform.parent.position + transform.parent.forward * 3f;
        to = throne.position + throne.forward * 2.2f;
        requestedStart = from;
        requestedEnd = to;
        transform.position = from;
        deadline = Time.time + 20f;
        var animator = animators.Single(a => a.isHuman);
        var nativeClips = animator.runtimeAnimatorController.animationClips;
        var idle = nativeClips.First(c => c.name == "Idle");
        var walking = nativeClips.First(c => c.name == "Dwarf Walk");
        graph = PlayableGraph.Create("Lab George native walking");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        idleClip = AnimationClipPlayable.Create(graph, idle);
        walkClip = AnimationClipPlayable.Create(graph, walking);
        locomotion = AnimationMixerPlayable.Create(graph, 2);
        locomotion.ConnectInput(0, idleClip, 0, 1f);
        locomotion.ConnectInput(1, walkClip, 0, 0f);
        // World movement is manual; the native clips own the complete body pose.
        idleClip.SetApplyFootIK(false);
        walkClip.SetApplyFootIK(false);
        AnimationPlayableOutput.Create(graph, "George", animator).SetSourcePlayable(locomotion);
        graph.Play();
        foreach (var a in animators)
        {
            // Bare George has no gameplay animation receivers or physics owner.
            a.fireEvents = false;
            a.applyRootMotion = false;
            a.speed = 1f;
        }
    }

    void SetState(string value)
    {
        state = value;
        Debug.Log("[GreydwarfResident] {\"action\":\"walk_preview\",\"state\":\"" + value + "\"}");
    }

    void Update()
    {
        if (!captured || !Pathfinding.instance) return;
        frames++;
        if (state == "path_pending" && Time.time >= nextPath)
        {
            nextPath = Time.time + .5f;
            attempts++;
            // Evidence belongs to this attempt. A later unavailable path must
            // not retain a rejected route from an earlier NavMesh build.
            route = Array.Empty<Vector3>();
            corners = 0;
            startSnapError = endSnapError = startHeightError = endHeightError = 0f;
            navigationLayers = Pathfinding.instance.m_layers.value;
            // Native CleanPath inserts averaged-height steering hints. George
            // has no Character/Rigidbody to resolve those against ground contacts,
            // so this placement preview follows unmodified NavMesh corners.
            if (Pathfinding.instance.GetPath(from, to, path,
                Pathfinding.AgentType.HumanoidNoSwim, requireFullPath: true, cleanup: false) && path.Count > 0)
            {
                route = path.ToArray();
                corners = path.Count;
                startSnapError = Vector3.Distance(from, path[0]);
                endSnapError = Vector3.Distance(to, path[path.Count - 1]);
                startHeightError = Mathf.Abs(from.y - path[0].y);
                endHeightError = Mathf.Abs(to.y - path[path.Count - 1].y);
                // Native extended sampling can fall back as far as 12m. A full
                // route on a lower floor must not masquerade as this furniture's
                // route. Limit this preview to the native initial 1.5m search
                // radius and HumanoidNoSwim's .3m climb height. These offsets are
                // exposed so the live floor/pivot check can explain a rejection.
                if (startSnapError <= 1.5f && endSnapError <= 1.5f &&
                    startHeightError <= .3f && endHeightError <= .3f)
                {
                    transform.position = path[0];
                    to = path[path.Count - 1];
                    corner = 1;
                    SetState(corner < path.Count ? "walking" : "arrived");
                }
                else if (Time.time >= deadline) SetState("path_surface_mismatch");
            }
            else if (Time.time >= deadline) SetState("path_unavailable");
        }
        if (state == "walking")
        {
            Vector3 target = path[corner];
            Vector3 delta = target - transform.position;
            // Height counts for arrival too; being directly below a raised
            // waypoint must not consume it. Rotation stays upright while moving
            // along the complete 3D segment. This is not collision locomotion.
            if (delta.magnitude <= .15f)
            {
                distanceWalked += delta.magnitude;
                transform.position = target;
                corner++;
                if (corner >= path.Count) SetState("arrived");
            }
            else
            {
                Vector3 facing = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (facing.sqrMagnitude > .0001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(facing), 180f * Time.deltaTime);
                Vector3 position = Vector3.MoveTowards(transform.position, target,
                    1.2f * Time.deltaTime);
                distanceWalked += Vector3.Distance(transform.position, position);
                transform.position = position;
            }
        }
        walkWeight = Mathf.MoveTowards(walkWeight, state == "walking" ? 1f : 0f,
            Time.deltaTime / .25f);
        locomotion.SetInputWeight(0, 1f - walkWeight);
        locomotion.SetInputWeight(1, walkWeight);
        remaining = Vector3.Distance(transform.position, to);
        clips = new[] {
            idleClip.GetAnimationClip().name + ":" + locomotion.GetInputWeight(0),
            walkClip.GetAnimationClip().name + ":" + locomotion.GetInputWeight(1) };
    }

    void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
        if (!captured) return;
        transform.localPosition = savedPosition;
        transform.localRotation = savedRotation;
        for (int n = 0; n < animators.Length; n++)
            if (animators[n])
            {
                // Release the graph, then restore the donor's seated controller.
                animators[n].Rebind();
                animators[n].fireEvents = events[n];
                animators[n].applyRootMotion = rootMotion[n];
                animators[n].speed = speeds[n];
                animators[n].SetBool("george_vibing", vibing[n]);
                animators[n].SetFloat("forward_speed", forward[n]);
                animators[n].Update(0f);
            }
    }
}
