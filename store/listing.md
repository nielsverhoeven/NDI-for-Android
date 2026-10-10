# NDI for Android — Google Play Console material (closed testing)

Everything the Play Console asks for on the closed-testing track, in the order the *Dashboard → Set up your app* list presents it. Copy is paste-ready; every requirement carries the Google page it was taken from. Binary assets live next to this file (see §4). The in-app counterparts (About panel trademark line, privacy-policy link) are `SettingsViewModel.NdiTrademarkNotice` / `SettingsViewModel.PrivacyPolicyUrl` — keep the strings here identical to those constants.

Sources used throughout:

| Topic | Google page |
|---|---|
| Listing assets and specs | https://support.google.com/googleplay/android-developer/answer/9866151 |
| App name, descriptions, keyword policy | https://support.google.com/googleplay/android-developer/answer/9859152 |
| Category and tags | https://support.google.com/googleplay/android-developer/answer/113475 |
| Content rating (IARC) | https://support.google.com/googleplay/android-developer/answer/9859655 |
| Privacy policy requirement | https://support.google.com/googleplay/android-developer/answer/9859455 |
| Data safety form | https://support.google.com/googleplay/android-developer/answer/10787469 |
| Foreground-service declarations | https://support.google.com/googleplay/android-developer/answer/13392821 |
| Closed testing | https://support.google.com/googleplay/android-developer/answer/9845334 |
| Releases and release notes | https://support.google.com/googleplay/android-developer/answer/9859348 |
| Personal-account testing requirements (12 testers × 14 days) | https://support.google.com/googleplay/android-developer/answer/14151465 |
| Play App Signing / upload key | https://support.google.com/googleplay/android-developer/answer/9842756 |
| Target API level requirement | https://developer.android.com/google/play/requirements/target-sdk |
| 16 KB page size requirement | https://developer.android.com/guide/practices/page-sizes |
| Bundle size limits | https://support.google.com/googleplay/android-developer/answer/9859372 |

---

## 1. App details

| Field | Value | Note / source |
|---|---|---|
| App name | **NDI for Android** | 15 chars, limit 30 (answer/9859152) |
| Package name | `com.ndi.android` | fixed decision; cannot change after the first upload |
| App or game | App | |
| Free or paid | Free | cannot be changed to paid later |
| Default language | English (United States) — en-US | |
| Category | **Video Players & Editors** (fallback: **Tools**) | set under *Grow users → Store presence → Store settings*; pick from the dropdown — the authoritative list could not be fetched (answer/113475) |
| Tags | up to 5, chosen from Play's fixed list (answer/113475) | suggestions, not verified against the live list: Video streaming, Live streaming, Video players, Remote control |
| Contact — website | `https://github.com/nielsverhoeven/NDI-for-Android` | |
| Contact — support | `https://github.com/nielsverhoeven/NDI-for-Android/issues` | the public feedback channel; also used as the closed-track feedback channel (§10) |
| Contact — e-mail | the address on the developer account | mandatory on the listing; no e-mail is kept in this public repo (architect C10). The privacy policy refers to "the developer e-mail published on the app's Google Play listing" |
| Privacy policy | `https://nielsverhoeven.github.io/NDI-for-Android/privacy-policy/` | trailing-slash form, see §9 |
| Target API | 36 — already satisfied by every release artifact | Play requires API 36 for new apps/updates since 31 Aug 2026 (developer.android.com/google/play/requirements/target-sdk). The value comes from the TFM `net10.0-android` with the Android 36.x workload; there is no csproj knob. CI asserts it on the AAB |
| 16 KB page size | compliant for `arm64-v8a` | all `LOAD` segments of every 64-bit `.so` are `0x4000`-aligned; CI asserts it (developer.android.com/guide/practices/page-sizes) |
| Bundle size | ~44 MB release APK; AAB base module limit is 500 MB | answer/9859372 — not a concern |
| ABIs shipped | `arm64-v8a` + `x86_64` | Release RIDs are `android-arm64;android-x64`; `libndi.so` exists only for arm64 → NDI is soft-disabled on x86_64 (emulators, some Chromebooks). Reflected in the listing copy (§3) |

---

## 2. Short description

Limit 80 characters (answer/9866151).

```
Watch and send NDI® video on your local network, with PTZ camera control.
```

