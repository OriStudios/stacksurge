using UnityEditor;
using UnityEngine;

public class BuildWindow : EditorWindow
{
    // ── State ──────────────────────────────────────────────────────────────
    private bool _bumpMajor;
    private bool _bumpMinor;
    private bool _bumpPatch;
    private bool _targetAAB  = true;   // true = Release AAB, false = Debug APK
    private bool _targetSet;           // whether the user has picked a target yet

    // ── Styles (lazy-init) ─────────────────────────────────────────────────
    private GUIStyle _styleCard;
    private GUIStyle _styleVersionNumber;
    private GUIStyle _styleVersionLabel;
    private GUIStyle _styleSectionLabel;
    private GUIStyle _styleBumpActive;
    private GUIStyle _styleBumpInactive;
    private GUIStyle _styleBumpSubActive;
    private GUIStyle _styleBumpSubInactive;
    private GUIStyle _styleTargetActive;
    private GUIStyle _styleTargetInactive;
    private GUIStyle _styleTargetTitle;
    private GUIStyle _styleTargetSub;
    private GUIStyle _stylePreviewRow;
    private GUIStyle _styleBuildButton;
    private GUIStyle _styleBuildButtonDisabled;
    private bool     _stylesInitialised;

    // ── Colors ─────────────────────────────────────────────────────────────
    private static readonly Color ColBackground  = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color ColCard        = new Color(0.18f, 0.18f, 0.18f);
    private static readonly Color ColCardActive  = new Color(0.24f, 0.24f, 0.24f);
    private static readonly Color ColBorder      = new Color(0.30f, 0.30f, 0.30f);
    private static readonly Color ColText        = new Color(0.90f, 0.90f, 0.90f);
    private static readonly Color ColMuted       = new Color(0.50f, 0.50f, 0.50f);
    private static readonly Color ColAccent      = new Color(0.95f, 0.95f, 0.95f);
    private static readonly Color ColBuildButton = new Color(0.22f, 0.22f, 0.22f);

    // ── Open ───────────────────────────────────────────────────────────────
    [MenuItem("Build/Android Build Window")]
    public static void Open()
    {
        var win = GetWindow<BuildWindow>(utility: false, title: "Android build");
        win.minSize = new Vector2(420, 520);
        win.maxSize = new Vector2(700, 700);
    }

    // ── GUI ────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        InitStyles();

