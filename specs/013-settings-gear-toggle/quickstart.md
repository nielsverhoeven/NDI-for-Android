# Quickstart: Settings Gear Toggle

**Feature**: 013-settings-gear-toggle  
**Date**: March 23, 2026  

## Overview

Add a gear icon in the top right corner of main screens that toggles the visibility of a settings bottom sheet.

## Prerequisites

- Settings bottom sheet component exists (BottomSheetDialogFragment)
- Top app bar menus configured in each main screen

## Implementation Steps

1. **Update Menu XML**: Change showAsAction from "ifRoom" to "always" for action_settings in menu files
2. **Create Settings Bottom Sheet**: Implement SettingsBottomSheetDialogFragment with configuration UI
3. **Modify Screen Logic**: Update SourceListScreen, ViewerScreen, OutputScreen to handle toggle instead of navigation
4. **Add State Management**: Track bottom sheet visibility state in ViewModels
5. **Update Tests**: Modify navigation tests to verify toggle behavior

## Key Code Changes

### Menu Updates
```xml
<item
    android:id="@+id/action_settings"
    android:icon="@android:drawable/ic_menu_manage"
    android:title="@string/settings_title"
    app:showAsAction="always" />
```

### Screen Updates
```kotlin
// In onCreateView or similar
binding.topAppBar.setOnMenuItemClickListener { item ->
    when (item.itemId) {
        R.id.action_settings -> {
            viewModel.onSettingsToggle()
            true
        }
        // other items
    }
}
```

### ViewModel Updates
```kotlin
fun onSettingsToggle() {
    if (settingsSheet.isVisible) {
        settingsSheet.dismiss()
    } else {
        settingsSheet.show()
    }
}
```

## Testing

- Unit tests for ViewModel toggle logic
- UI tests for icon visibility and tap behavior
- Playwright e2e for full toggle flow and regression