using BepInEx.Configuration;

namespace RepoDeltaForceMod;

internal static class ModFeatureSettings
{
    internal static ConfigEntry<bool> EnableOpeningHavocEvent { get; private set; } = null!;
    internal static ConfigEntry<float> OpeningHavocEventOverlayDurationSeconds { get; private set; } = null!;

    internal static bool AutomaticMilitaryTerminalSceneSpawnEnabled => false;
    internal static bool OpeningHavocEventEnabled => EnableOpeningHavocEvent.Value;
    internal static float OpeningHavocEventOverlayDuration => OpeningHavocEventOverlayDurationSeconds.Value;

    internal static void Initialize(ConfigFile config)
    {
        EnableOpeningHavocEvent = config.Bind(
            "Gameplay",
            "EnableOpeningHavocEvent",
            true,
            "When enabled, entering a generated gameplay scene triggers one opening Havoc event from the current event pool.");

        OpeningHavocEventOverlayDurationSeconds = config.Bind(
            "Gameplay",
            "OpeningHavocEventOverlayDurationSeconds",
            8f,
            "How long the opening Havoc event notice stays visible on screen.");
    }
}