        // Background fill
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), ColBackground);

        float pad = 16f;
        float w   = position.width - pad * 2;

        GUILayout.Space(pad);
        GUILayout.BeginHorizontal();
        GUILayout.Space(pad);
        GUILayout.BeginVertical(GUILayout.Width(w));

        // ── Project badge (top-right) ──────────────────────────────────────
        // (window title already shows "Android build"; badge shown inline)

        DrawVersionRow(w);
        GUILayout.Space(16);
        DrawBumpSection(w);
        GUILayout.Space(16);
        DrawTargetSection(w);
        GUILayout.Space(16);
        DrawPreviewRow(w);
        GUILayout.Space(8);
        DrawBuildButton(w);

        GUILayout.EndVertical();
        GUILayout.Space(pad);
        GUILayout.EndHorizontal();
    }

    // ── Version row ────────────────────────────────────────────────────────
    private void DrawVersionRow(float w)
    {
        float half = (w - 8) / 2f;
        float h    = 80f;

        GUILayout.BeginHorizontal();

        // Version card
        Rect vr = GUILayoutUtility.GetRect(half, h, GUILayout.Width(half), GUILayout.Height(h));
        DrawRoundedCard(vr, ColCard, ColBorder);
        GUI.Label(new Rect(vr.x, vr.y + 12, vr.width, 20), "version",                              _styleVersionLabel);
        GUI.Label(new Rect(vr.x, vr.y + 30, vr.width, 36), PlayerSettings.bundleVersion,           _styleVersionNumber);

        GUILayout.Space(8);

        // Bundle code card
        Rect br = GUILayoutUtility.GetRect(half, h, GUILayout.Width(half), GUILayout.Height(h));
        DrawRoundedCard(br, ColCard, ColBorder);
        GUI.Label(new Rect(br.x, br.y + 12, br.width, 20), "bundle code",                          _styleVersionLabel);
        GUI.Label(new Rect(br.x, br.y + 30, br.width, 36), PlayerSettings.Android.bundleVersionCode.ToString(), _styleVersionNumber);

        GUILayout.EndHorizontal();
    }

    // ── Bump section ───────────────────────────────────────────────────────
    private void DrawBumpSection(float w)
    {
        GUILayout.Label("version bump — toggle one or more", _styleSectionLabel);
        GUILayout.Space(6);

        float third = (w - 16) / 3f;
        string preview = BuildScript.BumpVersion(PlayerSettings.bundleVersion, ActiveBump());

        GUILayout.BeginHorizontal();
        _bumpMajor = DrawBumpChip(third, "Major", "+1.0.0", _bumpMajor);
        GUILayout.Space(8);
        _bumpMinor = DrawBumpChip(third, "Minor", "+0.1.0", _bumpMinor);
        GUILayout.Space(8);
        _bumpPatch = DrawBumpChip(third, "Patch", "+0.0.1", _bumpPatch);
        GUILayout.EndHorizontal();
    }

    private bool DrawBumpChip(float w, string label, string sub, bool active)
    {
        GUIStyle titleStyle = active ? _styleBumpActive    : _styleBumpInactive;
        GUIStyle subStyle   = active ? _styleBumpSubActive : _styleBumpSubInactive;

        Rect r = GUILayoutUtility.GetRect(w, 62, GUILayout.Width(w), GUILayout.Height(62));
        DrawRoundedCard(r, active ? ColCardActive : ColCard, active ? ColAccent : ColBorder);

        float innerY = r.y + 12;
        GUI.Label(new Rect(r.x, innerY,      r.width, 24), label, titleStyle);
        GUI.Label(new Rect(r.x, innerY + 22, r.width, 18), sub,   subStyle);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            return !active;
        return active;
    }

    // ── Target section ─────────────────────────────────────────────────────
    private void DrawTargetSection(float w)
    {
        GUILayout.Label("build target", _styleSectionLabel);
        GUILayout.Space(6);

        float half = (w - 8) / 2f;
        GUILayout.BeginHorizontal();

        bool aabActive = _targetSet && _targetAAB;
        bool apkActive = _targetSet && !_targetAAB;

        if (DrawTargetCard(half, "⬡  Release AAB", "Play Store · signed", aabActive))
        {
            _targetAAB = true;
            _targetSet = true;
        }

        GUILayout.Space(8);

        if (DrawTargetCard(half, "⚙  Debug APK", "Sideload · dev build", apkActive))
        {
            _targetAAB = false;
            _targetSet = true;
        }

        GUILayout.EndHorizontal();
    }

    private bool DrawTargetCard(float w, string title, string sub, bool active)
    {
        Rect r = GUILayoutUtility.GetRect(w, 72, GUILayout.Width(w), GUILayout.Height(72));
        DrawRoundedCard(r, active ? ColCardActive : ColCard, active ? ColAccent : ColBorder);

        GUIStyle titleStyle = active ? _styleTargetActive   : _styleTargetInactive;
        GUIStyle subStyle   = _styleTargetSub;

        float innerY = r.y + 14;
        GUI.Label(new Rect(r.x, innerY,      r.width, 26), title, titleStyle);
        GUI.Label(new Rect(r.x, innerY + 24, r.width, 18), sub,   subStyle);

        if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            return true;
        return false;
    }

    // ── Preview row ────────────────────────────────────────────────────────
    private void DrawPreviewRow(float w)
    {
        bool anyBump   = ActiveBump() != BuildScript.VersionBump.None;
        bool hasTarget = _targetSet;

        string previewText;
        if (!anyBump && !hasTarget)
            previewText = "→  Select bumps and a target";
        else if (!anyBump)
            previewText = "→  Select at least one version bump";
        else if (!hasTarget)
            previewText = "→  Select a build target";
        else
        {
            string newVer  = BuildScript.BumpVersion(PlayerSettings.bundleVersion, ActiveBump());
            int    newCode = PlayerSettings.Android.bundleVersionCode + 1;
            string ext     = _targetAAB ? ".aab" : ".apk";
            previewText    = $"→  StackSurge_{newVer}{ext}   ·   code {newCode}";
        }

        Rect r = GUILayoutUtility.GetRect(w, 44, GUILayout.Width(w), GUILayout.Height(44));
        DrawRoundedCard(r, ColCard, ColBorder);
        GUI.Label(r, previewText, _stylePreviewRow);
    }

    // ── Build button ───────────────────────────────────────────────────────
    private void DrawBuildButton(float w)
    {
        bool ready = ActiveBump() != BuildScript.VersionBump.None && _targetSet;

        Rect r = GUILayoutUtility.GetRect(w, 52, GUILayout.Width(w), GUILayout.Height(52));
        DrawRoundedCard(r, ready ? ColBuildButton : ColCard, ready ? ColBorder : new Color(0.22f, 0.22f, 0.22f));

        GUIStyle btnStyle = ready ? _styleBuildButton : _styleBuildButtonDisabled;
        GUI.Label(r, "🔨  Build", btnStyle);

        if (ready && GUI.Button(r, GUIContent.none, GUIStyle.none))
        {
            BuildScript.Build(_targetAAB, ActiveBump());
            // Repaint so version numbers update immediately after build
            Repaint();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private BuildScript.VersionBump ActiveBump()
    {
        var b = BuildScript.VersionBump.None;
        if (_bumpMajor) b |= BuildScript.VersionBump.Major;
        if (_bumpMinor) b |= BuildScript.VersionBump.Minor;
        if (_bumpPatch) b |= BuildScript.VersionBump.Patch;
        return b;
    }

    private static void DrawRoundedCard(Rect r, Color fill, Color border)
    {
        // Unity IMGUI doesn't support rounded rects natively; we fake it with
        // nested rects + a 1px border overlay.
        EditorGUI.DrawRect(r, border);
        EditorGUI.DrawRect(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), fill);
    }

    // ── Style init ─────────────────────────────────────────────────────────
    private void InitStyles()
    {
        if (_stylesInitialised) return;
        _stylesInitialised = true;

        _styleVersionLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColMuted },
        };

        _styleVersionNumber = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 28,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColText },
        };

        _styleSectionLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            normal   = { textColor = ColMuted },
        };

        _styleBumpActive = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColAccent },
        };
        _styleBumpInactive = new GUIStyle(_styleBumpActive)
        {
            normal = { textColor = ColText },
        };

        _styleBumpSubActive = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColMuted },
        };
        _styleBumpSubInactive = new GUIStyle(_styleBumpSubActive);

        _styleTargetActive = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColAccent },
        };
        _styleTargetInactive = new GUIStyle(_styleTargetActive)
        {
            normal = { textColor = ColText },
        };

        _styleTargetSub = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColMuted },
        };

        _stylePreviewRow = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleLeft,
            padding   = new RectOffset(14, 14, 0, 0),
            normal    = { textColor = ColMuted },
        };

        _styleBuildButton = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 15,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = ColText },
        };
        _styleBuildButtonDisabled = new GUIStyle(_styleBuildButton)
        {
            normal = { textColor = ColMuted },
        };
    }
}