73 characters.

---

## 3. Full description

Limit 4 000 characters (answer/9866151). Avoid superlatives ("best", "fastest") and keyword repetition — "Repetitive or irrelevant use of keywords … can result in an app being suspended" (answer/9859152). The last line is mandatory (trademark decision).

```
NDI for Android turns your phone or tablet into a monitor, a camera controller and a wireless camera for any NDI® production network — no cloud, no account, nothing leaves your local network.

WATCH
• Discovers NDI sources on your Wi-Fi automatically (mDNS) and via NDI Discovery Servers you add
• Tap a source to view it with low-latency playback, audio, tally echo and live stats
• Quality profiles (Smooth / Balanced / High) so weaker Wi-Fi links still keep up
• Full-screen viewer with auto-hiding controls; phones rotate into full screen automatically
• Reconnects by itself when a stream drops, and remembers your last source

CONTROL
• PTZ cameras: pan, tilt, zoom and presets straight from the viewer
• VISCA-over-IP control for cameras behind SDI/HDMI-to-NDI converters

SEND
• Publish your device screen, front camera or rear camera as a full-bandwidth NDI source
• Optional microphone audio track
• Stream name of your choice; live connection count in the notification; Stop from the shade

ALSO
• Connection history, ndi:// deep links to jump straight to a source
• Light, dark and system themes with accent colours
• Developer mode with a diagnostic log for troubleshooting a link

NOTES
• Requires NDI senders/receivers on the same local network (e.g. OBS, vMix, TriCaster, NDI Tools, PTZ cameras). This app does not provide NDI sources itself.
• Full-bandwidth 1080p60 reception needs a good 5 GHz Wi-Fi link and a recent device; the Smooth profile is recommended on mid-range tablets.
• NDI|HX (H.264/HEVC) sources may not decode on Android yet.
• Requires a 64-bit ARM device (arm64-v8a). NDI is unavailable on x86_64 devices such as emulators and some Chromebooks.

Open source: https://github.com/nielsverhoeven/NDI-for-Android
Privacy policy: https://nielsverhoeven.github.io/NDI-for-Android/privacy-policy/

NDI® is a registered trademark of Vizrt NDI AB.
```

---

## 4. Graphics checklist

Specs from answer/9866151. Files are versioned in this folder; the coordinator supplies them. Screenshots are JPEG to keep `store/` around 10 MB (`.gitattributes` already marks `*.png`/`*.jpg` binary).

| # | Asset | Play requirement | File(s) under `store/` | Content |
|---|---|---|---|---|
| 1 | App icon | 512 × 512, 32-bit PNG with alpha, ≤ 1024 KB. Play applies its own rounded mask: full-bleed square, no baked-in rounded corners or shadow | `icon-512.png` | downscale of `src/MauiApp/Platforms/Android/Resources/drawable/ndi_logo.png` (1024 × 1024 RGBA) |
| 2 | Feature graphic | 1024 × 500, JPEG or 24-bit PNG, **no alpha**. Required for publishing | `feature-graphic-1024x500.png` | dark background, logo, tagline "Watch and send NDI® video on your LAN" |
| 3 | Phone screenshots | 2–8 per device type (min. 2 across all types to publish); 320–3840 px per side; long side ≤ 2 × short side. For featuring: ≥ 4 at ≥ 1080 px, 16:9 (≥ 1920 × 1080) or 9:16 (≥ 1080 × 1920) | `screenshots/phone/*.jpg` | Galaxy S25 / S21 Ultra: Home, View tab with sources, Viewer portrait, Viewer landscape full screen with PTZ overlay, Output tab, Settings → About showing the trademark line |
| 4 | 7-inch tablet screenshots | up to 8; 1080–7680 px; 16:9 or 9:16; "a minimum of 4 … to demonstrate your in-app experience" for large-screen eligibility | `screenshots/tablet-7/*.jpg` | Galaxy Tab A9+ (SM-X210R): two-pane View + viewer deck, Viewer full screen, Output, Settings |
| 5 | 10-inch tablet screenshots | same as 7-inch | `screenshots/tablet-10/*.jpg` | same set (Play accepts the same images in both slots) |
| 6 | Promo video (optional) | public or unlisted YouTube URL; no playlist or timecodes; ads off; embeddable | — (URL only) | reuse the foreground-service demo footage (§8) |

