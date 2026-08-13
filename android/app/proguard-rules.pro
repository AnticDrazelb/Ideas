# THE BRIDGE IS CALLED FROM JAVASCRIPT AND R8 CANNOT SEE THAT.
#
# Nothing in Kotlin ever calls HostBridge.copy or PlayGamesBridge.submit, so to
# R8 every one of these methods is unreachable and gets stripped or renamed.
# The build stays green, the app installs, and then the page's clipboard and
# leaderboards silently do nothing — in release only. This rule is the whole
# reason a release build of a WebView app is not the same app as a debug one.
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}

# The bridge classes themselves are instantiated reflectively by nothing, but
# they are reached only through addJavascriptInterface, so keep the types too.
-keep class com.anticdrazelb.singularity.bridge.** { *; }

# Play Games resolves parts of itself reflectively.
-dontwarn com.google.android.gms.**
