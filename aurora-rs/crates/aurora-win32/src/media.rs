use std::sync::OnceLock;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use aurora_core::MediaSessionSnapshot;
use crossbeam_channel::{bounded, unbounded, Sender};
use windows::Media::Control::{
    GlobalSystemMediaTransportControlsSession, GlobalSystemMediaTransportControlsSessionManager,
    GlobalSystemMediaTransportControlsSessionPlaybackStatus,
};
use windows::Storage::Streams::{DataReader, IRandomAccessStreamReference};
use windows::Win32::System::WinRT::{RoInitialize, RO_INIT_MULTITHREADED};
use windows::Win32::UI::Input::KeyboardAndMouse::{
    SendInput, INPUT, INPUT_0, INPUT_KEYBOARD, KEYBDINPUT, KEYEVENTF_EXTENDEDKEY,
    KEYEVENTF_KEYUP, VIRTUAL_KEY, VK_MEDIA_NEXT_TRACK, VK_MEDIA_PLAY_PAUSE, VK_MEDIA_PREV_TRACK,
};

enum MediaReq {
    Query(Sender<MediaSessionSnapshot>),
    Command(String),
    Seek(u32),
}

/// Builds the canonical track cache key (`title|artist|app_name`).
pub fn media_track_cache_key(title: &str, artist: &str, app_name: &str) -> String {
    format!("{title}|{artist}|{app_name}")
}

/// Single-slot track thumbnail cache owned by the WinRT MTA worker to avoid
/// re-opening and re-reading `IRandomAccessStreamReference` streams on unchanged tracks.
#[derive(Debug, Default, Clone)]
pub struct MediaThumbnailCache {
    last_key: Option<String>,
    last_bytes: Option<Vec<u8>>,
}

impl MediaThumbnailCache {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn last_key(&self) -> Option<&str> {
        self.last_key.as_deref()
    }

    pub fn get_or_fetch<F>(&mut self, title: &str, artist: &str, app_name: &str, fetch: F) -> Option<Vec<u8>>
    where
        F: FnOnce() -> Option<Vec<u8>>,
    {
        let key = media_track_cache_key(title, artist, app_name);
        if self.last_key.as_deref() == Some(key.as_str()) {
            return self.last_bytes.clone();
        }
        let bytes = fetch();
        self.last_key = Some(key);
        self.last_bytes = bytes.clone();
        bytes
    }

    pub fn clear(&mut self) {
        self.last_key = None;
        self.last_bytes = None;
    }
}

/// Persistent WinRT MTA (`RO_INIT_MULTITHREADED`) media worker.
/// Calling WinRT `IAsyncOperation::get()` on an STA (`COINIT_APARTMENTTHREADED`) thread without
/// a message pump can fail or stall; running GSMTC on a dedicated MTA thread guarantees instant,
/// reliable Spotify / YouTube / Opera / Chrome / Edge / VLC detection from any caller thread!
fn media_worker_tx() -> &'static Sender<MediaReq> {
    static TX: OnceLock<Sender<MediaReq>> = OnceLock::new();
    TX.get_or_init(|| {
        let (tx, rx) = unbounded::<MediaReq>();
        let _ = std::thread::Builder::new()
            .name("aurora-winrt-media".into())
            .spawn(move || {
                let _ = unsafe { RoInitialize(RO_INIT_MULTITHREADED) };
                let mut cached_mgr: Option<GlobalSystemMediaTransportControlsSessionManager> = None;
                let mut thumb_cache = MediaThumbnailCache::new();

                while let Ok(req) = rx.recv() {
                    if cached_mgr.is_none() {
                        cached_mgr = GlobalSystemMediaTransportControlsSessionManager::RequestAsync()
                            .ok()
                            .and_then(|op| op.get().ok());
                    }
                    match req {
                        MediaReq::Query(reply) => {
                            let snap = match cached_mgr.as_ref() {
                                Some(mgr) => {
                                    let res = query_from_manager(mgr, &mut thumb_cache);
                                    if !res.has_session {
                                        // Refresh manager handle in case Shell / Media service restarted
                                        if let Some(fresh) =
                                            GlobalSystemMediaTransportControlsSessionManager::RequestAsync()
                                                .ok()
                                                .and_then(|op| op.get().ok())
                                        {
                                            let r2 = query_from_manager(&fresh, &mut thumb_cache);
                                            cached_mgr = Some(fresh);
                                            r2
                                        } else {
                                            res
                                        }
                                    } else {
                                        res
                                    }
                                }
                                None => {
                                    thumb_cache.clear();
                                    MediaSessionSnapshot::default()
                                }
                            };
                            let _ = reply.send(snap);
                        }
                        MediaReq::Command(cmd) => {
                            let handled = cached_mgr
                                .as_ref()
                                .is_some_and(|mgr| dispatch_smtc_command(mgr, &cmd));
                            if !handled {
                                send_hardware_media_key(&cmd);
                            }
                        }
                        MediaReq::Seek(target_secs) => {
                            if let Some(mgr) = cached_mgr.as_ref() {
                                let _ = dispatch_smtc_seek(mgr, target_secs);
                            }
                        }
                    }
                }
            });
        tx
    })
}

