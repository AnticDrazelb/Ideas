plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

android {
    namespace = "com.anticdrazelb.singularity"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.anticdrazelb.singularity"
        minSdk = 26
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"

        // There are no instrumented tests and no test runner. Adding one would
        // be scaffolding for a suite that does not exist.
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro",
            )
        }
        debug {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
    }

    // THE GAME IS ONE FILE AND IT MUST STAY ONE FILE.
    //
    // The page reads its own source at runtime — it pulls the level generator
    // out of `document.querySelector('script').textContent` and hands it to a
    // Worker. Anything that rewrites, minifies or splits the HTML breaks level
    // generation on the second cube and nowhere earlier, which is about the
    // worst place for a build step to leave a bug. So the asset is shipped
    // verbatim, and the only thing done to it is compression in the APK.
    androidResources {
        // (assets are deflated by default; listed here as the seam to reach for
        // if a future asset ever needs storing uncompressed)
        noCompress += listOf<String>()
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        buildConfig = true
    }

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.activity)
    implementation(libs.androidx.webkit)
    implementation(libs.androidx.splashscreen)

    // Play Games is dead weight until res/values/play_games.xml carries a real
    // application id. The SDK is never initialised before that, so an
    // unconfigured build behaves exactly like a build without the dependency:
    // window.AndroidGPG is never installed and the page says BOARDS — LOCAL ONLY.
    implementation(libs.play.services.games)
}
