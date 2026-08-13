plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
}

android {
    namespace = "com.anticdrazelb.singularity"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.anticdrazelb.singularity"

        // THE FLOOR IS A HARDWARE DECISION, NOT AN API ONE.
        //
        // This is a canvas game that fills the screen every frame, and the
        // devices it runs badly on are old ones — a 2016 tablet renders FEWER
        // pixels than a current phone and still cannot keep up, because the GPU
        // is the wall. There is no API here that needs Android 10; the version
        // is being used as the only proxy the platform offers for "not a decade
        // old".
        //
        // 29 specifically: the last generation of genuinely weak hardware tops
        // out at Android 8.1, so 28 would already clear it — and 29 is where
        // systemGestureExclusionRects lands, which makes it the floor that also
        // removes the last runtime version check in this project.
        //
        // Raise it to 30 or 31 by editing this one line; nothing else in the
        // project depends on the value.
        minSdk = 29
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
    // There is deliberately no asset processing configured below. The page reads
    // its own source at runtime — it pulls the level generator out of
    // `document.querySelector('script').textContent` and hands it to a Worker —
    // so anything that rewrites, minifies or splits the HTML breaks level
    // generation on the second cube and nowhere earlier, which is about the
    // worst place for a build step to leave a bug. The asset ships verbatim and
    // the APK just deflates it.

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