Not a listing asset but worth knowing: the app ships a plain `@drawable/ndi_logo` launcher icon, no `<MauiIcon>` adaptive icon. Android 8+ therefore shows it inside a white/black square. Optional follow-up, not needed for the closed track.

---

## 5. Content rating questionnaire (IARC)

Where: *Policy and programs → App content → Content ratings*. An e-mail address is required ("used for correspondence with International Age Rating Coalition") — answer/9859655.

| Question | Answer | Basis |
|---|---|---|
| Category | **Utility, Productivity, Communication, or Other** | production tool |
| Violence, sexuality, language, controlled substances, gambling, fear/horror themes | No to all | no such content in the app |
| Does the app allow users to interact or exchange content with other users? | **Yes** — users can send their camera, screen and microphone as a video/audio stream to other devices on their local network | user-controlled, not moderated by the developer, never stored or routed through a server. Answer this honestly; the researcher's expected outcome is *Everyone / PEGI 3 / USK All ages* ("utility programs, product catalogues or tool apps" is the USK example) |
| Does the app share the user's location with other users? | No | no location permission |
| Does the app allow users to purchase digital goods? | No | no purchases |
| Does the app contain advertising? | No | no ads SDK |

The rating is computed by the questionnaire; the outcome above is an expectation, not a guarantee. The exact question wording in the console may differ from the paraphrases here.

---

## 6. App access

Where: *Policy and programs → App content → App access*.

Answer: **"All functionality in my app is available without special access"** (no login, no account, no in-app purchase).

Add this note for the reviewer in the instructions field:

```
No login or account exists. The app needs an NDI® sender (e.g. OBS with the NDI plugin, NDI Tools "Test Patterns", vMix, or an NDI PTZ camera) on the same Wi-Fi/LAN to show video; without one the View tab stays empty. The demo video linked in the foreground-service declaration shows the full flow.
```

---

## 7. Data safety

Where: *Policy and programs → App content → Data safety*. Play's definitions (answer/10787469): "'Collect' means transmitting data from your app off a user's device"; data access "solely on the user's device" is not collection "as long as the data is never transmitted off the user's device"; "Developers with no user data collection must still complete the form and provide a privacy policy link".

Verified basis: the app has no analytics, advertising or crash-reporting SDK (`NdiForAndroid.csproj` and `NdiForAndroid.Core.csproj` reference only MAUI, sqlite-net-pcl, Logging.Debug, SkiaSharp, CommunityToolkit.Mvvm); network code is NDI P/Invoke (LAN discovery/receive/send), VISCA-over-TCP to a camera address the user enters, and reachability checks. Nothing talks to a developer-controlled server. Settings, discovery servers, recent sources, viewing/output history and the diagnostic log stay in a local SQLite database; `android:allowBackup="true"` means Android may include them in the user's own Google account backup.

| Question | Answer | Basis |
|---|---|---|
| Does your app collect or share any of the required user data types? | **No** | nothing leaves the device to the developer or any third party |
| Is all of the user data collected by your app encrypted in transit? | not asked when the answer above is No | — |
| Do you provide a way for users to request that their data is deleted? | not asked when the answer above is No | — |
| Independent security review (optional) | leave unchecked | — |
| Privacy policy URL (separate task, *App content → Privacy policy*) | `https://nielsverhoeven.github.io/NDI-for-Android/privacy-policy/` | mandatory because the app uses camera and microphone (answer/9859455) |

**LAN-streaming nuance.** The NDI output stream does transmit camera/microphone/screen content off the device — to NDI receivers on the user's own local network that the user chooses to connect. Google's text has no explicit carve-out for peer-to-peer/local-network transfers; the verified exemptions are on-device processing and (for *sharing*) user-initiated transfers to a party the user expects. The defensible position behind the "No" answer is the explicit privacy-policy wording ("sent only to NDI receivers on your own network that you connect; the developer never receives it") plus the foreground-service declarations in §8. **Fallback if a reviewer objects** (no code change): declare *Photos and videos* and *Audio* as collected, not shared, processed ephemerally, required for the app's functionality. Decide only if asked. Google's treatment of local-network streaming specifically could not be verified.

