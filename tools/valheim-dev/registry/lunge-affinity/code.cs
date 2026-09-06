using System;
using BenheimQoL.Affinities;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Result
    {
        public bool applied;
        public string reason;
    }

    public static string Run(string inputJson)
    {
        AffinityDevelopmentFixtureResult fixture =
            AffinityDevelopmentFixture.ApplyToEquippedWeapon("lunge");
        return JsonUtility.ToJson(new Result {
            applied = fixture.Applied,
            reason = fixture.Reason
        });
    }
}
