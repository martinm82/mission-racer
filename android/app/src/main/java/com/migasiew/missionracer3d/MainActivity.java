package com.migasiew.missionracer3d;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.res.AssetManager;
import android.graphics.Color;
import android.os.Build;
import android.os.Bundle;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.ArrayList;
import java.util.List;

/**
 * Native Android Tablet & Phone App for Mission Racer 3D.
 * Runs 100% full-screen with hardware-accelerated WebGL, native Android haptic vibration,
 * bundled offline 3D tracks & cars, automatic Wi-Fi + 4G/5G WAN multiplayer sync,
 * and automatic Over-The-Air (OTA) Cloud Bundle Updates from GitHub Pages / Netlify.
 */
public class MainActivity extends Activity {
    private WebView webView;
    private static final String PREFS_NAME = "mr_ota_prefs";
    private static final String KEY_OTA_URL = "ota_cloud_url";
    private static final String KEY_OTA_SHA = "ota_sha256";
    private static final String KEY_OTA_VER = "ota_version";
    private static final String KEY_SEED_TIME = "seed_apk_time";
    private static final String DEFAULT_CLOUD_OTA_URL = "https://martinm82.github.io/mission-racer";

    @SuppressLint({"SetJavaScriptEnabled", "AddJavascriptInterface"})
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        enableImmersiveFullscreen();

        webView = new WebView(this);
        webView.setBackgroundColor(Color.parseColor("#050811"));
        webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(true);
        settings.setAllowFileAccessFromFileURLs(true);
        settings.setAllowUniversalAccessFromFileURLs(true);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            if (checkSelfPermission(Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(new String[]{Manifest.permission.RECORD_AUDIO}, 101);
            }
        }

        webView.addJavascriptInterface(new AndroidHapticBridge(this), "AndroidBridge");
        webView.setWebChromeClient(new MissionChromeClient());
        webView.setWebViewClient(new MissionWebViewClient(this));