Other *App content* declarations on the dashboard (confirm the list in the console):

| Declaration | Answer |
|---|---|
| Ads | No |
| Target audience and content | 18 and over (the app is a production tool; avoids the Families policy) |
| News app | No |
| Government app | No |
| Financial features | None |
| Health apps | No |
| Advertising ID | No (no ads SDK; MAUI does not pull `play-services-ads-identifier`) |
| Foreground service permissions | §8 |

Guardrail: `src/MauiApp/Services/TelemetryService.cs` carries a TODO to wire Application Insights. Any off-device telemetry requires updating `docs/privacy-policy.md` and this form first.

---

## 8. Foreground-service declarations

Where: *Policy and programs → App content → Foreground service permissions*. Play rule: "When your apps target Android 14 and above, you'll need to declare any foreground service types that you use"; per type you give a description, the user impact if deferred, a use case, and "a link to a video demonstrating each foreground service feature" (answer/13392821).

What the app declares: one service, `com.ndi.android.ScreenShareForegroundService`, with `android:foregroundServiceType="mediaProjection|camera|microphone"`; manifest permissions `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION`, `FOREGROUND_SERVICE_CAMERA`, `FOREGROUND_SERVICE_MICROPHONE`, `CAMERA`, `RECORD_AUDIO`, `POST_NOTIFICATIONS`. All three types have a real code path; none is declared-but-unused. At runtime the service starts with `mediaProjection` only for the "Screen" input, `camera` for the front/rear camera inputs when `CAMERA` is granted, and `microphone` whenever `RECORD_AUDIO` is granted.

### 8.1 Common statement (append to each declaration)

```
No media is stored by the app or sent to any server; frames and audio are only delivered to NDI® receivers on the user's local network that the user connects.
```

### 8.2 mediaProjection

Use case to select: **"Project media to non-primary display or external devices using the MediaProjection APIs, including streaming"**.

```
NDI for Android lets the user publish the device screen as a live NDI® video source on the local network (Output tab → Video Input "Screen" → Start Output). After the user accepts Android's screen-capture consent dialog, the app runs a foreground service of type mediaProjection that owns the MediaProjection session, reads frames from a VirtualDisplay and hands them to the NDI sender while the user switches to other apps. The ongoing notification shows the stream name and live connection count and has a Stop action. If the system deferred or interrupted the service the NDI receivers (video switchers, recorders) would lose the feed mid-production.
```

### 8.3 camera

Use case to select: **"Continue to access the camera from the background"** (video streaming).

```
In Output → Video Input "Front camera"/"Rear camera" the app captures the camera with Camera2 and publishes it as an NDI® source on the LAN, acting as a wireless production camera. The foreground service of type camera keeps the capture running when the screen is locked or the user moves to another app; the notification shows connections and offers Stop. Interruption would cut the camera feed to every connected receiver.
```

### 8.4 microphone (corrected wording)

Use case to select: **audio capture for live streaming**.

```
When microphone permission has been granted, the service declares the microphone type so the user can enable "Send microphone audio" during a running screen or camera session; audio is captured only while that switch is on. With the switch on, the app captures 48 kHz PCM with AudioRecord and sends it as the NDI® audio track of the stream. The foreground service of type microphone keeps that audio flowing while the app is in the background; the same notification and Stop action apply. Interruption would silence the live stream.
```

Why the wording: `ScreenShareForegroundService.GetGrantedServiceTypes` adds the microphone type whenever `RECORD_AUDIO` is granted, regardless of the "Send microphone audio" switch. The service type is a capability, not a capture, so the declaration must cover camera and screen sessions as well. Gating the type on the switch is a separate code tidy-up, not a Play prerequisite.

### 8.5 Demo-video shot list

One unlisted YouTube video (or Drive link). Show the whole flow unedited; one video can serve all three declarations and the optional promo slot.

