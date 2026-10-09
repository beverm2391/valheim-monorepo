using System;
using System.Linq;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable] public sealed class Input { public string emote; }
    [Serializable] public sealed class Result
    {
        public string[] available, playing;
        public string current, mode;
        public int starts, finishes;
        public float placementError;
    }

    public static string Run(string json)
    {
        var resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident) throw new Exception("Spawn George first.");
        var palette = resident.GetComponents<MonoBehaviour>()
            .SingleOrDefault(c => c.GetType().Name == "ResidentEmotePalette");
        if (!palette) throw new Exception("Install the emote palette first.");
        var input = JsonUtility.FromJson<Input>(json);
        if (!string.IsNullOrEmpty(input.emote))
            resident.SendMessage("OnResidentEmote", json, SendMessageOptions.RequireReceiver);
        // Lab recipes compile independently; inspect this prototype's public
        // fields rather than depend on another recipe's generated assembly.
        var type = palette.GetType();
        return JsonUtility.ToJson(new Result {
            available = (string[])type.GetField("available").GetValue(palette),
            playing = (string[])type.GetField("playing").GetValue(palette),
            current = (string)type.GetField("current").GetValue(palette),
            mode = (string)type.GetField("mode").GetValue(palette),
            starts = (int)type.GetField("starts").GetValue(palette),
            finishes = (int)type.GetField("finishes").GetValue(palette),
            placementError = (float)type.GetField("placementError").GetValue(palette)
        });
    }
}
