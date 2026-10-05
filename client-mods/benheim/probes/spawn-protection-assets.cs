using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Read-only Valheim Dev run_once source. Inspect in an authorized loaded local
// world before coding the renderer; no prefab instantiation or mutation.
public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Result
    {
        public bool worldReady;
        public bool workbenchFound;
        public int loadedPlayerBaseAreas;
        public string inspection;
    }

    public static string Run(string inputJson)
    {
        Result result = new Result { worldReady = Player.m_localPlayer != null && ZNetScene.instance != null };
        StringBuilder report = new StringBuilder();
        GameObject workbench = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("piece_workbench") : null;
        result.workbenchFound = workbench != null;
        if (workbench != null)
        {
            foreach (CircleProjector projector in workbench.GetComponentsInChildren<CircleProjector>(true))
            {
                GameObject segment = projector.m_prefab;
                report.AppendLine($"projector={Path(projector.transform, workbench.transform)} mask={projector.m_mask.value} segments={projector.m_nrOfSegments} radius={projector.m_radius} prefab={(segment != null ? segment.name : "missing")} scale={(segment != null ? segment.transform.localScale : Vector3.zero)}");
                if (segment != null)
                {
                    int nodes = 0;
                    Inspect(segment.transform, segment.transform, report, 0, ref nodes);
                }
            }
        }
        foreach (EffectArea area in EffectArea.GetAllAreas())
        {
            if (area == null || (area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
            result.loadedPlayerBaseAreas++;
            if (result.loadedPlayerBaseAreas > 8) continue;
            Collider collider = area.GetComponent<Collider>();
            report.AppendLine($"area={area.transform.root.name} collider={(collider != null ? collider.GetType().Name : "missing")} active={area.isActiveAndEnabled} radius={(collider != null ? area.GetRadius() : 0f)} scale={area.transform.lossyScale}");
        }
        // A scalar report remains inspectable when Unity's serializer omits
        // arrays of dynamically compiled nested DTOs in a Lab observation.
        result.inspection = report.ToString();
        return JsonUtility.ToJson(result);
    }

    private static void Inspect(Transform current, Transform root, StringBuilder report, int depth, ref int nodes)
    {
        if (depth > 4 || nodes >= 24) return;
        nodes++;
        List<string> components = new List<string>();
        foreach (Component component in current.GetComponents<Component>())
            components.Add(component != null ? component.GetType().FullName : "missing_script");
        List<string> shaders = new List<string>();
        Renderer renderer = current.GetComponent<Renderer>();
        if (renderer != null)
            foreach (Material material in renderer.sharedMaterials)
                shaders.Add(material != null && material.shader != null ? material.shader.name : "missing");
        report.AppendLine($"node={Path(current, root)} active={current.gameObject.activeSelf} components={string.Join(",", components)} shaders={string.Join(",", shaders)}");
        for (int i = 0; i < current.childCount; i++) Inspect(current.GetChild(i), root, report, depth + 1, ref nodes);
    }

    private static string Path(Transform current, Transform root)
    {
        string path = current.name;
        while (current != root && current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }
}
