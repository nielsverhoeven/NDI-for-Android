# store/

Google Play Console material for NDI for Android: `listing.md` (copy, declarations, Data-safety and content-rating answers, closed-testing steps) plus the binary assets it lists (`icon-512.png`, `feature-graphic-1024x500.png`, `screenshots/{phone,tablet-7,tablet-10}/*.jpg`).
This folder is deliberately outside `docs/`, so GitHub Pages does not publish it; it is versioned so the listing copy can be diffed against the code (trademark line, privacy URL in `SettingsViewModel`).
To regenerate screenshots: install the current release build with the `android-build-install-run` skill, open each screen listed in `listing.md` §4, run `adb exec-out screencap -p > shot.png`, convert to JPEG and place it in the matching subfolder (phone: Galaxy S25/S21 Ultra; tablets: Galaxy Tab A9+).
Keep the folder around 10 MB; `.gitattributes` already treats `*.png`/`*.jpg` as binary.
