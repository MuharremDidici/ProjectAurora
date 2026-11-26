# Project Aurora — Feature & Verification Matrix (`v7.2.0`, Saf Rust)

| Domain | Implemented Pure Rust / WebView2 Components | Automated Crate Tests |
|---|---|---|
| **Multi-Monitor Dock & Native AppBar** | `aurora_win32::appbar::NativeAppBarManager`, `aurora_core::models`, `dock.html` | `aurora-core`, `aurora-win32`, `aurora-app` (`cargo test --workspace`) |
| **Window Activation & Alt+Tab Focus Safety** | `aurora_win32::hooks_and_overlays`, `hardware_osd`, `windows_catalog`, `webview_host.rs` | `aurora-win32::tests::alt_drag_strictly_excludes_turkish_altgr_and_shell_switchers` |
| **10 Shell Architectures, 20 Themes & 2D HSV** | `aurora_core::tokens`, `aurora_core::models`, `crown.html`, `dock.html` | `all_v7_architectures_and_island_styles_resolve_cleanly`, `all_twenty_themes_exceed_wcag_aa_on_primary_secondary_and_accent` |
| **Dynamic Island (`Aurora Crown`) & Live OSD** | `aurora_win32::hardware_osd`, `audio`, `bluetooth`, `media`, `crown.html` | `aurora-win32::tests` (volume step, Live BT, GSMTC STA) |
| **Real Win11 System Tray & OmniSearch** | `aurora_win32::privacy_and_metrics` (`NotifyIconSettings`), `aurora_core::calc`, `bridge.rs` | `notify_icon_registry_and_tray_query_return_deduplicated_apps`, `turkish_fuzzy_search_matches_diacritics` |
| **10 Desktop Widgets (`HWND_BOTTOM`) & AI Quota** | `widgets.html`, `aurora_storage::widgets_store`, `aurora_win32::ai_quota` (DPAPI) | `widgets_store::tests`, `ai_quota::tests` |
| **Persistence, Updater & Zero-UAC Setup** | `aurora_storage::updater`, `aurora-setup`, Watchdog (`--watchdog-pid`) | `updater::tests`, `aurora-setup::tests`, live `SHA256_MATCH` verification |
