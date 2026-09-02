using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Affinities;

internal static class SnipeVignette
{
    private const int TextureSize = 128;
    private const float FullDrawOpacity = 0.68f;
    private static GameObject? root;
    private static RawImage? image;
    private static Texture2D? texture;

    internal static void Show(float drawPercentage)
    {
        if (drawPercentage <= 0f)
        {
            if (root) root.SetActive(false);
            return;
        }

        if (!root) Build();
        image!.color = new Color(0f, 0f, 0f, FullDrawOpacity * Mathf.Clamp01(drawPercentage));
        root!.SetActive(true);
    }

    internal static void Reset()
    {
        // Destroy both owned objects; destroying a RawImage does not release
        // its runtime texture. No native HUD image/material is modified.
        if (root)
        {
            root.SetActive(false);
            Object.Destroy(root);
        }
        if (texture) Object.Destroy(texture);
        root = null;
        image = null;
        texture = null;
    }

    private static void Build()
    {
        Reset();
        // This is an asset-free Unity UI gradient, below native HUD canvases.
        // It needs no loaded damage-flash sprite or post-processing profile,
        // and never competes with the native damage flash or blocks input.
        root = new GameObject("Benheim Snipe Edges", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        GameObject panel = new GameObject("Soft Edges", typeof(RectTransform), typeof(RawImage));
        RectTransform rect = (RectTransform)panel.transform;
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image = panel.GetComponent<RawImage>();
        image.raycastTarget = false;

        texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = "Benheim Snipe Soft Edges";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float horizontal = x / (float)(TextureSize - 1) * 2f - 1f;
                float vertical = y / (float)(TextureSize - 1) * 2f - 1f;
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f,
                    SnipeRules.EdgeOpacity(horizontal, vertical));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        image.texture = texture;
    }
}
