plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}
val distributionKey = System.getenv("AQUA_ANDROID_KEYSTORE")
val distributionPassword = System.getenv("AQUA_ANDROID_STORE_PASSWORD")
val distributionAlias = System.getenv("AQUA_ANDROID_KEY_ALIAS") ?: "aquacontrol-local"
val hasDistributionSigning = !distributionKey.isNullOrBlank() && !distributionPassword.isNullOrBlank()

android {
    namespace = "bo.aquacontrol.movil"
    compileSdk = 35
    buildToolsVersion = "35.0.0"

    defaultConfig {
        applicationId = "bo.aquacontrol.movil"
        minSdk = 26
        targetSdk = 35
        versionCode = 4
        versionName = "0.4.0"
        manifestPlaceholders["allowCleartext"] = "false"
        manifestPlaceholders["appLabel"] = "AquaControl Móvil"
    }

    buildFeatures { buildConfig = true }
    signingConfigs {
        if (hasDistributionSigning) create("distribution") {
            storeFile = file(distributionKey!!)
            storePassword = distributionPassword
            keyAlias = distributionAlias
            keyPassword = distributionPassword
        }
    }
    buildTypes {
        getByName("debug") { manifestPlaceholders["allowCleartext"] = "true" }
        getByName("release") { if (hasDistributionSigning) signingConfig = signingConfigs.getByName("distribution") }
        create("local") {
            initWith(getByName("release"))
            applicationIdSuffix = ".local"
            isDebuggable = false
            manifestPlaceholders["allowCleartext"] = "true"
            manifestPlaceholders["appLabel"] = "AquaControl Local"
            matchingFallbacks += listOf("release")
        }
    }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    kotlinOptions { jvmTarget = "17" }
}
tasks.matching { it.name == "preLocalBuild" }.configureEach {
    doFirst { check(hasDistributionSigning) { "Ejecute scripts/build-mobile-local.ps1 para compilar la APK local firmada." } }
}

dependencies {
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("com.google.android.material:material:1.12.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    implementation("com.squareup.okhttp3:okhttp-urlconnection:4.12.0")
}
