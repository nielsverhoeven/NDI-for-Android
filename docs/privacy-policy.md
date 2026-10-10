---
title: Privacy Policy — NDI for Android
permalink: /privacy-policy/
---

# Privacy Policy — NDI for Android

_Last updated: 2026-10-10_

**NDI for Android** (package `com.ndi.android`) is an open-source app published by Niels Verhoeven
(source: <https://github.com/nielsverhoeven/NDI-for-Android>). It lets you discover NDI® video
sources on your local network, watch them, control PTZ cameras, and publish your device's camera,
microphone or screen as an NDI source.

## Summary

- The app does **not** collect, store or transmit any personal data to the developer or to any third party.
- There are no third-party analytics, advertising or crash-reporting SDKs in the app.
- There is no account, no login, no in-app purchase and no advertising.
- Everything the app remembers is stored only on your device.
- Video and audio are exchanged **only** with NDI devices on your own local network, and only while
  you start and keep a session running. The developer never receives them.

## Permissions and what they are used for

The app declares the following Android permissions. Each is used only for the purpose listed.

| Permission | Purpose |
|---|---|
| Camera (`CAMERA`) | Only when you choose "Front camera" or "Rear camera" as the Output video input. Frames are encoded and sent as an NDI stream to receivers on your local network. Nothing is saved. |
| Microphone (`RECORD_AUDIO`) | Only when "Send microphone audio" is switched on during Output. Audio is sent as the NDI audio track to receivers on your local network. Nothing is recorded to storage. |
| Screen capture (MediaProjection) | Only after you accept Android's screen-capture consent dialog, when "Screen" is the Output video input. The screen content is sent as an NDI stream to receivers on your local network. |
| Network (`INTERNET`, `ACCESS_WIFI_STATE`, `ACCESS_NETWORK_STATE`, `CHANGE_WIFI_MULTICAST_STATE`) | NDI discovery (mDNS/multicast), connecting to NDI sources and to NDI Discovery Servers you configure, and sending your NDI stream on the local network. Camera control commands (VISCA over IP) are sent only to the camera address you enter. The app does not contact any internet service of its own. |
| Foreground service (`FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_CAMERA`, `FOREGROUND_SERVICE_MICROPHONE`, `FOREGROUND_SERVICE_MEDIA_PROJECTION`) and notifications (`POST_NOTIFICATIONS`) | Keeps your camera, microphone or screen stream running while the app is in the background and shows the "NDI Output Active" notification with a Stop button, as Android requires. |
| Wake lock (`WAKE_LOCK`) | Keeps Wi-Fi responsive (disables Wi-Fi power saving) while you watch a stream. |

## Data stored on your device

Settings (theme, developer mode), the NDI Discovery Servers you add, recently seen NDI sources,
viewing and output history, and an optional diagnostic log are kept in a local SQLite database in
the app's private storage. No data is written anywhere else, and no data is sent to the developer.

The app allows Android's standard app backup (`android:allowBackup="true"`), so Android may include
this data in your own device backup (for example your Google account backup) under the rules of your
device and account; the developer has no access to that backup. You can erase all of the app's data
by clearing the app's storage in Android Settings or by uninstalling the app.

## Data shared with others

Live video and audio you choose to publish are sent to NDI receivers (for example video mixers,
recorders or monitoring software) on your local network that connect to your stream. Who can receive
it depends on your network and is under your control. The developer never receives it and has no way
to see it.

## Children

The app is a production tool. It is not directed at children and contains no advertising, purchases
or social features.

## Changes to this policy

Changes to this policy are published at this address with a new "Last updated" date. The version in
the app's source repository history shows what changed.

## Contact

Questions about this policy or about the app's handling of data:

- GitHub Issues: <https://github.com/nielsverhoeven/NDI-for-Android/issues>
- or the developer e-mail address shown on the app's Google Play listing.

NDI® is a registered trademark of Vizrt NDI AB.
