using System.Xml;
using UnityEngine;
using Verse;

namespace DERPA
{
    // Persisted, user-facing settings. The single source of truth is darknessEnabled,
    // read in two places: the runtime overlay patch (DarkenLightingOverlay) and the
    // load-time night-sky patch (PatchOperationDarknessGate).
    public class DERPASettings : ModSettings
    {
        public bool darknessEnabled = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref darknessEnabled, "darknessEnabled", true);
            base.ExposeData();
        }
    }

    // RimWorld instantiates this automatically at startup, during CreateModClasses --
    // which (confirmed from decompiled LoadAllActiveMods) runs BEFORE ApplyPatches.
    // So GetSettings() here populates DERPASettings before any PatchOperation runs,
    // which is what makes PatchOperationDarknessGate able to read the setting at
    // patch time.
    public class DERPAMod : Mod
    {
        public static DERPASettings Settings;

        public DERPAMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DERPASettings>();
        }

        public override string SettingsCategory() => "DERPA Labs";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var l = new Listing_Standard();
            l.Begin(inRect);
            l.CheckboxLabeled("Darken unlit areas", ref Settings.darknessEnabled,
                "Make unlit interiors and night exteriors genuinely dark.");
            l.Gap(6f);
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            l.Label("Note: the darker night sky applies on the next game restart.");
            GUI.color = Color.white;
            l.End();
        }

        // Called when the settings window closes. The overlay darkening can update
        // live, so force visible map sections to rebake. (The night-sky XML cannot --
        // patches only run at load -- hence the note above.)
        public override void WriteSettings()
        {
            base.WriteSettings();
            Find.CurrentMap?.mapDrawer?.RegenerateEverythingNow();
        }
    }

    // Custom PatchOperation that applies its nested <operations> only when the
    // darkness setting is on. Subclasses the vanilla sequence operation, so the
    // child <li Class="PatchOperation...">s run through normal machinery; we just
    // gate the whole sequence on the setting.
    //
    // Safe at patch time: see DERPAMod above -- the Mod instance and its settings
    // exist before ApplyPatches runs. Returns true (success, no-op) when disabled so
    // the patch sequence doesn't log a spurious failure.
    public class PatchOperationDarknessGate : PatchOperationSequence
    {
        protected override bool ApplyWorker(XmlDocument xml)
        {
            if (DERPAMod.Settings != null && !DERPAMod.Settings.darknessEnabled)
                return true;
            return base.ApplyWorker(xml);
        }
    }
}
