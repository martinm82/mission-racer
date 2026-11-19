import AVFoundation
import UIKit
import WebKit

final class MissionRacerViewController: UIViewController, WKScriptMessageHandler, WKNavigationDelegate, WKUIDelegate {

    private var webView: WKWebView!
    private let lightImpact = UIImpactFeedbackGenerator(style: .light)
    private let mediumImpact = UIImpactFeedbackGenerator(style: .medium)
    private let heavyImpact = UIImpactFeedbackGenerator(style: .heavy)
    private let rigidImpact = UIImpactFeedbackGenerator(style: .rigid)
    private let notificationGenerator = UINotificationFeedbackGenerator()

    private var meterRecorder: AVAudioRecorder?
    private var clipRecorder: AVAudioRecorder?
    private var vadTimer: Timer?
    private var isMonitoringVoice = false
    private var isCapturingClip = false
    private var lastClipSentTime: TimeInterval = 0

    private static let serverUrlDefaultsKey = "MissionRacerServerURL"
    private static let otaUrlDefaultsKey = "MissionRacerCloudOTAURL"
    private static let otaShaDefaultsKey = "MissionRacerOTASha256"
    private static let otaVerDefaultsKey = "MissionRacerOTAVersion"
    private static let otaSeedKey = "MissionRacerOTABundleSeedId"
    private static let defaultCloudOtaUrl = "https://martinm82.github.io/mission-racer"

    override var prefersStatusBarHidden: Bool { true }
    override var prefersHomeIndicatorAutoHidden: Bool { true }
    override var preferredScreenEdgesDeferringSystemGestures: UIRectEdge { .all }
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask { .all }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = UIColor(red: 0.02, green: 0.03, blue: 0.07, alpha: 1.0)

        lightImpact.prepare()
        mediumImpact.prepare()
        heavyImpact.prepare()
        rigidImpact.prepare()
        notificationGenerator.prepare()