1. Launch the app, Home tab; show the Output status card.
2. Output tab: Video Input picker → "Screen"; stream name visible; toggle "Send microphone audio" on; tap **Start Output**.
3. Android's MediaProjection consent dialog ("Start recording or casting with NDI for Android?") → tap Start/Allow. Show the microphone permission prompt if it appears.
4. Pull down the shade: the **"NDI Output Active — Streaming: … — N connections"** notification with its **Stop** action.
5. Press Home, open another app → the notification persists (background operation).
6. On a laptop beside the phone: NDI Studio Monitor or OBS showing the phone's screen as a source (proves "project to external device"); show the connection count change on the notification.
7. Back in the app: **Stop Output** → the notification disappears.
8. Repeat 2–7 with Video Input "Rear camera" (camera permission prompt, camera feed visible on the laptop) — covers `camera` and `microphone`.
9. Finish with the Stop action from the notification itself.

---

## 9. Privacy policy URL

```
https://nielsverhoeven.github.io/NDI-for-Android/privacy-policy/
```

- Use this exact string (trailing slash) in the Play Console field, the store listing (§3) and the app constant `SettingsViewModel.PrivacyPolicyUrl`. The page declares `permalink: /privacy-policy/`, so this is its canonical URL.
- Requirement: apps that access camera or microphone "must link to a policy on the store listing and within the app"; the policy must be on "an active URL", apply to this app, and "comprehensively disclose how your app collects, uses, and shares user data" (answer/9859455). Entered under *Policy and programs → App content → Privacy policy*. The "within the app" clause is met by the tappable "Privacy policy" line in Settings → About.
- The page is served by GitHub Pages from `main` / `/docs` (Jekyll). Pages is a repository setting Niels enables after the docs PR merges; verify `curl -sI <url>` returns 200 before creating the Play release.

---

## 10. Closed testing

Sources: answer/9845334 (closed testing), answer/9859348 (releases and notes), answer/14151465 (personal-account requirements).

### 10.1 Click path (Play Console)