/// Native WinRT GSMTC Media Session query (Spotify, YouTube, Edge, Opera, Chrome, VLC).
/// Safe to call from ANY thread (STA UI, STA telemetry, or MTA) with bounded timeout.
pub fn query_media_session() -> MediaSessionSnapshot {
    let (reply_tx, reply_rx) = bounded(1);
    if media_worker_tx().send(MediaReq::Query(reply_tx)).is_err() {
        return MediaSessionSnapshot::default();
    }
    reply_rx
        .recv_timeout(Duration::from_millis(1200))
        .unwrap_or_default()
}

/// Dispatches `play_pause` | `next` | `prev` to the best active SMTC session, falling back to
/// native Windows hardware media keys (`VK_MEDIA_*`) when no SMTC session is registered yet!
pub fn send_media_command(cmd: &str) {
    let _ = media_worker_tx().send(MediaReq::Command(cmd.to_string()));
}

/// Seeks playback position (in seconds relative to `StartTime`) on the active SMTC media session.
pub fn seek_media_position_secs(target_secs: u32) {
    let _ = media_worker_tx().send(MediaReq::Seek(target_secs));
}

pub fn compute_effective_timeline_secs(
    start_100ns: i64,
    end_100ns: i64,
    pos_100ns: i64,
    last_updated_filetime_100ns: i64,
    now_unix_ms: i64,
    is_playing: bool,
) -> (u32, u32) {
    let duration_secs = ((end_100ns - start_100ns).max(0) / 10_000_000) as u32;
    let raw_pos = ((pos_100ns - start_100ns).max(0) / 10_000_000) as u32;
    const WIN_EPOCH_DIFF_100NS: i64 = 116_444_736_000_000_000;
    let effective_pos = if is_playing
        && duration_secs > 0
        && last_updated_filetime_100ns > WIN_EPOCH_DIFF_100NS
    {
        let last_upd_ms = (last_updated_filetime_100ns - WIN_EPOCH_DIFF_100NS) / 10_000;
        let elapsed_secs = ((now_unix_ms - last_upd_ms).clamp(0, 86_400_000) / 1000) as u32;
        raw_pos.saturating_add(elapsed_secs).min(duration_secs)
    } else if duration_secs > 0 {
        raw_pos.min(duration_secs)
    } else {
        raw_pos
    };
    (effective_pos, duration_secs)
}

fn read_session_snapshot(
    session: &GlobalSystemMediaTransportControlsSession,
) -> (MediaSessionSnapshot, Option<IRandomAccessStreamReference>) {
    let playback_info = session.GetPlaybackInfo().ok();
    let status = playback_info.as_ref().and_then(|pi| pi.PlaybackStatus().ok());
    let is_playing = status == Some(GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing);
    let is_paused = status == Some(GlobalSystemMediaTransportControlsSessionPlaybackStatus::Paused);

    let app_name = session
        .SourceAppUserModelId()
        .map(|h| friendly_media_app(&h.to_string()))
        .unwrap_or_else(|_| "Medya".into());

    let (mut title, artist, thumb_ref) = if let Ok(prop_op) = session.TryGetMediaPropertiesAsync() {
        if let Ok(props) = prop_op.get() {
            let t = props.Title().map(|s| s.to_string()).unwrap_or_default().trim().to_string();
            let a = props.Artist().map(|s| s.to_string()).unwrap_or_default().trim().to_string();
            let r = props.Thumbnail().ok();
            (t, a, r)
        } else {
            (String::new(), String::new(), None)
        }
    } else {
        (String::new(), String::new(), None)
    };

    if title.is_empty() && (is_playing || is_paused) {
        title = app_name.clone();
    }

    let now_unix_ms = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0);
    let (position_secs, duration_secs) = session
        .GetTimelineProperties()
        .ok()
        .map(|tl| {
            let start = tl.StartTime().map(|t| t.Duration).unwrap_or(0);
            let end = tl.EndTime().map(|t| t.Duration).unwrap_or(0);
            let pos = tl.Position().map(|t| t.Duration).unwrap_or(0);
            let upd = tl.LastUpdatedTime().map(|t| t.UniversalTime).unwrap_or(0);
            compute_effective_timeline_secs(start, end, pos, upd, now_unix_ms, is_playing)
        })
        .unwrap_or((0, 0));
    let can_seek = duration_secs > 0
        && playback_info
            .as_ref()
            .and_then(|pi| pi.Controls().ok())
            .and_then(|c| c.IsPlaybackPositionEnabled().ok())
            .unwrap_or(true);

    let snap = MediaSessionSnapshot {
        is_playing,
        has_session: !title.is_empty() || is_playing || is_paused,
        title,
        artist,
        app_name,
        thumbnail_bytes: None,
        position_secs,
        duration_secs,
        timeline_updated_ms: now_unix_ms,
        can_seek,
    };
    (snap, thumb_ref)
}

