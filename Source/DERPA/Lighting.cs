using HarmonyLib;
using UnityEngine;
using Verse;
using System;
using System.Reflection;

namespace DERPA
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("DERPA.lightingmod");  // any unique string
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    [HarmonyPatch(typeof(SectionLayer_LightingOverlay), "Regenerate")]
    public static class DarkenLightingOverlay
    {
        // Reshape the overlay's alpha (darkness) so unlit areas go dark AND a lamp's
        // edge fades in instead of cutting hard.
        //
        // Each vertex: RGB = lamp light colour, alpha = darkness (0 bright .. 255 black).
        // Glow renders near-binary (on/off), but ALPHA renders continuously -- so we
        // grade in alpha, keyed on glow:
        //   glow >= SoftMax (bright core) -> vanilla (untouched)
        //   glow == 0       (unlit)       -> remap alpha (DarkAlpha floor -> MaxAlpha)
        //   in between      (lamp edge)   -> blend, so brightness ramps down to the edge
        // This DOES darken dim lit cells (sim > 0) -- accepted, to soften the border.
        // Daytime stays bright: sunlit cells have alpha ~ 0, and 0 * anything = 0.
        //
        // DarkAlpha is the game's own roofed-area floor
        // (SectionLayer_LightingOverlay.RoofedAreaMinSkyCover); read once, falls back
        // to 100 if a future version renames or removes the field.

        static readonly float DarkAlpha = ReadRoofedAlphaFloor();
        static readonly float MaxAlpha = 255f;
        const int SoftMax = 128;   // glow at/above which a cell is "fully lit" (untouched)

        static float ReadRoofedAlphaFloor()
        {
            var field = AccessTools.Field(typeof(SectionLayer_LightingOverlay), "RoofedAreaMinSkyCover");
            if (field != null && field.IsLiteral)
                return (byte)field.GetRawConstantValue();
            Log.Warning("[DERPA] Couldn't read SectionLayer_LightingOverlay.RoofedAreaMinSkyCover; using fallback 100.");
            return 100f;
        }

        static void Postfix(SectionLayer __instance)
        {
            var getSubMesh = AccessTools.Method(typeof(SectionLayer), "GetSubMesh",
                new[] { typeof(Material) });
            var subMesh = (LayerSubMesh)getSubMesh.Invoke(__instance,
                new object[] { MatBases.LightOverlay });

            if (subMesh?.mesh?.colors32 == null) return;

            var colors = subMesh.mesh.colors32;
            float scale = MaxAlpha / DarkAlpha;
            for (int i = 0; i < colors.Length; i++)
            {
                int maxRGB = Mathf.Max(colors[i].r, Mathf.Max(colors[i].g, colors[i].b));
                // t: 1 = fully lit (vanilla) .. 0 = unlit (full dark), ramped over [0, SoftMax].
                float t = maxRGB >= SoftMax ? 1f : maxRGB / (float)SoftMax;
                int darkened = Mathf.Min(255, Mathf.RoundToInt(colors[i].a * scale));
                colors[i].a = (byte)Mathf.RoundToInt(Mathf.Lerp(darkened, colors[i].a, t));
            }
            subMesh.mesh.colors32 = colors;
        }
    }
}
