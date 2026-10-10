using System;
using System.Collections.Generic;

// Only the native boundary is stubbed. The production tub component's Update
// owns deferral, retries, permanent failure, and invitation generation changes.
namespace UnityEngine
{
    internal class Object
    {
        public static implicit operator bool(Object? value) => value != null;
        public static bool operator !(Object? value) => value == null;
    }
    internal class MonoBehaviour : Object
    {
        internal readonly GameObject gameObject = new();
    }
    internal class GameObject : Object
    {
        internal BenheimQoL.GreydwarfResident.GreydwarfResidentBehaviour? Resident;
        internal T GetComponent<T>() where T : class => (Resident as T)!;
    }
    internal class Transform { internal object position = new(); }
    internal static class Time { internal static float time; }
}

internal sealed class Smelter : UnityEngine.Object { }
internal sealed class Chair : UnityEngine.Object
{
    internal readonly UnityEngine.Transform m_attachPoint = new();
}
internal sealed class ZDO { internal int Generation = 1; internal bool Invited = true; }
internal sealed class ZNetView : UnityEngine.Object
{
    internal readonly ZDO Data = new();
    internal bool Valid = true;
    internal bool IsValid() => Valid;
    internal ZDO GetZDO() => Data;
}
internal sealed class Player { internal static Player? GetClosestPlayer(object point, float range) => null; }

namespace Benheim.Resident
{
    internal static class ResidentTub
    {
        internal static int Generation(ZDO zdo) => zdo.Generation;
        internal static bool IsInvited(ZDO zdo) => zdo.Invited;
        internal static Chair FindSeat(UnityEngine.GameObject tub) => new();
    }
}

namespace BenheimQoL.GreydwarfResident
{
    internal static class ResidentClient
    {
        internal static bool Available = true;
        internal static void Register(ResidentTubClient tub) { }
        internal static void Unregister(ResidentTubClient tub) { }
    }
    internal sealed class GreydwarfResidentBehaviour : UnityEngine.Object
    {
        internal readonly UnityEngine.GameObject gameObject = new();
    }
    internal static class GreydwarfResidentRuntime
    {
        internal static bool IsWorldReady;
        internal static bool FailCreation;
        internal static int Attempts;
        internal static UnityEngine.GameObject CreateAtSeat(Chair seat)
        {
            Attempts++;
            if (!IsWorldReady || FailCreation) throw new InvalidOperationException("native creation failed");
            return new UnityEngine.GameObject { Resident = new GreydwarfResidentBehaviour() };
        }
        internal static void Remove(UnityEngine.GameObject resident) { }
    }
    internal static class ResidentDiagnostics
    {
        internal static readonly List<(string Event, string Reason)> Events = new();
        internal static void Emit(string name, string reason, int resident = 0) => Events.Add((name, reason));
    }
}