fn pick_best_session(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
) -> Option<(
    GlobalSystemMediaTransportControlsSession,
    MediaSessionSnapshot,
    Option<IRandomAccessStreamReference>,
)> {
    let mut best: Option<(
        u8,
        GlobalSystemMediaTransportControlsSession,
        MediaSessionSnapshot,
        Option<IRandomAccessStreamReference>,
    )> = None;

    let mut consider = |sess: GlobalSystemMediaTransportControlsSession, is_current: bool| {
        let (snap, thumb_ref) = read_session_snapshot(&sess);
        if !snap.has_session {
            return;
        }
        let app_low = snap.app_name.to_ascii_lowercase();
        let is_music_app = app_low.contains("spotify")
            || app_low.contains("music")
            || app_low.contains("vlc")
            || app_low.contains("tidal")
            || app_low.contains("deezer")
            || app_low.contains("foobar");
        let mut score = 0u8;
        if snap.is_playing {
            score += 100;
        }
        if !snap.title.is_empty() && !snap.title.eq_ignore_ascii_case(&snap.app_name) {
            score += 20;
        }
        if is_music_app {
            score += 15;
        }
        if is_current {
            score += 10;
        }
        if thumb_ref.is_some() {
            score += 5;
        }
        if best.as_ref().is_none_or(|(bs, _, _, _)| score > *bs) {
            best = Some((score, sess, snap, thumb_ref));
        }
    };

    if let Ok(cur) = mgr.GetCurrentSession() {
        consider(cur, true);
    }
    if let Ok(all) = mgr.GetSessions() {
        let count = all.Size().unwrap_or(0);
        for i in 0..count {
            if let Ok(sess) = all.GetAt(i) {
                consider(sess, false);
            }
        }
    }

    best.map(|(_, s, snap, thumb_ref)| (s, snap, thumb_ref))
}

fn query_from_manager(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
    thumb_cache: &mut MediaThumbnailCache,
) -> MediaSessionSnapshot {
    let Some((_, mut snap, thumb_ref)) = pick_best_session(mgr) else {
        thumb_cache.clear();
        return MediaSessionSnapshot::default();
    };
    snap.thumbnail_bytes = thumb_cache.get_or_fetch(
        &snap.title,
        &snap.artist,
        &snap.app_name,
        || thumb_ref.as_ref().and_then(read_stream_ref_bytes),
    );
    snap
}

fn dispatch_smtc_command(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
    cmd: &str,
) -> bool {
    let target = pick_best_session(mgr)
        .map(|(s, _, _)| s)
        .or_else(|| mgr.GetCurrentSession().ok());
    let Some(session) = target else {
        return false;
    };
    match cmd {
        "play_pause" => session.TryTogglePlayPauseAsync().and_then(|o| o.get()).unwrap_or(false),
        "next" => session.TrySkipNextAsync().and_then(|o| o.get()).unwrap_or(false),
        "prev" => session.TrySkipPreviousAsync().and_then(|o| o.get()).unwrap_or(false),
        _ => false,
    }
}

fn dispatch_smtc_seek(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
    target_secs: u32,
) -> bool {
    let target = pick_best_session(mgr)
        .map(|(s, _, _)| s)
        .or_else(|| mgr.GetCurrentSession().ok());
    let Some(session) = target else {
        return false;
    };
    let start_100ns = session
        .GetTimelineProperties()
        .ok()
        .and_then(|tl| tl.StartTime().ok())
        .map(|ts| ts.Duration)
        .unwrap_or(0);
    let requested = start_100ns.saturating_add((target_secs as i64).saturating_mul(10_000_000));
    session
        .TryChangePlaybackPositionAsync(requested)
        .and_then(|o| o.get())
        .unwrap_or(false)
}

