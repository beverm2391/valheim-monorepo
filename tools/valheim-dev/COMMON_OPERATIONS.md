# Valheim Dev Common Operations

Use `run_once` for code that can finish now. Use `install_change` only when code
must keep running after `Run()` returns. Give each run a short label. Pass values
through `inputs` instead of rewriting source.

Each `Run(string inputJson)` entrypoint accepts a serialized JSON object or array
and returns one in serialized form:

```csharp
using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Result
    {
        public bool ok;
    }

    public static string Run(string inputJson)
    {
        return JsonUtility.ToJson(new Result { ok = true });
    }
}
```

The MCP response parses the returned JSON. Use `read_ledger` with the operation
ID when source, compiler output, runtime detail, or later warnings and errors
matter. Log entries are associated by time; they do not prove that the run
caused a message.

## Inspect The Player

```csharp
using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Result
    {
        public Vector3 position;
        public Vector3 scale;
        public float health;
    }

    public static string Run(string inputJson)
    {
        Player player = Player.m_localPlayer;
        return JsonUtility.ToJson(new Result {
            position = player.transform.position,
            scale = player.transform.localScale,
            health = player.GetHealth()
        });
    }
}
```

Verification: run the same observation after a respawn or scene change. A prior
result does not describe a replacement `Player` object.

## Give An Item

Call this with inputs such as `{ "prefab": "SwordBronze", "amount": 1 }`.

```csharp
using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Input
    {
        public string prefab;
        public int amount = 1;
    }

    [Serializable]
    public sealed class Result
    {
        public string prefab;
        public int requested;
        public bool added;
    }

    public static string Run(string inputJson)
    {
        Input input = JsonUtility.FromJson<Input>(inputJson);
        GameObject prefab = ObjectDB.instance.GetItemPrefab(input.prefab);
        bool added = prefab && Player.m_localPlayer.GetInventory().AddItem(prefab, input.amount);
        return JsonUtility.ToJson(new Result {
            prefab = input.prefab,
            requested = input.amount,
            added = added
        });
    }
}
```

Verification: run an inventory observation for the prefab and compare its count
with the pre-run count. The item is a game effect; Lab off and `remove_change`
do not remove it.

## Inspect Loaded UI

```csharp
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Result
    {
        public int imageCount;
        public string[] sample;
    }

    public static string Run(string inputJson)
    {
        Image[] images = Hud.instance.GetComponentsInChildren<Image>(true);
        return JsonUtility.ToJson(new Result {
            imageCount = images.Length,
            sample = images.Take(20).Select(image => image.name).ToArray()
        });
    }
}
```

Verification: narrow a second observation to the named UI object and return its
active state, hierarchy, and relevant component values.

Loaded Unity UI, TextMeshPro, physics, and other modules are normal compiler
references. If compiled code cannot reference a loaded module, treat that as a
Valheim Dev compiler bug. Do not work around it with reflection.

## Change Movement Values

Call this with inputs such as `{ "runSpeed": 30, "jumpForce": 14 }`.

```csharp
using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Input
    {
        public float runSpeed;
        public float jumpForce;
    }

    [Serializable]
    public sealed class Result
    {
        public float previousRunSpeed;
        public float currentRunSpeed;
        public float previousJumpForce;
        public float currentJumpForce;
    }

    public static string Run(string inputJson)
    {
        Input input = JsonUtility.FromJson<Input>(inputJson);
        Player player = Player.m_localPlayer;
        Result result = new Result {
            previousRunSpeed = player.m_runSpeed,
            previousJumpForce = player.m_jumpForce,
            currentRunSpeed = input.runSpeed,
            currentJumpForce = input.jumpForce
        };
        player.m_runSpeed = input.runSpeed;
        player.m_jumpForce = input.jumpForce;
        return JsonUtility.ToJson(result);
    }
}
```

Verification: read both fields in a fresh `run_once`. Valheim or a new player
object can overwrite them.

## Inspect The Camera

