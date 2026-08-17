using System.IO;

/// <summary>
/// WHERE THE REPOSITORY IS, ASKED RATHER THAN ASSUMED.
///
/// The catalogue was reached as "unity/SingularityEngine/Assets/Resources", a
/// path that is only correct if the harness is run from the repository root.
/// unity/README.md says to run it from `unity` — `dotnet run --project
/// tools/UnityStubs` — and from there the same string resolves to
/// `unity/unity/...`, the asset is not found, the ladder silently falls back to
/// the generator, and the run ends with "there is no cube 500 in the catalogue"
/// on a tree with nothing wrong with it.
///
/// Two documented ways to invoke the same tool, one of which fails a check: the
/// fix belongs in the path rather than in the prose. This walks up from wherever
/// it was started looking for the one directory that identifies the repository,
/// so every documented invocation reaches the same file and a future one does
/// too.
/// </summary>
static class Repo
{
    static string _root;

    /// <summary>The repository root, or null if the harness is running outside a checkout.</summary>
    public static string Root
    {
        get
        {
            if (_root != null) return _root;
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null)
            {
                if (Directory.Exists(Path.Combine(d.FullName, "unity", "SingularityEngine")))
                    return _root = d.FullName;
                d = d.Parent;
            }
            return null;
        }
    }

    /// <summary>A path under the Unity project's Resources folder, wherever the run started.</summary>
    public static string Resource(string file)
        => Path.Combine(ResourceDir ?? Path.Combine("unity", "SingularityEngine", "Assets", "Resources"), file);

    /// <summary>The Resources folder itself, or null if there is no checkout above us.</summary>
    public static string ResourceDir
        => Root == null ? null : Path.Combine(Root, "unity", "SingularityEngine", "Assets", "Resources");
}
