using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RepoDeltaForceMod;

[BepInPlugin("IHanabiI.RepoDeltaForceMod", "RepoDeltaForceMod", "1.0")]
public class RepoDeltaForceMod : BaseUnityPlugin
{
    private static readonly Rect FlightRecorderHudRect = new(16f, 300f, 420f, 116f);
    private static Texture2D? _solidTexture;
    private static GUIStyle? _havocEventTitleStyle;
    private static GUIStyle? _havocEventBodyStyle;
    private static GUIStyle? _militaryTerminalTitleStyle;
    private static GUIStyle? _militaryTerminalStatusStyle;
    private static GUIStyle? _militaryTerminalBodyStyle;

    internal static RepoDeltaForceMod Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void Awake()
    {
        Instance = this;

        gameObject.transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        ModFeatureSettings.Initialize(Config);
        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        OpeningHavocEventService.Tick();
        FlightRecorderStatusHudService.Tick();
        FlightRecorderEnvironmentalInterferenceService.Tick();
        FlightRecorderResidualReplayService.Tick();
        AirDropCaseOpenService.Tick();
        AirDropCaseHaulRewardService.Tick();
        ValuableHoldRadarService.Tick();
    }

    private void LateUpdate()
    {
        MilitaryTerminalHeldUiSuppressionService.Tick();
        FlightRecorderHighlightService.Tick();
        HavocSupplyHighlightService.Tick();
    }

    private void OnGUI()
    {
        DrawOpeningHavocEventOverlay();
        DrawMilitaryTerminalRadarHud();
    }

    private static void DrawOpeningHavocEventOverlay()
    {
        if (!OpeningHavocEventService.TryGetOverlayState(out var overlayState))
        {
            return;
        }

        EnsureHudStyles();

        var panelRect = GetOpeningHavocEventPanelRect();
        DrawFilledRect(panelRect, new Color(0.02f, 0.03f, 0.03f, 0.78f));
        DrawOutline(panelRect, new Color(0.72f, 0.96f, 0.28f, 0.9f), 2f);

        var titleGlowColor = GUI.color;
        var titlePulse = 0.85f + Mathf.PingPong(overlayState.ElapsedTime * 1.6f, 0.15f);
        GUI.color = new Color(0.95f, 1f, 0.72f, titlePulse);

        var titleRect = new Rect(panelRect.x + 12f, panelRect.y + 18f, panelRect.width - 24f, 48f);
        GUI.Label(new Rect(titleRect.x + 2f, titleRect.y + 2f, titleRect.width, titleRect.height), overlayState.Title, _havocEventTitleStyle);
        GUI.color = titleGlowColor;
        GUI.Label(titleRect, overlayState.Title, _havocEventTitleStyle);

        var bodyBaseY = panelRect.y + 80f;
        for (var i = 0; i < overlayState.RevealLineCount && i < overlayState.DetailLines.Count; i++)
        {
            var lineRevealOffset = Mathf.Max(0f, overlayState.ElapsedTime - (0.45f + i * 0.6f));
            var revealProgress = EaseOutCubic(Mathf.Clamp01(lineRevealOffset / 0.45f));
            var lineRect = new Rect(
                panelRect.x + 18f,
                bodyBaseY + i * 28f + (1f - revealProgress) * 12f,
                panelRect.width - 36f,
                26f);

            var bodyColor = GUI.color;
            GUI.color = new Color(0.92f, 0.98f, 0.88f, revealProgress);
            GUI.Label(lineRect, overlayState.DetailLines[i], _havocEventBodyStyle);
            GUI.color = bodyColor;
        }
    }

    private static void DrawMilitaryTerminalRadarHud()
    {
        var hudState = ValuableHoldRadarService.CurrentHudState;
        if (hudState is null)
        {
            return;
        }

        EnsureHudStyles();

        var radarWindowRect = GetMilitaryTerminalRadarRect();
        DrawFilledRect(radarWindowRect, new Color(0.03f, 0.04f, 0.04f, 0.84f));
        DrawOutline(radarWindowRect, new Color(0.72f, 0.96f, 0.28f, 0.8f), 2f);

        var titleRect = new Rect(radarWindowRect.x + 12f, radarWindowRect.y + 10f, radarWindowRect.width - 24f, 24f);
        GUI.Label(titleRect, hudState.Title, _militaryTerminalTitleStyle);

        var lineRect = new Rect(radarWindowRect.x + 16f, radarWindowRect.y + 42f, radarWindowRect.width - 32f, 22f);
        GUI.Label(lineRect, hudState.StatusLine, _militaryTerminalStatusStyle);

        foreach (var leadLine in hudState.LeadLines)
        {
            lineRect.y += 22f;
            GUI.Label(lineRect, leadLine, _militaryTerminalBodyStyle);
        }
    }

    private static Rect GetMilitaryTerminalRadarRect()
    {
        const float width = 332f;
        const float height = 134f;
        var x = (Screen.width - width) * 0.5f;
        var y = Mathf.Max(18f, Screen.height - 490f);
        return new Rect(x, y, width, height);
    }

    private static Rect GetOpeningHavocEventPanelRect()
    {
        const float width = 720f;
        const float height = 168f;
        var x = (Screen.width - width) * 0.5f;
        var y = Mathf.Max(72f, Screen.height * 0.26f);
        return new Rect(x, y, width, height);
    }

    private static void EnsureHudStyles()
    {
        _solidTexture ??= Texture2D.whiteTexture;

        if (_havocEventTitleStyle is null)
        {
            _havocEventTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = false,
            };
            _havocEventTitleStyle.normal.textColor = new Color(0.92f, 1f, 0.55f, 1f);
        }

        if (_havocEventBodyStyle is null)
        {
            _havocEventBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            _havocEventBodyStyle.normal.textColor = new Color(0.92f, 0.98f, 0.9f, 1f);
        }

        if (_militaryTerminalTitleStyle is null)
        {
            _militaryTerminalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            _militaryTerminalTitleStyle.normal.textColor = new Color(0.87f, 0.97f, 0.46f, 1f);
        }

        if (_militaryTerminalStatusStyle is null)
        {
            _militaryTerminalStatusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            _militaryTerminalStatusStyle.normal.textColor = new Color(0.94f, 0.98f, 0.9f, 1f);
        }

        if (_militaryTerminalBodyStyle is null)
        {
            _militaryTerminalBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                wordWrap = false,
            };
            _militaryTerminalBodyStyle.normal.textColor = new Color(0.82f, 0.93f, 0.74f, 1f);
        }
    }

    private static void DrawFilledRect(Rect rect, Color color)
    {
        var previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _solidTexture);
        GUI.color = previousColor;
    }

    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        DrawFilledRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawFilledRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawFilledRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawFilledRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private static float EaseOutCubic(float value)
    {
        var clamped = Mathf.Clamp01(value);
        var inverse = 1f - clamped;
        return 1f - inverse * inverse * inverse;
    }

    private void OnDestroy()
    {
        Unpatch();
    }
}
