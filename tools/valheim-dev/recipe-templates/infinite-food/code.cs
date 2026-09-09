// Bundled template; the runtime recipe is copied into VALHEIM_DEV_ROOT/registry.
using System;
using UnityEngine;

public sealed class InfiniteLabFood : MonoBehaviour
{
    public Player Player;

    private void LateUpdate()
    {
        if (!Player)
            return;

        foreach (Player.Food food in Player.GetFoods())
        {
            if (food == null || food.m_item == null)
                continue;

            ItemDrop.ItemData.SharedData shared = food.m_item.m_shared;
            food.m_time = shared.m_foodBurnTime;
            food.m_health = shared.m_food;
            food.m_stamina = shared.m_foodStamina;
            food.m_eitr = shared.m_foodEitr;
        }
    }
}

public static class ValheimDevChange
{
    [Serializable]
    public sealed class Result
    {
        public bool installed;
        public int currentFoods;
    }

    private static InfiniteLabFood installed;

    public static string Run(string inputJson)
    {
        Player player = Player.m_localPlayer;
        installed = player.gameObject.AddComponent<InfiniteLabFood>();
        installed.Player = player;

        return JsonUtility.ToJson(new Result {
            installed = true,
            currentFoods = player.GetFoods().Count
        });
    }

    public static void Cleanup()
    {
        if (!installed)
            return;
        UnityEngine.Object.Destroy(installed);
        installed = null;
    }
}