        setupWebView()
        loadBundledGame()
    }

    private func setupWebView() {
        let config = WKWebViewConfiguration()
        config.allowsInlineMediaPlayback = true
        config.mediaTypesRequiringUserActionForPlayback = []
        config.preferences.javaScriptCanOpenWindowsAutomatically = false
        config.preferences.setValue(true, forKey: "allowFileAccessFromFileURLs")

        let contentController = WKUserContentController()
        contentController.add(self, name: "haptics")
        contentController.add(self, name: "serverConfig")
        contentController.add(self, name: "voiceIntercom")
        contentController.add(self, name: "otaUpdate")

        let savedOtaUrl = UserDefaults.standard.string(forKey: Self.otaUrlDefaultsKey) ?? Self.defaultCloudOtaUrl
        let escapedOtaUrl = savedOtaUrl
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
        let savedOtaVer = UserDefaults.standard.string(forKey: Self.otaVerDefaultsKey) ?? "1.0.0"

        let savedServerUrl = UserDefaults.standard.string(forKey: Self.serverUrlDefaultsKey) ?? ""
        let escapedUrl = savedServerUrl
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")

        let startScriptSource = """
        (function() {
          window.MISSION_RACER_IOS_NATIVE = true;
          window.MISSION_RACER_SERVER_URL = "\(escapedUrl)";
          window.MISSION_RACER_OTA_URL = "\(escapedOtaUrl)";
          window.MISSION_RACER_OTA_VERSION = "\(savedOtaVer)";

          const origFetch = window.fetch.bind(window);
          const origEventSource = window.EventSource;

          function resolveServerBase() {
            const raw = (window.MISSION_RACER_SERVER_URL || "").trim().replace(/\\/+$/, "");
            return raw;
          }

          window.fetch = function(input, init) {
            const url = (typeof input === "string") ? input : (input && input.url ? input.url : "");
            if (url.startsWith("/api/")) {
              const base = resolveServerBase();
              if (base) {
                return origFetch(base + url, init);
              }
              return handleStandaloneApi(url, init);
            }
            return origFetch(input, init);
          };

          function handleStandaloneApi(url, init) {
            try {
              if (url.startsWith("/api/info")) {
                return Promise.resolve(new Response(JSON.stringify({
                  localIp: "Standalone iOS",
                  port: 3000,
                  lanUrl: "Standalone Offline Mode (iOS Native)",
                  roomCode: "MR-IOS",
                  onlineCount: 1
                }), { status: 200, headers: { "Content-Type": "application/json" } }));
              }
              if (url.startsWith("/api/event") && init && init.body) {
                const data = JSON.parse(init.body);
                if (data.eventType === "STAMP_MISSION" && data.payload && window.drivers) {
                  const key = data.payload.driverKey || 7;
                  const drv = window.drivers[key];
                  if (drv) {
                    drv.fuel = Math.min(5, (drv.fuel || 0) + (Number(data.payload.fuel) || 1));
                    drv.credits = (drv.credits || 0) + (Number(data.payload.credits) || 150);
                    drv.stamps = (drv.stamps || 0) + 1;
                    if (typeof window.renderLicense === "function") window.renderLicense();
                  }
                  if (typeof window.playCountdownSynth === "function") window.playCountdownSynth();
                  if (typeof window.showToast === "function") {
                    window.showToast("🔨 " + (data.payload.stampedBy || "Pit Boss Dad") + " stamped \\"" + (data.payload.title || "Mission") + "\\"! +" + (data.payload.credits || 150) + "🪙 +" + (data.payload.fuel || 1) + "⛽");
                  }
                }
              }
            } catch (e) {}
            return Promise.resolve(new Response(JSON.stringify({ ok: true }), {
              status: 200,
              headers: { "Content-Type": "application/json" }
            }));
          }

          window.EventSource = function(url, eventSourceInitDict) {
            if (typeof url === "string" && url.startsWith("/api/")) {
              const base = resolveServerBase();
              if (base && origEventSource) {
                return new origEventSource(base + url, eventSourceInitDict);
              }
              // Offline standalone mock EventSource
              const mockEs = {
                readyState: 1,
                url: url,
                onopen: null,
                onmessage: null,
                onerror: null,
                close: function() { this.readyState = 2; },
                addEventListener: function() {},
                removeEventListener: function() {}
              };
              setTimeout(function() {
                if (typeof mockEs.onmessage === "function" && window.drivers && window.currentDriverKey) {
                  const d = window.drivers[window.currentDriverKey];
                  mockEs.onmessage({
                    data: JSON.stringify({
                      type: "CONNECTED",
                      payload: {
                        clientId: window.myClientId || "ios_local",
                        players: [{
                          clientId: window.myClientId || "ios_local",
                          driverKey: window.currentDriverKey,
                          callsign: d ? d.callsign : "Lightning 7",
                          icon: d ? d.icon : "⚡",
                          speed: 0,
                          lap: 1,
                          color: window.vehicleColor || "#00e676",
                          trackTheme: window.currentTrackTheme || "monaco"
                        }]
                      }
                    })
                  });
                }
              }, 150);
              return mockEs;
            }
            return new origEventSource(url, eventSourceInitDict);
          };
        })();
        """

        let endScriptSource = """
        (function() {
          function postHaptic(style) {
            try {
              if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.haptics) {
                window.webkit.messageHandlers.haptics.postMessage(style);
              }
            } catch (e) {}
          }

          // Hook game actions with native iOS CoreHaptics / UIImpactFeedbackGenerator
          const wrapFn = function(name, hapticStyle) {
            const orig = window[name];
            if (typeof orig === "function") {
              window[name] = function() {
                postHaptic(hapticStyle);
                return orig.apply(this, arguments);
              };
            }
          };

          wrapFn("triggerNitroBoost", "heavy");
          wrapFn("playSillyHorn", "medium");
          wrapFn("stampMission", "success");
          wrapFn("triggerGrandPrixLaunch", "heavy");
          wrapFn("load3DTrack", "light");
          wrapFn("resetCarToRoad", "rigid");

          ["btnGas", "btnBrake", "btnLeft", "btnRight", "btnNitro"].forEach(function(id) {
            const el = document.getElementById(id);
            if (el) {
              el.addEventListener("touchstart", function() {
                postHaptic(id === "btnNitro" ? "heavy" : "light");
              }, { passive: true });
            }
          });

          // Inject Native iOS Wi-Fi Multiplayer Server Configuration UI in Pairing Panel
          const pairingPanel = document.getElementById("mpanel-pairing");
          if (pairingPanel) {
            const cfgBox = document.createElement("div");
            cfgBox.className = "panel-box";
            cfgBox.style.marginTop = "12px";
            cfgBox.innerHTML = `
              <h3 style="font-size:14px; margin-bottom:6px; color:#00e5ff;">🍎 Native iOS Wi-Fi Multiplayer Server (Optional)</h3>
              <p style="font-size:12px; color:#94a3b8; margin-bottom:8px;">
                Mission Racer 3D runs 100% standalone offline with AI bots & missions. To race live over Wi-Fi with family devices, enter your Mac Pit Control Server URL (e.g. <code>http://192.168.1.100:3000</code>) or leave blank for Standalone Mode:
              </p>
              <div style="display:flex; gap:8px; flex-wrap:wrap;">
                <input id="iosServerUrlInput" type="url" placeholder="http://192.168.1.100:3000 (or leave empty for Offline Mode)"
                  value="${window.MISSION_RACER_SERVER_URL || ''}"
                  style="flex:1; min-width:200px; padding:10px 12px; border-radius:10px; border:1px solid rgba(255,255,255,0.2); background:rgba(0,0,0,0.45); color:#fff; font-size:13px; -webkit-user-select:text; user-select:text;" />
                <button class="sync-race-btn" id="iosSaveServerBtn" style="padding:10px 16px;">💾 Save & Connect</button>
              </div>
            `;
            pairingPanel.appendChild(cfgBox);

            const saveBtn = document.getElementById("iosSaveServerBtn");
            const urlInput = document.getElementById("iosServerUrlInput");
            if (saveBtn && urlInput) {
              saveBtn.addEventListener("click", function() {
                const newUrl = (urlInput.value || "").trim().replace(/\\/+$/, "");
                window.MISSION_RACER_SERVER_URL = newUrl;
                postHaptic("success");
                try {
                  window.webkit.messageHandlers.serverConfig.postMessage(newUrl);
                } catch (e) {}
                if (typeof window.showToast === "function") {
                  window.showToast(newUrl ? ("📡 Connected to " + newUrl) : "🏎️ Switched to Standalone Offline Mode");
                }
                if (typeof window.connectMultiplayerStream === "function") {
                  window.connectMultiplayerStream();
                }
              });
            }
            // Inject Cloud OTA Auto-Update (GitHub Pages / Netlify) Card in Pairing Panel
            const otaBox = document.createElement("div");
            otaBox.className = "panel-box";
            otaBox.style.marginTop = "12px";
            otaBox.innerHTML = `
              <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:6px;">
                <h3 style="font-size:14px; color:#fbbf24; margin:0;">☁️ Cloud OTA Auto-Update (Works From Any Network)</h3>
                <span id="otaVerBadge" style="font-size:11px; font-weight:800; color:#10b981; background:rgba(16,185,129,0.14); padding:3px 8px; border-radius:8px;">v${window.MISSION_RACER_OTA_VERSION || '1.0.0'}</span>
              </div>
              <p style="font-size:12px; color:#94a3b8; margin-bottom:8px;">
                Automatically downloads game updates Over-The-Air from GitHub Pages or Netlify whenever you open the app (even away from home Wi-Fi):
              </p>
              <div style="display:flex; gap:8px; flex-wrap:wrap;">
                <input id="cloudOtaUrlInput" type="url" placeholder="https://martinm82.github.io/mission-racer (or Netlify URL)"
                  value="${window.MISSION_RACER_OTA_URL || 'https://martinm82.github.io/mission-racer'}"
                  style="flex:1; min-width:200px; padding:10px 12px; border-radius:10px; border:1px solid rgba(255,255,255,0.2); background:rgba(0,0,0,0.45); color:#fff; font-size:13px; -webkit-user-select:text; user-select:text;" />
                <button class="sync-race-btn" id="cloudOtaCheckBtn" style="padding:10px 16px; background:linear-gradient(135deg,#f59e0b,#10b981);">🔄 Check & Update Now</button>
              </div>
            `;
            pairingPanel.appendChild(otaBox);

            const otaBtn = document.getElementById("cloudOtaCheckBtn");
            const otaInput = document.getElementById("cloudOtaUrlInput");
            if (otaBtn && otaInput) {
              otaBtn.addEventListener("click", function() {
                const newOtaUrl = (otaInput.value || "").trim().replace(/\\/+$/, "");
                window.MISSION_RACER_OTA_URL = newOtaUrl;
                postHaptic("medium");
                if (typeof window.showToast === "function") {
                  window.showToast("☁️ Checking Cloud OTA server for updates...");
                }
                try {
                  window.webkit.messageHandlers.otaUpdate.postMessage(newOtaUrl);
                } catch (e) {}
              });
            }
          }
        })();
        """

        contentController.addUserScript(WKUserScript(
            source: startScriptSource,
            injectionTime: .atDocumentStart,
            forMainFrameOnly: true
        ))
        contentController.addUserScript(WKUserScript(
            source: endScriptSource,
            injectionTime: .atDocumentEnd,
            forMainFrameOnly: true
        ))

        config.userContentController = contentController

        webView = WKWebView(frame: view.bounds, configuration: config)
        webView.translatesAutoresizingMaskIntoConstraints = false
        webView.navigationDelegate = self
        webView.uiDelegate = self
        webView.isOpaque = false
        webView.backgroundColor = UIColor(red: 0.02, green: 0.03, blue: 0.07, alpha: 1.0)
        webView.scrollView.backgroundColor = UIColor(red: 0.02, green: 0.03, blue: 0.07, alpha: 1.0)
        webView.scrollView.bounces = false
        webView.scrollView.isScrollEnabled = false
        webView.scrollView.contentInsetAdjustmentBehavior = .never
        if #available(iOS 16.4, *) {
            webView.isInspectable = true
        }

        view.addSubview(webView)
        NSLayoutConstraint.activate([
            webView.topAnchor.constraint(equalTo: view.topAnchor),
            webView.bottomAnchor.constraint(equalTo: view.bottomAnchor),
            webView.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            webView.trailingAnchor.constraint(equalTo: view.trailingAnchor)
        ])
    }

    private func otaDirectoryURL() -> URL {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        return docs.appendingPathComponent("OTAWebBundle", isDirectory: true)
    }

    private func ensureOTADirectorySeeded() -> URL? {
        guard let webBundleURL = Bundle.main.url(forResource: "WebBundle", withExtension: nil) else {
            return nil
        }
        let fm = FileManager.default
        let otaDir = otaDirectoryURL()
        let otaIndex = otaDir.appendingPathComponent("index.html")

        // Compute embedded bundle signature so a fresh Xcode install refreshes the base seed
        let embeddedIndex = webBundleURL.appendingPathComponent("index.html")
        let attrs = (try? fm.attributesOfItem(atPath: embeddedIndex.path)) ?? [:]
        let modDate = (attrs[.modificationDate] as? Date)?.timeIntervalSince1970 ?? 0
        let size = (attrs[.size] as? NSNumber)?.intValue ?? 0
        let bundleVer = Bundle.main.infoDictionary?["CFBundleVersion"] as? String ?? "1"
        let currentSeedId = "\(bundleVer)_\(size)_\(Int(modDate))"
        let savedSeedId = UserDefaults.standard.string(forKey: Self.otaSeedKey) ?? ""

        if !fm.fileExists(atPath: otaIndex.path) || savedSeedId != currentSeedId {
            try? fm.removeItem(at: otaDir)
            do {
                try fm.copyItem(at: webBundleURL, to: otaDir)
                UserDefaults.standard.set(currentSeedId, forKey: Self.otaSeedKey)
            } catch {
                return webBundleURL
            }
        }
        return fm.fileExists(atPath: otaIndex.path) ? otaDir : webBundleURL
    }

    private func loadBundledGame() {
        if let activeDir = ensureOTADirectorySeeded() {
            let indexURL = activeDir.appendingPathComponent("index.html")
            if FileManager.default.fileExists(atPath: indexURL.path) {
                webView.loadFileURL(indexURL, allowingReadAccessTo: activeDir)
                // Automatically check Cloud OTA (GitHub Pages / Netlify / Pit Server) in background
                checkForCloudOTAUpdate(customBaseUrl: nil, manual: false)
                return
            }
        }

        if let webBundleURL = Bundle.main.url(forResource: "WebBundle", withExtension: nil),
           FileManager.default.fileExists(atPath: webBundleURL.appendingPathComponent("index.html").path) {
            let indexURL = webBundleURL.appendingPathComponent("index.html")
            webView.loadFileURL(indexURL, allowingReadAccessTo: webBundleURL)
            return
        }

        if let indexURL = Bundle.main.url(forResource: "index", withExtension: "html") {
            webView.loadFileURL(indexURL, allowingReadAccessTo: indexURL.deletingLastPathComponent())
            return
        }
    }

    private func checkForCloudOTAUpdate(customBaseUrl: String?, manual: Bool) {
        if let custom = customBaseUrl, !custom.isEmpty {
            UserDefaults.standard.set(custom, forKey: Self.otaUrlDefaultsKey)
        }
        let savedOta = (UserDefaults.standard.string(forKey: Self.otaUrlDefaultsKey) ?? Self.defaultCloudOtaUrl)
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "/+$", with: "", options: .regularExpression)
        let savedLan = (UserDefaults.standard.string(forKey: Self.serverUrlDefaultsKey) ?? "http://192.168.178.22:3000")
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "/+$", with: "", options: .regularExpression)

        var candidates: [String] = []
        if !savedOta.isEmpty { candidates.append(savedOta) }
        if !candidates.contains(Self.defaultCloudOtaUrl) { candidates.append(Self.defaultCloudOtaUrl) }
        if !savedLan.isEmpty && !candidates.contains(savedLan) { candidates.append(savedLan) }

        tryNextOTACandidate(candidates: candidates, index: 0, manual: manual)
    }

    private func tryNextOTACandidate(candidates: [String], index: Int, manual: Bool) {
        if index >= candidates.count {
            if manual {
                DispatchQueue.main.async {
                    self.webView?.evaluateJavaScript("if(window.showToast) window.showToast('✅ Game is already up to date (or OTA server unreachable)');", completionHandler: nil)
                }
            }
            return
        }

        let baseUrl = candidates[index]
        let ts = Int(Date().timeIntervalSince1970)
        guard let manifestURL = URL(string: "\(baseUrl)/ota-manifest.json?t=\(ts)") else {
            tryNextOTACandidate(candidates: candidates, index: index + 1, manual: manual)
            return
        }

        var req = URLRequest(url: manifestURL)
        req.cachePolicy = .reloadIgnoringLocalAndRemoteCacheData
        req.timeoutInterval = 5.0

        URLSession.shared.dataTask(with: req) { [weak self] data, resp, err in
            guard let self = self else { return }
            guard err == nil,
                  let http = resp as? HTTPURLResponse, http.statusCode == 200,
                  let data = data,
                  let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let remoteSha = json["sha256"] as? String, !remoteSha.isEmpty else {
                self.tryNextOTACandidate(candidates: candidates, index: index + 1, manual: manual)
                return
            }

            let remoteVer = (json["version"] as? String) ?? "1.1.0"
            let currentSha = UserDefaults.standard.string(forKey: Self.otaShaDefaultsKey) ?? ""

            if remoteSha == currentSha {
                if manual {
                    DispatchQueue.main.async {
                        self.webView?.evaluateJavaScript("if(window.showToast) window.showToast('✅ Already on latest Cloud OTA version (v\(remoteVer))!');", completionHandler: nil)
                    }
                }
                return
            }

            // Download updated index.html and wan-multiplayer.js from baseUrl
            self.downloadAndApplyOTABundle(baseUrl: baseUrl, version: remoteVer, sha256: remoteSha)
        }.resume()
    }

    private func downloadAndApplyOTABundle(baseUrl: String, version: String, sha256: String) {
        let ts = Int(Date().timeIntervalSince1970)
        guard let indexUrl = URL(string: "\(baseUrl)/index.html?t=\(ts)"),
              let wanUrl = URL(string: "\(baseUrl)/wan-multiplayer.js?t=\(ts)") else { return }

        let session = URLSession.shared
        var indexReq = URLRequest(url: indexUrl, cachePolicy: .reloadIgnoringLocalAndRemoteCacheData, timeoutInterval: 10.0)
        var wanReq = URLRequest(url: wanUrl, cachePolicy: .reloadIgnoringLocalAndRemoteCacheData, timeoutInterval: 10.0)
        indexReq.httpMethod = "GET"
        wanReq.httpMethod = "GET"

        session.dataTask(with: indexReq) { [weak self] idxData, idxResp, idxErr in
            guard let self = self, idxErr == nil,
                  let http1 = idxResp as? HTTPURLResponse, http1.statusCode == 200,
                  let idxData = idxData,
                  let idxStr = String(data: idxData, encoding: .utf8),
                  idxStr.contains("<!DOCTYPE html>"), idxStr.contains("Mission Racer 3D") else { return }

            session.dataTask(with: wanReq) { [weak self] wanData, wanResp, wanErr in
                guard let self = self, wanErr == nil,
                      let http2 = wanResp as? HTTPURLResponse, http2.statusCode == 200,
                      let wanData = wanData, wanData.count > 256 else { return }

                let otaDir = self.otaDirectoryURL()
                try? FileManager.default.createDirectory(at: otaDir, withIntermediateDirectories: true)
                let otaIndex = otaDir.appendingPathComponent("index.html")
                let otaWan = otaDir.appendingPathComponent("wan-multiplayer.js")

                do {
                    try idxData.write(to: otaIndex, options: .atomic)
                    try wanData.write(to: otaWan, options: .atomic)
                    UserDefaults.standard.set(sha256, forKey: Self.otaShaDefaultsKey)
                    UserDefaults.standard.set(version, forKey: Self.otaVerDefaultsKey)

                    DispatchQueue.main.async {
                        self.notificationGenerator.notificationOccurred(.success)
                        self.webView?.evaluateJavaScript("if(window.showToast) window.showToast('🚀 Cloud OTA Update v\(version) Installed! Reloading...');", completionHandler: nil)
                        DispatchQueue.main.asyncAfter(deadline: .now() + 1.1) {
                            self.webView?.loadFileURL(otaIndex, allowingReadAccessTo: otaDir)
                        }
                    }
                } catch {}
            }.resume()
        }.resume()
    }

    // MARK: - WKScriptMessageHandler

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        switch message.name {
        case "haptics":
            guard let style = message.body as? String else { return }
            switch style {
            case "light":
                lightImpact.impactOccurred()
            case "medium":
                mediumImpact.impactOccurred()
            case "heavy":
                heavyImpact.impactOccurred()
            case "rigid":
                rigidImpact.impactOccurred()
            case "success":
                notificationGenerator.notificationOccurred(.success)
            default:
                lightImpact.impactOccurred()
            }
        case "serverConfig":
            if let serverUrl = message.body as? String {
                UserDefaults.standard.set(serverUrl, forKey: Self.serverUrlDefaultsKey)
            }
        case "voiceIntercom":
            if let action = message.body as? String {
                if action == "start" || action == "transmit" {
                    startNativeVoiceIntercom()
                } else if action == "stop" {
                    stopNativeVoiceIntercom()
                }
            }
        case "otaUpdate":
            let customUrl = message.body as? String
            checkForCloudOTAUpdate(customBaseUrl: customUrl, manual: true)
        default:
            break
        }
    }

    // MARK: - WKNavigationDelegate & WKUIDelegate
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.playback, mode: .default, options: [.mixWithOthers])
            try session.setActive(true)
        } catch {}
        webView.evaluateJavaScript("if(window.resumeWebAudioAfterMic) window.resumeWebAudioAfterMic();", completionHandler: nil)
    }

    @available(iOS 15.0, *)
    func webView(
        _ webView: WKWebView,
        requestMediaCapturePermissionFor origin: WKSecurityOrigin,
        initiatedByFrame frame: WKFrameInfo,
        type: WKMediaCaptureType,
        decisionHandler: @escaping (WKPermissionDecision) -> Void
    ) {
        decisionHandler(.grant)
    }

    // MARK: - Native iOS Push-To-Talk / Tap-To-Talk Compact AAC (.m4a) Recorder
    // Uses 12 kHz mono AAC (12 kbps) so a 2.2s voice clip is ~3.3 KB (~4.4 KB base64),
    // fitting inline in ntfy.sh JSON packets and playing natively on Android & iOS
    // while keeping AVAudioSession in .playback mode 99% of the time so WebAudio never mutes!
    private var aacRecorderSettings: [String: Any] {
        return [
            AVFormatIDKey: Int(kAudioFormatMPEG4AAC),
            AVSampleRateKey: 12000.0,
            AVNumberOfChannelsKey: 1,
            AVEncoderBitRateKey: 12000,
            AVEncoderAudioQualityKey: AVAudioQuality.medium.rawValue
        ]
    }

    private func restorePlaybackAudioSession() {
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.playback, mode: .default, options: [.mixWithOthers])
            try session.setActive(true)
        } catch {}
        webView?.evaluateJavaScript(
            "if(window.resumeWebAudioAfterMic) window.resumeWebAudioAfterMic(); if(window.unlockAudioOnUserGesture) window.unlockAudioOnUserGesture();",
            completionHandler: nil
        )
    }

    private func startNativeVoiceIntercom() {
        guard !isCapturingClip else { return }
        let session = AVAudioSession.sharedInstance()
        session.requestRecordPermission { [weak self] granted in
            DispatchQueue.main.async {
                guard let self = self else { return }
                if !granted {
                    self.webView?.evaluateJavaScript("if(window.onIosNativeMicStatus) window.onIosNativeMicStatus(false);", completionHandler: nil)
                    self.restorePlaybackAudioSession()
                    return
                }
                do {
                    try session.setCategory(
                        .playAndRecord,
                        mode: .default,
                        options: [.defaultToSpeaker, .allowBluetooth, .mixWithOthers]
                    )
                    try session.setActive(true)
                } catch {}

                let clipURL = FileManager.default.temporaryDirectory.appendingPathComponent("mr_ptt_voice.m4a")
                try? FileManager.default.removeItem(at: clipURL)

                do {
                    let rec = try AVAudioRecorder(url: clipURL, settings: self.aacRecorderSettings)
                    rec.isMeteringEnabled = true
                    rec.prepareToRecord()
                    rec.record()
                    self.clipRecorder = rec
                    self.isCapturingClip = true
                    self.isMonitoringVoice = true
                    self.webView?.evaluateJavaScript(
                        "if(window.onIosNativeMicStatus) window.onIosNativeMicStatus(true); if(window.onIosNativeVoiceState) window.onIosNativeVoiceState(true); if(window.showToast) window.showToast('🎙️ Recording 2s Voice Radio... Speak now!');",
                        completionHandler: nil
                    )
                } catch {
                    self.restorePlaybackAudioSession()
                    return
                }

                DispatchQueue.main.asyncAfter(deadline: .now() + 2.2) { [weak self] in
                    guard let self = self, self.isCapturingClip else { return }
                    self.clipRecorder?.stop()
                    self.clipRecorder = nil
                    self.isCapturingClip = false
                    self.isMonitoringVoice = false

                    self.restorePlaybackAudioSession()

                    if let data = try? Data(contentsOf: clipURL), data.count > 256 {
                        let b64 = data.base64EncodedString()
                        let js = "if(window.onIosNativeVoiceClip) window.onIosNativeVoiceClip('data:audio/mp4;base64,\(b64)');"
                        self.webView?.evaluateJavaScript(js, completionHandler: nil)
                    }
                    self.webView?.evaluateJavaScript(
                        "if(window.onIosNativeVoiceState) window.onIosNativeVoiceState(false); if(window.onIosNativeMicStatus) window.onIosNativeMicStatus(false); if(window.resumeWebAudioAfterMic) window.resumeWebAudioAfterMic(); if(window.unlockAudioOnUserGesture) window.unlockAudioOnUserGesture();",
                        completionHandler: nil
                    )
                }
            }
        }
    }

    private func stopNativeVoiceIntercom() {
        isMonitoringVoice = false
        isCapturingClip = false
        vadTimer?.invalidate()
        vadTimer = nil
        meterRecorder?.stop()
        meterRecorder = nil
        clipRecorder?.stop()
        clipRecorder = nil
        restorePlaybackAudioSession()
        webView?.evaluateJavaScript(
            "if(window.onIosNativeVoiceState) window.onIosNativeVoiceState(false); if(window.onIosNativeMicStatus) window.onIosNativeMicStatus(false);",
            completionHandler: nil
        )
    }
}
