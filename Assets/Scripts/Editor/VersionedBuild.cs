using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Every player build gets its own Semantic Versioning 2.0.0 number (semver.org): MAJOR.MINOR.PATCH,
/// still 0.y.z while the game is in development. A build raises PATCH (0.1.1 -> 0.1.2); a build that adds
/// a feature raises MINOR (0.1.2 -> 0.2.0). The number goes into PlayerSettings.bundleVersion
/// (Application.version) and names the folder and the zip:
///   Builds/AshenWoods_0.1.2/Ashen Woods.exe  +  Builds/AshenWoods_0.1.2.zip
/// A version folder that already exists is never written into: one folder, one build, one exe.
/// Every build ships CHANGELOG.txt (the project-root file). Its "Unreleased" section must list what changed,
/// or nothing is built; after a successful build that section is renamed to the new version and a fresh,
/// empty "Unreleased" section is put above it.
/// </summary>
public static class VersionedBuild
{
    const string BuildsFolder = "Builds";
    const string FolderPrefix = "AshenWoods_";
    const string ExeName = "Ashen Woods.exe";
    const string ChangelogName = "CHANGELOG.txt";
    const string UnreleasedHeader = "== Не выпущено ==";

    [MenuItem("Ashen Woods/Build/Windows Build - Next Patch (0.y.Z)")]
    static void BuildPatchMenu() => Build(false);

    [MenuItem("Ashen Woods/Build/Windows Build - Next Minor (0.Y.0)")]
    static void BuildMinorMenu() => Build(true);

    /// <summary>Builds the next version for Windows 64. Returns a one-line result (also logged).</summary>
    public static string Build(bool minor)
    {
        string previous = PlayerSettings.bundleVersion;
        string version = Next(previous, minor);
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", BuildsFolder));
        string folder = Path.Combine(root, FolderPrefix + version);
        string zip = folder + ".zip";

        if (Directory.Exists(folder) || File.Exists(zip))
        {
            string refused = "VersionedBuild: " + folder + " already exists - nothing built. Raise the version first.";
            Debug.LogError(refused);
            return refused;
        }

        string changelog = Path.Combine(root, "..", ChangelogName);
        if (!HasUnreleasedNotes(changelog))
        {
            string refused = "VersionedBuild: " + ChangelogName + " has no notes under \"" + UnreleasedHeader
                             + "\" - write what changed in this build first. Nothing built.";
            Debug.LogError(refused);
            return refused;
        }

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("VersionedBuild: no enabled scenes in the build settings.");
            return "no scenes";
        }

        PlayerSettings.bundleVersion = version;
        AssetDatabase.SaveAssets();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CleanBuildCache,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            PlayerSettings.bundleVersion = previous;
            AssetDatabase.SaveAssets();
            string failed = "VersionedBuild: " + version + " " + summary.result + " (" + summary.totalErrors + " errors); version stays " + previous;
            Debug.LogError(failed);
            return failed;
        }

        ReleaseChangelog(changelog, version);
        File.Copy(changelog, Path.Combine(folder, ChangelogName));
        Zip(folder, zip);
        string done = "VersionedBuild: " + version + " built in " + summary.totalTime.TotalSeconds.ToString("0") + " s -> "
                      + Path.Combine(folder, ExeName) + " + " + ChangelogName + " and " + Path.GetFileName(zip)
                      + " (" + (new FileInfo(zip).Length / (1024f * 1024f)).ToString("0") + " MB)";
        Debug.Log(done);
        return done;
    }

    /// <summary>0.1.0-prototype -> 0.1.1 (patch) or 0.2.0 (minor). Pre-release and build tags are dropped.</summary>
    public static string Next(string current, bool minor)
    {
        string core = (current ?? "").Split('-', '+')[0];
        string[] parts = core.Split('.');
        int Part(int i) => i < parts.Length && int.TryParse(parts[i], out int v) ? v : 0;
        int major = Part(0), min = Part(1), patch = Part(2);
        return minor ? major + "." + (min + 1) + ".0" : major + "." + min + "." + (patch + 1);
    }

    /// <summary>True when the changelog's "Unreleased" section holds at least one non-empty line.</summary>
    static bool HasUnreleasedNotes(string changelog)
    {
        if (!File.Exists(changelog))
            return false;
        string[] lines = File.ReadAllLines(changelog);
        int start = Array.IndexOf(lines, UnreleasedHeader);
        if (start < 0)
            return false;
        for (int i = start + 1; i < lines.Length && !lines[i].StartsWith("== ", StringComparison.Ordinal); i++)
            if (lines[i].Trim().Length > 0)
                return true;
        return false;
    }

    /// <summary>Turns the "Unreleased" section into "== version - date ==" under a new empty "Unreleased".</summary>
    static void ReleaseChangelog(string changelog, string version)
    {
        var lines = File.ReadAllLines(changelog).ToList();
        int start = lines.IndexOf(UnreleasedHeader);
        lines[start] = "== " + version + " - " + DateTime.Now.ToString("yyyy-MM-dd") + " ==";
        lines.InsertRange(start, new[] { UnreleasedHeader, "" });
        File.WriteAllLines(changelog, lines, new System.Text.UTF8Encoding(true));
    }

    /// <summary>Zips the build without Unity's debug-symbol folder (*_DoNotShip).</summary>
    static void Zip(string folder, string zip)
    {
        using (ZipArchive archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(folder.Length + 1).Replace('\\', '/');
                if (relative.Split('/')[0].EndsWith("_DoNotShip", StringComparison.Ordinal))
                    continue;
                archive.CreateEntryFromFile(file, relative, System.IO.Compression.CompressionLevel.Optimal);
            }
        }
    }
}