1. **Create app**: name "NDI for Android", App, Free, default language en-US. Fill the store-listing contact details, including the developer e-mail (mandatory).
2. **Dashboard → Set up your app** — complete every task: App access (§6), Ads = No, Content rating (§5), Target audience 18+, Data safety (§7), Privacy policy (§9), Government/Financial/Health/News = No, Advertising ID = No, Foreground service permissions (§8, with the demo-video link), Store settings → category (§1), Main store listing → copy (§2–3), icon, feature graphic, screenshots (§4). "If **Create new release** is disabled, complete any outstanding setup tasks listed on your Dashboard page."
3. **Test and release → Setup → App signing**: accept the Google-generated app-signing key (default; Play describes it as "quantum-ready, hybrid signing with Google-generated keys"). The key you sign bundles with is the *upload key*: "You use this key to sign your app bundle before uploading it to the Play Console" (answer/9842756). After the first upload confirm that the upload-key certificate shown has SHA-256 `FC:F1:B4:53:B7:82:A0:B9:BB:AE:3D:48:F8:61:2B:8A:D3:F7:85:65:AF:85:86:4B:18:AA:0B:07:1C:C8:42:71` (the repo's release keystore, KNOWLEDGE-BASE "Release signing"). That Play registers the first-upload key automatically is console behaviour, not stated on the fetched page — confirm on screen.
4. **Testing → Closed testing** → the default closed track ("Alpha") → *Manage track*.
   - **Testers tab → Create email list**: name `ndi-beta`; addresses comma-separated or CSV — "Play Console does not accept CSV files that are in UTF-8 with BOM format"; up to 2 000 addresses per list.
   - **Feedback channel**: `https://github.com/nielsverhoeven/NDI-for-Android/issues` (or the developer e-mail).
   - **Countries**: Netherlands (+ others as needed). "Changes to distributed countries and regions apply across all tracks."
5. **Create new release** → upload `com.ndi.android-Signed.aab` taken from the GitHub release whose tag matches the `versionCode` you want on Play (first upload: the release that contains the trademark line and privacy link; versionCode must always rise — Play rejects a reused or lower code) → release name auto-fills from the version name → release notes (§10.4) → *Save as draft → Next → Preview and confirm → Start rollout*. The first closed-testing release goes through Google review: "usually take seven days or less".
6. **Opt-in link**: appears on the track page only "when an app status is 'Published'" and "can take several hours to become available to testers". Copy it and send the tester instructions (§10.3).
7. Keep uploading newer `versionCode`s to the same track (e.g. the integration release with the #449 fix). The 14-day opt-in clock is per tester, not per build.

### 10.2 Tester list rules

- Invite 15–20 people so that at least 12 survive the 14 days.
- Testers must use the Google account that is on the list; the opt-in link only works for listed addresses.
- "Testers who opt in, test for less than 14 days, and then opt out don't count."
- Each tester needs a 64-bit ARM Android device (API 26+); NDI is unavailable on x86_64 devices such as emulators and some Chromebooks.

### 10.3 Tester instructions (send with the opt-in link)

```
Thanks for testing NDI for Android. Five steps:

1. Uninstall the GitHub APK first. Play-installed builds are signed with a different key, so Android refuses to update a sideloaded install (and vice versa). Uninstall, then install from Play.
2. On your phone or tablet, sign in to Google with the account you were invited with.
3. Open the opt-in link: <opt-in link> and tap "Become a tester".
4. Install the app from the Play Store link on that page (it can take a few hours after you opt in before the install button appears).
5. Stay opted in for at least 14 days — Google only counts testers who remain opted in that long. Updates arrive through Play like any other app.

You need an NDI® sender on the same Wi-Fi (e.g. OBS with the NDI plugin, NDI Tools, vMix or an NDI PTZ camera) to see video. Please report problems at https://github.com/nielsverhoeven/NDI-for-Android/issues with your device model and Android version.
```

### 10.4 Release-notes template

Limit 500 Unicode characters per language; the `<en-US>` tags go on their own lines (answer/9859348). Verbatim from the architect verdict §4.5:

```
<en-US>
NDI for Android 0.20.x (closed beta)
• Discover and watch NDI® sources, PTZ control (NDI, VISCA over IP), full-screen viewer
• Send your screen, camera or microphone as an NDI source
Fixed: tablet portrait full screen now hides the tab bar (#449)
Known limitations:
• NDI|HX (H.264/HEVC) sources may show no video (#435, unverified)
• 1080p60 full-bandwidth may drop frames on mid-range tablets (e.g. Galaxy Tab A9+); use the Smooth profile (#451)
Uninstall the GitHub APK before installing from Play.
</en-US>
```

Notes:
- Measured length of the text above: **501 characters between the tags, 518 including the `<en-US>`/`</en-US>` lines** — one over the 500 limit. Verify in the console counter; if it objects, cut the "Fixed" line first (437 / 454 chars without it).
- **The "Fixed … (#449)" line belongs only to the AAB that contains PR #455** (the integration release, v0.20.40 or later). For an earlier upload (v0.20.39) delete that line.
- Replace `0.20.x` with the version name of the uploaded bundle.

---

## 11. Production gate

Personal developer accounts created after 13 Nov 2023 must run a closed test with "a minimum of 12 testers who have been opted in continuously for at least 14 days" before they can apply for production (answer/14151465).

1. Watch the tester count on the closed-testing track page; keep the track running and keep uploading newer `versionCode`s.
2. When 12 testers have been opted in for 14 consecutive days: *Dashboard → Apply for production*. The form has three sections: about the closed test, about the app, production readiness.
3. Google reviews the application ("usually take seven days or less"). Open testing becomes available only after production access is granted.
4. Applying is optional — closed testing can continue indefinitely.

---

## 12. Known limitations to repeat in the notes

Carry these into every release note and into tester communication until the issues close:

| Issue | Limitation | Facts |
|---|---|---|
| #435 | NDI\|HX (H.264/HEVC) sources may show no video on Android — **unverified** | the bundled `libndi.so` loads `libavcodec(-ndi).so.61` at runtime; none is bundled and there is no MediaCodec path, so HX decode has never been verified (`docs/ndi-sdk-coverage.md`) |
| #451 | 1080p60 full-bandwidth reception may drop frames on mid-range tablets such as the Galaxy Tab A9+ (SM-X210R, Android 16); use the Smooth profile | receiver-side ceiling inside the SDK receive stage, independent of Wi-Fi: 1080p full-bandwidth ≈ 208–286 ms per frame vs Smooth ≈ 60 ms (tablet gate 2026-10-10, v0.20.37) |

Resolved, do not list as a limitation: #449 (tablet portrait full screen left the Shell tab bar visible) — fixed by PR #455; use the "Fixed" line in §10.4 for bundles that contain it.

---

NDI® is a registered trademark of Vizrt NDI AB.
