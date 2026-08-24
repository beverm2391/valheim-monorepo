using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Infrastructure;

/// <summary>
/// Takes a manual snapshot of native runtime donors after their owning systems
/// are ready. It never caches a startup view because ObjectDB and Unity UI
/// lifecycles can populate the same singleton in later phases.
/// </summary>
internal static partial class RuntimePrimitiveCatalog
{
    internal static List<RuntimePrimitiveRecord> Create(
        RuntimePrimitiveCatalogCategory category)
    {
        List<RuntimePrimitiveRecord> records = new List<RuntimePrimitiveRecord>();
        switch (category)
        {
            case RuntimePrimitiveCatalogCategory.Effects:
                AddEffects(records, ObjectDB.instance);
                break;
            case RuntimePrimitiveCatalogCategory.Text:
                AddText(records, GetUiRoots());
                break;
            case RuntimePrimitiveCatalogCategory.Ui:
                AddUi(records, GetUiRoots());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(category));
        }

        records.Sort(RuntimePrimitiveRecord.CompareStableIdentity);
        return records;
    }

    private static void AddText(
        List<RuntimePrimitiveRecord> records,
        List<RuntimePrimitiveRoot> roots)
    {
        HashSet<string> fontIdentities = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, Material> materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (!font)
            {
                continue;
            }

            string fontIdentity = StableFontIdentity(font);
            string materialIdentity = StableMaterialIdentity(font.material);
            if (fontIdentities.Add(fontIdentity))
            {
                records.Add(
                    new RuntimePrimitiveRecord("text", "font", $"font:{fontIdentity}")
                        .String("internal_identity", font.name)
                        .String("material_identity", materialIdentity)
                        .String("runtime_type", font.GetType().FullName));
            }
            AddMaterial(materials, font.material);
        }

        foreach (RuntimePrimitiveRoot root in roots)
        {
            foreach (TMP_Text text in root.Transform.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                if (!text)
                {
                    continue;
                }

                if (IsPluginOwnedSubtree(root.Transform, text.transform))
                {
                    continue;
                }

                string path = StablePath(root.Transform, text.transform);
                records.Add(
                    new RuntimePrimitiveRecord("text", "text_donor", $"{root.Kind}:{path}#TMP_Text")
                        .String("source_root", root.Kind)
                        .String("hierarchy_path", path)
                        .String("font_identity", StableFontIdentity(text.font))
                        .String("material_identity", StableMaterialIdentity(text.fontSharedMaterial))
                        .String("runtime_type", text.GetType().FullName));
                AddMaterial(materials, text.fontSharedMaterial);
            }
        }

        foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
        {
            string shaderName = material && material.shader ? material.shader.name : string.Empty;
            if (shaderName.IndexOf("TextMesh", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AddMaterial(materials, material);
            }
        }

        foreach ((string identity, Material material) in materials)
        {
            records.Add(
                new RuntimePrimitiveRecord("text", "material", $"material:{identity}")
                    .String("internal_identity", material.name)
                    .String("shader_identity", material.shader ? material.shader.name : null)
                    .String("texture_identity", StableObjectIdentity(material.mainTexture)));
        }
    }

    private static void AddMaterial(
        Dictionary<string, Material> materials,
        Material? material)
    {
        if (!material)
        {
            return;
        }

        string identity = StableMaterialIdentity(material);
        if (!materials.ContainsKey(identity))
        {
            materials.Add(identity, material);
        }
    }

    private static void AddUi(
        List<RuntimePrimitiveRecord> records,
        List<RuntimePrimitiveRoot> roots)
    {
        foreach (RuntimePrimitiveRoot root in roots)
        {
            AddUiComponents(records, root, root.Transform.GetComponentsInChildren<Image>(includeInactive: true));
            AddUiComponents(records, root, root.Transform.GetComponentsInChildren<Button>(includeInactive: true));
            AddUiComponents(records, root, root.Transform.GetComponentsInChildren<Toggle>(includeInactive: true));
            AddUiComponents(records, root, root.Transform.GetComponentsInChildren<ScrollRect>(includeInactive: true));
            AddUiComponents(records, root, root.Transform.GetComponentsInChildren<Scrollbar>(includeInactive: true));
        }
    }

    private static void AddUiComponents<T>(
        List<RuntimePrimitiveRecord> records,
        RuntimePrimitiveRoot root,
        T[] components)
        where T : Component
    {
        foreach (T component in components)
        {
            if (!component)
            {
                continue;
            }

            if (IsPluginOwnedSubtree(root.Transform, component.transform))
            {
                continue;
            }

            string donorKind = UiDonorKind(component);
            string path = StablePath(root.Transform, component.transform);
            RuntimePrimitiveRecord record =
                new RuntimePrimitiveRecord("ui", donorKind, $"{root.Kind}:{path}#{component.GetType().Name}")
                    .String("source_root", root.Kind)
                    .String("provenance", "native_runtime_root")
                    .String("hierarchy_path", path)
                    .String("component_type", component.GetType().FullName);

            if (component is Image image)
            {
                record
                    .String("sprite_identity", StableSpriteIdentity(image.sprite))
                    .String("image_type", image.type.ToString())
                    .String("material_identity", StableMaterialIdentity(image.material));
            }

            records.Add(record);
        }
    }

    private static string UiDonorKind(Component component)
    {
        if (component is Image image)
        {
            return image.type == Image.Type.Sliced ? "panel_image" : "image";
        }

        if (component is Button)
        {
            return "button";
        }

        if (component is Toggle)
        {
            return "toggle";
        }

        if (component is ScrollRect)
        {
            return "scroll_rect";
        }

        if (component is Scrollbar)
        {
            return "scrollbar";
        }

        return component.GetType().Name;
    }

    private static bool IsPluginOwnedSubtree(Transform root, Transform target)
    {
        Transform? current = target;
        while (current != null)
        {
            if (RuntimePrimitiveCatalogPolicy.IsPluginOwnedObjectName(current.name))
            {
                return true;
            }
            if (current == root)
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    private static List<RuntimePrimitiveRoot> GetUiRoots()
    {
        return new List<RuntimePrimitiveRoot>
        {
            new RuntimePrimitiveRoot("inventory_gui", InventoryGui.instance.transform),
            new RuntimePrimitiveRoot("hud", Hud.instance.transform),
            new RuntimePrimitiveRoot("message_hud", MessageHud.instance.transform),
            new RuntimePrimitiveRoot("menu", Menu.instance.transform),
            new RuntimePrimitiveRoot("menu_settings_prefab", Menu.instance.m_settingsPrefab.transform)
        };
    }

    private static string StablePath(Transform root, Transform target)
    {
        Stack<string> segments = new Stack<string>();
        Transform? current = target;
        while (current != null && current != root)
        {
            segments.Push($"{current.name}[{current.GetSiblingIndex()}]");
            current = current.parent;
        }

        if (current == root)
        {
            return segments.Count == 0
                ? root.name
                : $"{root.name}/{string.Join("/", segments)}";
        }

        return $"detached/{target.name}[{target.GetSiblingIndex()}]";
    }

    // A runtime instance ID changes on each launch. This semantic tuple stays
    // useful across launches and intentionally treats equivalent loaded font
    // assets as the same composition donor.
    private static string StableFontIdentity(TMP_FontAsset? font)
    {
        if (!font)
        {
            return string.Empty;
        }

        return RuntimePrimitiveCatalogPolicy.StableFontIdentity(
            font.name,
            StableObjectIdentity(font.atlasTexture),
            StableMaterialIdentity(font.material));
    }

    private static string StableObjectIdentity(UnityEngine.Object? value)
    {
        return value ? $"{value.name}|{value.GetType().FullName}" : string.Empty;
    }

    private static string? StableSpriteIdentity(Sprite? sprite)
    {
        return sprite
            ? $"{sprite.name}|texture={StableObjectIdentity(sprite.texture)}"
            : null;
    }

    private static string StableMaterialIdentity(Material? material)
    {
        // Materials with the same asset, shader, and texture identity are
        // interchangeable for this donor catalog, so one record is enough.
        return material
            ? RuntimePrimitiveCatalogPolicy.StableMaterialIdentity(
                material.name,
                material.shader ? material.shader.name : string.Empty,
                StableObjectIdentity(material.mainTexture))
            : string.Empty;
    }

    private readonly struct RuntimePrimitiveRoot
    {
        internal RuntimePrimitiveRoot(string kind, Transform transform)
        {
            Kind = kind;
            Transform = transform;
        }

        internal string Kind { get; }
        internal Transform Transform { get; }
    }
}
