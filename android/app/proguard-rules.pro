# EVERY @JavascriptInterface METHOD IS CALLED FROM A STRING IN index.html.
# R8 has no way to see those call sites, so without this the bridges are
# renamed or removed and the game silently loses leaderboards, mail and the
# clipboard -- with no crash and no log to find it by.
-keepclassmembers class com.anticdrazelb.singularity.** {
    @android.webkit.JavascriptInterface <methods>;
}
-keep class com.anticdrazelb.singularity.HostBridge { *; }
-keep class com.anticdrazelb.singularity.GpgBridge { *; }

-dontwarn com.google.android.gms.**
