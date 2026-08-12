plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

android {
    namespace = "com.anticdrazelb.singularity"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.anticdrazelb.singularity"
        minSdk = 24
        targetSdk = 35
        versionCode = 1
        versionName = "1.0"
    }

    buildTypes {
        debug {
            applicationIdSuffix = ".debug"
            isMinifyEnabled = false
        }
        release {
            isMinifyEnabled = true
            // THE GAME IS ONE HTML FILE IN assets AND R8 CANNOT SEE INTO IT.
            // Resource shrinking is deliberately OFF: every leaderboard id is
            // a string resource reached only from Kotlin that R8 can prove
            // nothing about, and a shrunk-away board id fails silently at
            // runtime, which is the worst way for this to break.
            isShrinkResources = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
    kotlinOptions { jvmTarget = "11" }

    // index.html is already minified-by-hand and gzipping it again in the APK
    // buys nothing on a file this size while costing a decompress on every
    // cold start.
    androidResources { noCompress += listOf("html") }

    packaging { resources.excludes += "/META-INF/{AL2.0,LGPL2.1}" }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.activity)
    implementation(libs.androidx.splash)
    // WebViewAssetLoader lives here. It is what makes the game a SECURE
    // CONTEXT, which is what makes navigator.clipboard exist at all.
    implementation(libs.androidx.webkit)
    implementation(libs.play.services.games)
}
