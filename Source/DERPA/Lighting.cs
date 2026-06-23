using HarmonyLib;
using UnityEngine;
using Verse;
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
        // The lighting overlay's vertex alpha is "how much darkness to paint":
        //   0   = fully bright (e.g. sunlit daytime)
        //   255 = fully dark
        //
        // We remap the alpha value so full-bright is preserved, but dim areas go darker.
        //
        // First, we look up the alpha value that is used over roofed, unlit tiles.
        // We store this value in DarkAlpha.
        //
        // Then we remap so that alpha 0 stays at 0, but DarkAlpha maps to 255 (full dark).
        // The max value is clamped to 255.
        //
        //   alpha 0 -> 0  (sun-lit cells untouched)
        //   alpha >= DarkAlpha -> 255 (unlit indoor cell becomes black)
        //
        // Read the game's own roofed-area darkness floor
        // (SectionLayer_LightingOverlay.RoofedAreaMinSkyCover).
        // Cached once; falls back to 100 if a future version renames or removes the field.

        static readonly float DarkAlpha = ReadRoofedAlphaFloor();
        static readonly float MaxAlpha = 255f;

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
            for (int i = 0; i < colors.Length; i++)
            {
                int remapped = Mathf.RoundToInt(colors[i].a * (MaxAlpha / DarkAlpha));
                colors[i].a = (byte)Mathf.Min(255, remapped);
            }
            subMesh.mesh.colors32 = colors;
        }
    }
}
