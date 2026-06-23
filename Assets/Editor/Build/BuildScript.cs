using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildScript
{
    public const string BuildFolder = "Builds/Android";

    // ── Keystore ───────────────────────────────────────────────────────────
    private const string KeystorePath = "/Users/addi/brokendreams/StackSurge/keystore/stacksurge.keystore";
    private const string KeystorePass = "SS54321Collide";
    private const string KeyAlias     = "stacksurge";
    private const string KeyAliasPass = "SS54321Collide";

    // ── Version bump enum ──────────────────────────────────────────────────
    [Flags]
    public enum VersionBump { None = 0, Major = 1, Minor = 2, Patch = 4 }

    // ── Core build ─────────────────────────────────────────────────────────

    public static void Build(bool aab, VersionBump bump)
    {
        string label = aab ? "AAB" : "APK";
        string ext   = aab ? ".aab" : ".apk";

        // ── Bump semantic version ──────────────────────────────────────────
        string oldVersion = PlayerSettings.bundleVersion;
        string newVersion = BumpVersion(oldVersion, bump);
        PlayerSettings.bundleVersion = newVersion;
        Debug.Log($"[Build] [{label}] Version {oldVersion} → {newVersion} (flags: {bump})");

        // ── Increment bundle version code ──────────────────────────────────
        int newCode = PlayerSettings.Android.bundleVersionCode + 1;
        PlayerSettings.Android.bundleVersionCode = newCode;
        Debug.Log($"[Build] [{label}] Bundle version code → {newCode}");

        // ── Output filename ────────────────────────────────────────────────
        string fileName = $"StackSurge_{newVersion}{ext}";

        // ── Toggle AAB vs APK ──────────────────────────────────────────────
        EditorUserBuildSettings.buildAppBundle = aab;

        // ── Signing ────────────────────────────────────────────────────────
        if (aab)
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName      = KeystorePath;
            PlayerSettings.Android.keystorePass      = KeystorePass;
            PlayerSettings.Android.keyaliasName      = KeyAlias;
            PlayerSettings.Android.keyaliasPass      = KeyAliasPass;
            Debug.Log($"[Build] [{label}] Signing with alias: {KeyAlias}");
        }
        else
        {
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.Log($"[Build] [{label}] Using Unity debug keystore.");
        }

        // ── Collect scenes ─────────────────────────────────────────────────
        string[] scenes = GetEnabledScenes();
        Debug.Log($"[Build] [{label}] Scenes ({scenes.Length}): {string.Join(", ", scenes)}");

        // ── Output path ────────────────────────────────────────────────────
        string outputDir  = Path.GetFullPath(BuildFolder);
        Directory.CreateDirectory(outputDir);
        string outputPath = Path.Combine(outputDir, fileName);

        // ── Build options ──────────────────────────────────────────────────
        BuildOptions buildOptions = aab
            ? BuildOptions.None
            : BuildOptions.Development | BuildOptions.AllowDebugging;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outputPath,
            target           = BuildTarget.Android,
            options          = buildOptions,
        };

        Debug.Log($"[Build] [{label}] Starting → {outputPath}");
        BuildPipeline.BuildPlayer(options);
        Debug.Log($"[Build] [{label}] Done → {outputPath}");
    }

    // ── Version helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Applies all flagged bumps in Major → Minor → Patch order.
    /// Each bump resets lower components. Falls back to "0.0.1" on parse failure.
    /// </summary>
    public static string BumpVersion(string current, VersionBump bump)
    {
        int major = 0, minor = 0, patch = 0;

        string[] parts = (current ?? "").Split('.');
        if (parts.Length == 3)
        {
            int.TryParse(parts[0], out major);
            int.TryParse(parts[1], out minor);
            int.TryParse(parts[2], out patch);
        }

        if ((bump & VersionBump.Major) != 0) { major++; minor = 0; patch = 0; }
        if ((bump & VersionBump.Minor) != 0) { minor++;             patch = 0; }
        if ((bump & VersionBump.Patch) != 0) { patch++;                        }

        return $"{major}.{minor}.{patch}";
    }

    public static string[] GetEnabledScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled)
                scenes.Add(scene.path);
        return scenes.ToArray();
    }
}