```csharp
using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Result
    {
        public bool found;
        public string name;
        public Vector3 position;
        public float fieldOfView;
    }

    public static string Run(string inputJson)
    {
        Camera camera = Camera.main;
        return JsonUtility.ToJson(camera
            ? new Result {
                found = true,
                name = camera.name,
                position = camera.transform.position,
                fieldOfView = camera.fieldOfView
            }
            : new Result { found = false });
    }
}
```

Verification: compare the returned camera name and values with the visible
camera state. Re-run after camera-mode changes.

## Spawn A World Object

Call this with inputs such as `{ "prefab": "Boar", "distance": 4 }`.

```csharp
using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Input
    {
        public string prefab;
        public float distance = 4f;
    }

    [Serializable]
    public sealed class Result
    {
        public bool spawned;
        public string name;
        public Vector3 position;
    }

    public static string Run(string inputJson)
    {
        Input input = JsonUtility.FromJson<Input>(inputJson);
        Player player = Player.m_localPlayer;
        GameObject prefab = ZNetScene.instance.GetPrefab(input.prefab);
        if (!prefab) return JsonUtility.ToJson(new Result { spawned = false });
        Vector3 position = player.transform.position + player.transform.forward * input.distance;
        GameObject spawned = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        return JsonUtility.ToJson(new Result {
            spawned = true,
            name = spawned.name,
            position = spawned.transform.position
        });
    }
}
```

Verification: observe nearby network views for the prefab and ask Ben whether
the object is visible. Spawning can change the saved disposable world.

## Install Ongoing Behavior

An ongoing change needs a stable ID and a cleanup entrypoint. This example
holds a movement value until removal. Call it with `{ "runSpeed": 30 }`.

```csharp
using System;
using UnityEngine;

public sealed class LabRunSpeed : MonoBehaviour
{
    public Player Player;
    public float Previous;
    public float Target;

    private void Update()
    {
        if (Player) Player.m_runSpeed = Target;
    }

    public void Restore()
    {
        if (Player) Player.m_runSpeed = Previous;
    }

    private void OnDestroy() => Restore();
}

public static class ValheimDevChange
{
    [Serializable]
    public sealed class Input
    {
        public float runSpeed;
    }

    [Serializable]
    public sealed class Result
    {
        public float previousRunSpeed;
        public float targetRunSpeed;
    }

    private static LabRunSpeed installed;

    public static string Run(string inputJson)
    {
        Input input = JsonUtility.FromJson<Input>(inputJson);
        Player player = Player.m_localPlayer;
        installed = player.gameObject.AddComponent<LabRunSpeed>();
        installed.Player = player;
        installed.Previous = player.m_runSpeed;
        installed.Target = input.runSpeed;
        return JsonUtility.ToJson(new Result {
            previousRunSpeed = installed.Previous,
            targetRunSpeed = installed.Target
        });
    }

    public static void Cleanup()
    {
        if (installed == null) return;
        installed.Restore();
        UnityEngine.Object.Destroy(installed);
        installed = null;
    }
}
```

Use a descriptive `change_id` such as `movement.run-speed`. Reuse that ID to
replace the code. Use `remove_change` to call `Cleanup`.

Verification: read the current field after installation and again after
removal. Open the install run later to check whether its callbacks logged a
warning or error after `Run` returned. Turning Lab access off does not remove
the component. Re-enable Lab in the same world to inspect or remove it. Leaving
the world ends tracking and attempts cleanup before another world can use
Valheim Dev.

## Find The Current API

The running build, not this page, owns exact game APIs. Search its decompiled
source before guessing a signature:

```bash
safe client-mods/benheim/scripts/search-valheim-source.sh -n 'AddItem\('
safe client-mods/benheim/scripts/decompile-valheim.sh Inventory
```

Use `bhcatalog effects|text|ui [filter]` for native runtime assets covered by
the Benheim catalog. Otherwise, run a small observation that returns names,
types, components, or hierarchy. Keep the operation bounded and return only the
facts needed for the next step. Verify against a second focused observation or
Ben's visible result.
