using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
    }

    internal sealed class Harmony
    {
        private readonly string id;

        internal Harmony(string id)
        {
            this.id = id;
        }

        internal PatchClassProcessor CreateClassProcessor(Type patchType)
        {
            return new PatchClassProcessor(id, patchType);
        }

        internal void UnpatchSelf()
        {
            PatchState.Unpatch(id);
        }
    }

    internal sealed class PatchClassProcessor
    {
        private readonly string owner;
        private readonly Type patchType;

        internal PatchClassProcessor(string owner, Type patchType)
        {
            this.owner = owner;
            this.patchType = patchType;
        }

        internal void Patch()
        {
            if (patchType.Name == "BrokenInvalidPatch")
            {
                throw new InvalidOperationException("target method MissingMethod was not found");
            }

            PatchState.Apply(owner, patchType);
        }
    }

    internal static class PatchState
    {
        private static readonly Dictionary<string, HashSet<Type>> AppliedByOwner =
            new Dictionary<string, HashSet<Type>>(StringComparer.Ordinal);

        internal static void Apply(string owner, Type patchType)
        {
            if (!AppliedByOwner.TryGetValue(owner, out HashSet<Type>? applied))
            {
                applied = new HashSet<Type>();
                AppliedByOwner.Add(owner, applied);
            }

            applied.Add(patchType);
        }

        internal static void Unpatch(string owner)
        {
            AppliedByOwner.Remove(owner);
        }

        internal static bool IsApplied(Type patchType)
        {
            foreach (HashSet<Type> applied in AppliedByOwner.Values)
            {
                if (applied.Contains(patchType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
