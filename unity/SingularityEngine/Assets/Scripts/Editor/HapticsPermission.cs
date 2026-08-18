#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

/// <summary>
/// THE PERMISSION WITHOUT WHICH THE PHONE NEVER BUZZES.
///
/// Haptics call Android's vibrator through AndroidJavaObject, which means Unity
/// cannot see that this game vibrates. Unity adds `android.permission.VIBRATE` to
/// the generated manifest ONLY when it spots `Handheld.Vibrate()` in the code —
/// a reflected `Call("vibrate", ...)` is invisible to it. So the permission was
/// never declared, every call threw SecurityException, and Haptics caught it and
/// said nothing. The switch worked, the slider worked, the patterns were right,
/// and no phone has ever buzzed.
///
/// THE MANIFEST IS EDITED RATHER THAN REPLACED. Dropping an AndroidManifest.xml
/// into Plugins/Android makes it the MAIN manifest, which means hand-maintaining
/// the activity declaration and its whole config-changes list — and getting that
/// wrong does not fail a check, it fails to launch. This runs after Unity has
/// generated its own and adds one element to it, so whatever Unity decided about
/// the activity stays decided.
/// </summary>
public class HapticsPermission : IPostGenerateGradleAndroidProject
{
    // after Unity's own manifest work, before packaging
    public int callbackOrder => 1;

    const string Vibrate = "android.permission.VIBRATE";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string file = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(file))
        {
            UnityEngine.Debug.LogWarning("[Singularity] no generated manifest at " + file
                                       + " — haptics will be silent");
            return;
        }

        var doc = new XmlDocument();
        doc.Load(file);
        XmlElement root = doc.DocumentElement;
        if (root == null) return;

        // already there — a rebuild must not stack duplicates
        foreach (XmlNode n in root.SelectNodes("uses-permission"))
            if (n.Attributes?["android:name"]?.Value == Vibrate) return;

        XmlElement e = doc.CreateElement("uses-permission");
        e.SetAttribute("name", "http://schemas.android.com/apk/res/android", Vibrate);
        root.InsertBefore(e, root.FirstChild);
        doc.Save(file);
    }
}
#endif