fn send_hardware_media_key(cmd: &str) {
    let vk: VIRTUAL_KEY = match cmd {
        "play_pause" => VK_MEDIA_PLAY_PAUSE,
        "next" => VK_MEDIA_NEXT_TRACK,
        "prev" => VK_MEDIA_PREV_TRACK,
        _ => return,
    };
    let down = INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: vk,
                dwFlags: KEYEVENTF_EXTENDEDKEY,
                ..Default::default()
            },
        },
    };
    let up = INPUT {
        r#type: INPUT_KEYBOARD,
        Anonymous: INPUT_0 {
            ki: KEYBDINPUT {
                wVk: vk,
                dwFlags: KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP,
                ..Default::default()
            },
        },
    };
    unsafe {
        let _ = SendInput(&[down, up], std::mem::size_of::<INPUT>() as i32);
    }
}

fn read_stream_ref_bytes(r: &IRandomAccessStreamReference) -> Option<Vec<u8>> {
    let stream = r.OpenReadAsync().ok()?.get().ok()?;
    let size = stream.Size().ok()?;
    if size == 0 || size > 4 * 1024 * 1024 {
        return None;
    }
    let reader = DataReader::CreateDataReader(&stream).ok()?;
    reader.LoadAsync(size as u32).ok()?.get().ok()?;
    let mut buf = vec![0u8; size as usize];
    reader.ReadBytes(&mut buf).ok()?;
    Some(buf)
}

fn friendly_media_app(aumid: &str) -> String {
    let low = aumid.to_ascii_lowercase();
    if low.contains("spotify") {
        "Spotify".into()
    } else if low.contains("opera") {
        "Opera".into()
    } else if low.contains("chrome") {
        "Chrome".into()
    } else if low.contains("msedge") || low.contains("edge") {
        "Edge".into()
    } else if low.contains("firefox") {
        "Firefox".into()
    } else if low.contains("vlc") {
        "VLC".into()
    } else if low.contains("applemusic") || low.contains("itunes") {
        "Apple Music".into()
    } else {
        aumid.split(['!', '.', '\\']).next().unwrap_or("Medya").to_string()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn timeline_interpolation_clamps_to_duration_cleanly() {
        const DIFF: i64 = 116_444_736_000_000_000;
        let now_ms = 1_760_000_000_000i64;
        let upd_filetime = DIFF + (now_ms - 5_000) * 10_000;
        let (pos, dur) = compute_effective_timeline_secs(
            0,
            141 * 10_000_000,
            10 * 10_000_000,
            upd_filetime,
            now_ms,
            true,
        );
        assert_eq!(dur, 141);
        assert_eq!(pos, 15);

        let (clamped_pos, clamped_dur) = compute_effective_timeline_secs(
            0,
            141 * 10_000_000,
            139 * 10_000_000,
            upd_filetime,
            now_ms,
            true,
        );
        assert_eq!(clamped_dur, 141);
        assert_eq!(clamped_pos, 141);
    }

    #[test]
    fn media_thumbnail_cache_hits_on_unchanged_track_and_misses_on_track_change() {
        let mut cache = MediaThumbnailCache::new();
        let mut fetch_calls = 0usize;

        let art_a = cache.get_or_fetch("Midnight City", "M83", "Spotify", || {
            fetch_calls += 1;
            Some(vec![0x89, 0x50, 0x4E, 0x47])
        });
        assert_eq!(fetch_calls, 1);
        assert_eq!(art_a.as_deref(), Some(&[0x89, 0x50, 0x4E, 0x47][..]));
        assert_eq!(cache.last_key(), Some("Midnight City|M83|Spotify"));

        // Repeated 1.5s polls with identical track key MUST NOT re-read WinRT stream
        for _ in 0..5 {
            let cached = cache.get_or_fetch("Midnight City", "M83", "Spotify", || {
                fetch_calls += 1;
                Some(vec![1, 2, 3])
            });
            assert_eq!(fetch_calls, 1);
            assert_eq!(cached.as_deref(), Some(&[0x89, 0x50, 0x4E, 0x47][..]));
        }

        // Changing title, artist, or app_name triggers a cache miss and fetches once
        let art_b = cache.get_or_fetch("Outro", "M83", "Spotify", || {
            fetch_calls += 1;
            Some(vec![0xFF, 0xD8, 0xFF])
        });
        assert_eq!(fetch_calls, 2);
        assert_eq!(art_b.as_deref(), Some(&[0xFF, 0xD8, 0xFF][..]));
        assert_eq!(cache.last_key(), Some("Outro|M83|Spotify"));

        // Repeated poll on new track hits cache
        let _ = cache.get_or_fetch("Outro", "M83", "Spotify", || {
            fetch_calls += 1;
            None
        });
        assert_eq!(fetch_calls, 2);

        // Switching app_name misses cache
        let art_c = cache.get_or_fetch("Outro", "M83", "Opera", || {
            fetch_calls += 1;
            None
        });
        assert_eq!(fetch_calls, 3);
        assert!(art_c.is_none());

        // Clear resets cache
        cache.clear();
        assert!(cache.last_key().is_none());
    }
}