        setContentView(webView);
        loadGameWithOtaSupport();
        checkForCloudOtaUpdate(null, false);
    }

    private File getOtaDir() {
        return new File(getFilesDir(), "ota_bundle");
    }

    private void copyAssetFile(AssetManager am, String assetPath, File destFile) throws Exception {
        File parent = destFile.getParentFile();
        if (parent != null && !parent.exists()) {
            parent.mkdirs();
        }
        InputStream in = am.open(assetPath);
        FileOutputStream out = new FileOutputStream(destFile);
        byte[] buf = new byte[8192];
        int n;
        while ((n = in.read(buf)) > 0) {
            out.write(buf, 0, n);
        }
        out.flush();
        out.close();
        in.close();
    }

    private File ensureOtaBundleSeeded() {
        File otaDir = getOtaDir();
        File otaIndex = new File(otaDir, "index.html");
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, MODE_PRIVATE);

        long apkUpdateTime = 0;
        try {
            PackageInfo pi = getPackageManager().getPackageInfo(getPackageName(), 0);
            apkUpdateTime = pi.lastUpdateTime;
        } catch (Exception ignored) {}

        long savedSeedTime = prefs.getLong(KEY_SEED_TIME, -1);
        if (!otaIndex.exists() || savedSeedTime != apkUpdateTime) {
            try {
                AssetManager am = getAssets();
                String[] rootAssets = new String[]{"index.html", "wan-multiplayer.js", "three.min.js", "manifest.json", "icon.svg"};
                for (int i = 0; i < rootAssets.length; i++) {
                    try {
                        copyAssetFile(am, rootAssets[i], new File(otaDir, rootAssets[i]));
                    } catch (Exception ignored) {}
                }
                String[] nfcTemplates = am.list("nfc-license/templates");
                if (nfcTemplates != null) {
                    for (int i = 0; i < nfcTemplates.length; i++) {
                        String name = nfcTemplates[i];
                        copyAssetFile(am, "nfc-license/templates/" + name, new File(otaDir, "nfc-license/templates/" + name));
                    }
                }
                prefs.edit().putLong(KEY_SEED_TIME, apkUpdateTime).apply();
            } catch (Exception ignored) {}
        }
        return otaIndex.exists() ? otaIndex : null;
    }

    private void loadGameWithOtaSupport() {
        File otaIndex = ensureOtaBundleSeeded();
        if (otaIndex != null && otaIndex.exists()) {
            webView.loadUrl("file://" + otaIndex.getAbsolutePath());
        } else {
            webView.loadUrl("file:///android_asset/index.html");
        }
    }

    private static String downloadText(String urlStr, int timeoutMs) throws Exception {
        URL u = new URL(urlStr);
        HttpURLConnection conn = (HttpURLConnection) u.openConnection();
        conn.setConnectTimeout(timeoutMs);
        conn.setReadTimeout(timeoutMs);
        conn.setUseCaches(false);
        conn.setRequestProperty("Cache-Control", "no-cache");
        int code = conn.getResponseCode();
        if (code != 200) {
            conn.disconnect();
            throw new Exception("HTTP " + code);
        }
        InputStream in = conn.getInputStream();
        ByteArrayOutputStream bos = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        while ((n = in.read(buf)) > 0) {
            bos.write(buf, 0, n);
        }
        in.close();
        conn.disconnect();
        return new String(bos.toByteArray(), "UTF-8");
    }

    public void checkForCloudOtaUpdate(final String customUrl, final boolean manual) {
        final SharedPreferences prefs = getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
        if (customUrl != null && customUrl.trim().length() > 0) {
            prefs.edit().putString(KEY_OTA_URL, customUrl.trim().replaceAll("/+$", "")).apply();
        }
        final String savedOta = prefs.getString(KEY_OTA_URL, DEFAULT_CLOUD_OTA_URL).trim().replaceAll("/+$", "");
        final String currentSha = prefs.getString(KEY_OTA_SHA, "");

        final List<String> candidates = new ArrayList<String>();
        if (savedOta.length() > 0) candidates.add(savedOta);
        if (!candidates.contains(DEFAULT_CLOUD_OTA_URL)) candidates.add(DEFAULT_CLOUD_OTA_URL);
        candidates.add("http://192.168.178.22:3000");

        new OtaWorkerThread(this, candidates, currentSha, manual).start();
    }

    public static class OtaWorkerThread extends Thread {
        private final MainActivity act;
        private final List<String> candidates;
        private final String currentSha;
        private final boolean manual;

        OtaWorkerThread(MainActivity a, List<String> c, String sha, boolean m) {
            this.act = a;
            this.candidates = c;
            this.currentSha = sha;
            this.manual = m;
        }

        @Override
        public void run() {
            long ts = System.currentTimeMillis();
            for (int i = 0; i < candidates.size(); i++) {
                String base = candidates.get(i);
                try {
                    String manifestRaw = downloadText(base + "/ota-manifest.json?t=" + ts, 5000);
                    JSONObject json = new JSONObject(manifestRaw);
                    String remoteSha = json.optString("sha256", "");
                    String remoteVer = json.optString("version", "1.1.0");
                    if (remoteSha.length() == 0) continue;

                    if (remoteSha.equals(currentSha)) {
                        if (manual) {
                            act.evalJsOnUi("if(window.showToast) window.showToast('✅ Already on latest Cloud OTA version (v" + remoteVer + ")!');");
                        }
                        return;
                    }

                    String idxHtml = downloadText(base + "/index.html?t=" + ts, 10000);
                    String wanJs = downloadText(base + "/wan-multiplayer.js?t=" + ts, 10000);
                    if (!idxHtml.contains("<!DOCTYPE html>") || !idxHtml.contains("Mission Racer 3D") || wanJs.length() < 256) {
                        continue;
                    }

                    // Normalize any root-relative asset URLs for local file:// bundle
                    String normalizedHtml = idxHtml
                        .replace("href=\"/manifest.json\"", "href=\"manifest.json\"")
                        .replace("href=\"/icon.svg\"", "href=\"icon.svg\"")
                        .replace("src=\"/three.min.js\"", "src=\"three.min.js\"")
                        .replace("src=\"/wan-multiplayer.js\"", "src=\"wan-multiplayer.js\"")
                        .replace("/nfc-license/templates/", "nfc-license/templates/");

                    File otaDir = act.getOtaDir();
                    if (!otaDir.exists()) otaDir.mkdirs();
                    File idxFile = new File(otaDir, "index.html");
                    File wanFile = new File(otaDir, "wan-multiplayer.js");

                    FileOutputStream fos1 = new FileOutputStream(idxFile);
                    fos1.write(normalizedHtml.getBytes("UTF-8"));
                    fos1.flush();
                    fos1.close();

                    FileOutputStream fos2 = new FileOutputStream(wanFile);
                    fos2.write(wanJs.getBytes("UTF-8"));
                    fos2.flush();
                    fos2.close();

                    SharedPreferences prefs = act.getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
                    prefs.edit().putString(KEY_OTA_SHA, remoteSha).putString(KEY_OTA_VER, remoteVer).apply();

                    act.reloadOtaOnUi(remoteVer, idxFile.getAbsolutePath());
                    return;
                } catch (Exception ignored) {}
            }
            if (manual) {
                act.evalJsOnUi("if(window.showToast) window.showToast('✅ Game is up to date (or OTA server unreachable)');");
            }
        }
    }

    public void evalJsOnUi(final String js) {
        runOnUiThread(new JsEvalRunnable(this, js));
    }

    public void reloadOtaOnUi(final String ver, final String indexPath) {
        runOnUiThread(new OtaReloadRunnable(this, ver, indexPath));
    }

    public static class JsEvalRunnable implements Runnable {
        private final MainActivity act;
        private final String js;
        JsEvalRunnable(MainActivity a, String j) { this.act = a; this.js = j; }
        @Override
        public void run() {
            if (act.webView != null) {
                act.webView.evaluateJavascript(js, null);
            }
        }
    }

    public static class OtaReloadRunnable implements Runnable {
        private final MainActivity act;
        private final String ver;
        private final String path;
        OtaReloadRunnable(MainActivity a, String v, String p) { this.act = a; this.ver = v; this.path = p; }
        @Override
        public void run() {
            if (act.webView != null) {
                act.webView.evaluateJavascript("if(window.showToast) window.showToast('🚀 Cloud OTA Update v" + ver + " Installed! Reloading...');", null);
                act.webView.loadUrl("file://" + path);
            }
        }
    }

    public static class MissionWebViewClient extends WebViewClient {
        private final MainActivity act;
        MissionWebViewClient(MainActivity a) { this.act = a; }

        @Override
        public void onPageFinished(WebView view, String url) {
            super.onPageFinished(view, url);
            SharedPreferences prefs = act.getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
            String otaUrl = prefs.getString(KEY_OTA_URL, DEFAULT_CLOUD_OTA_URL);
            String otaVer = prefs.getString(KEY_OTA_VER, "1.0.0");
            String inject = "(function(){"
                + "window.MISSION_RACER_OTA_URL='" + otaUrl.replace("'", "\\'") + "';"
                + "window.MISSION_RACER_OTA_VERSION='" + otaVer.replace("'", "\\'") + "';"
                + "if(document.getElementById('cloudOtaUrlInput')) return;"
                + "var p=document.getElementById('mpanel-pairing');"
                + "if(!p) return;"
                + "var b=document.createElement('div'); b.className='panel-box'; b.style.marginTop='12px';"
                + "b.innerHTML='<div style=\"display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;\"><h3 style=\"font-size:14px;color:#fbbf24;margin:0;\">☁️ Cloud OTA Auto-Update (Works From Any Network)</h3><span style=\"font-size:11px;font-weight:800;color:#10b981;background:rgba(16,185,129,0.14);padding:3px 8px;border-radius:8px;\">v'+window.MISSION_RACER_OTA_VERSION+'</span></div><p style=\"font-size:12px;color:#94a3b8;margin-bottom:8px;\">Automatically downloads game updates Over-The-Air from GitHub Pages or Netlify whenever you open the app:</p><div style=\"display:flex;gap:8px;flex-wrap:wrap;\"><input id=\"cloudOtaUrlInput\" type=\"url\" value=\"'+window.MISSION_RACER_OTA_URL+'\" style=\"flex:1;min-width:200px;padding:10px 12px;border-radius:10px;border:1px solid rgba(255,255,255,0.2);background:rgba(0,0,0,0.45);color:#fff;font-size:13px;\" /><button class=\"sync-race-btn\" id=\"cloudOtaCheckBtn\" style=\"padding:10px 16px;background:linear-gradient(135deg,#f59e0b,#10b981);\">🔄 Check & Update Now</button></div>';"
                + "p.appendChild(b);"
                + "var btn=document.getElementById('cloudOtaCheckBtn'); var inp=document.getElementById('cloudOtaUrlInput');"
                + "if(btn&&inp){btn.addEventListener('click',function(){var u=(inp.value||'').trim().replace(/\\/+$/,'');if(window.showToast)window.showToast('☁️ Checking Cloud OTA server for updates...');if(window.AndroidBridge&&window.AndroidBridge.checkOtaUpdate)window.AndroidBridge.checkOtaUpdate(u);});}"
                + "})();";
            view.evaluateJavascript(inject, null);
        }
    }

    public static class MissionChromeClient extends WebChromeClient {
        @Override
        public void onPermissionRequest(final PermissionRequest request) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                request.grant(request.getResources());
            }
        }
    }

    public static class AndroidHapticBridge {
        private final MainActivity act;
        AndroidHapticBridge(MainActivity a) {
            this.act = a;
        }

        @JavascriptInterface
        public void vibrate(int durationMs) {
            try {
                Vibrator v = (Vibrator) act.getSystemService(Context.VIBRATOR_SERVICE);
                if (v != null && v.hasVibrator()) {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                        v.vibrate(VibrationEffect.createOneShot(Math.max(15, Math.min(300, durationMs)), VibrationEffect.DEFAULT_AMPLITUDE));
                    } else {
                        v.vibrate(Math.max(15, Math.min(300, durationMs)));
                    }
                }
            } catch (Exception ignored) {}
        }

        @JavascriptInterface
        public void checkOtaUpdate(String customUrl) {
            act.checkForCloudOtaUpdate(customUrl, true);
        }

        @JavascriptInterface
        public String getOtaVersion() {
            SharedPreferences prefs = act.getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
            return prefs.getString(KEY_OTA_VER, "1.0.0");
        }
    }

    private void enableImmersiveFullscreen() {
        View decorView = getWindow().getDecorView();
        decorView.setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
            | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
            | View.SYSTEM_UI_FLAG_FULLSCREEN
        );
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            enableImmersiveFullscreen();
        }
    }

    @Override
    public void onBackPressed() {
        if (webView != null && webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }
}
