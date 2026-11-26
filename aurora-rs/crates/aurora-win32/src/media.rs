use std::sync::OnceLock;
use std::time::Duration;

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
                                    let res = query_from_manager(mgr);
                                    if !res.has_session {
                                        // Refresh manager handle in case Shell / Media service restarted
                                        if let Some(fresh) =
                                            GlobalSystemMediaTransportControlsSessionManager::RequestAsync()
                                                .ok()
                                                .and_then(|op| op.get().ok())
                                        {
                                            let r2 = query_from_manager(&fresh);
                                            cached_mgr = Some(fresh);
                                            r2
                                        } else {
                                            res
                                        }
                                    } else {
                                        res
                                    }
                                }
                                None => MediaSessionSnapshot::default(),
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

fn read_session_snapshot(
    session: &GlobalSystemMediaTransportControlsSession,
) -> MediaSessionSnapshot {
    let status = session
        .GetPlaybackInfo()
        .and_then(|pi| pi.PlaybackStatus())
        .ok();
    let is_playing = status == Some(GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing);
    let is_paused = status == Some(GlobalSystemMediaTransportControlsSessionPlaybackStatus::Paused);

    let app_name = session
        .SourceAppUserModelId()
        .map(|h| friendly_media_app(&h.to_string()))
        .unwrap_or_else(|_| "Medya".into());

    let (mut title, artist, thumb) = if let Ok(prop_op) = session.TryGetMediaPropertiesAsync() {
        if let Ok(props) = prop_op.get() {
            let t = props.Title().map(|s| s.to_string()).unwrap_or_default().trim().to_string();
            let a = props.Artist().map(|s| s.to_string()).unwrap_or_default().trim().to_string();
            let bytes = props
                .Thumbnail()
                .ok()
                .and_then(|r| read_stream_ref_bytes(&r));
            (t, a, bytes)
        } else {
            (String::new(), String::new(), None)
        }
    } else {
        (String::new(), String::new(), None)
    };

    if title.is_empty() && (is_playing || is_paused) {
        title = app_name.clone();
    }

    MediaSessionSnapshot {
        is_playing,
        has_session: !title.is_empty() || is_playing || is_paused,
        title,
        artist,
        app_name,
        thumbnail_bytes: thumb,
    }
}

fn pick_best_session(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
) -> Option<(GlobalSystemMediaTransportControlsSession, MediaSessionSnapshot)> {
    let mut best: Option<(u8, GlobalSystemMediaTransportControlsSession, MediaSessionSnapshot)> = None;

    let mut consider = |sess: GlobalSystemMediaTransportControlsSession, is_current: bool| {
        let snap = read_session_snapshot(&sess);
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
        if snap.thumbnail_bytes.is_some() {
            score += 5;
        }
        if best.as_ref().is_none_or(|(bs, _, _)| score > *bs) {
            best = Some((score, sess, snap));
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

    best.map(|(_, s, snap)| (s, snap))
}

fn query_from_manager(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
) -> MediaSessionSnapshot {
    pick_best_session(mgr)
        .map(|(_, snap)| snap)
        .unwrap_or_default()
}

fn dispatch_smtc_command(
    mgr: &GlobalSystemMediaTransportControlsSessionManager,
    cmd: &str,
) -> bool {
    let target = pick_best_session(mgr)
        .map(|(s, _)| s)
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
