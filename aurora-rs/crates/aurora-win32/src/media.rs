use aurora_core::MediaSessionSnapshot;
use windows::Media::Control::{
    GlobalSystemMediaTransportControlsSessionManager,
    GlobalSystemMediaTransportControlsSessionPlaybackStatus,
};
use windows::Storage::Streams::{DataReader, IRandomAccessStreamReference};

/// Native WinRT GSMTC Media Session controller (Spotify, YouTube, Edge, Opera, VLC).
pub fn query_media_session() -> MediaSessionSnapshot {
    let Ok(op) = GlobalSystemMediaTransportControlsSessionManager::RequestAsync() else {
        return MediaSessionSnapshot::default();
    };
    let Ok(mgr) = op.get() else {
        return MediaSessionSnapshot::default();
    };
    let Ok(session) = mgr.GetCurrentSession() else {
        return MediaSessionSnapshot::default();
    };

    let is_playing = session
        .GetPlaybackInfo()
        .and_then(|pi| pi.PlaybackStatus())
        .map(|s| s == GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing)
        .unwrap_or(false);

    let app_name = session
        .SourceAppUserModelId()
        .map(|h| friendly_media_app(&h.to_string()))
        .unwrap_or_else(|_| "Medya".into());

    let (title, artist, thumb) = if let Ok(prop_op) = session.TryGetMediaPropertiesAsync() {
        if let Ok(props) = prop_op.get() {
            let t = props.Title().map(|s| s.to_string()).unwrap_or_default();
            let a = props.Artist().map(|s| s.to_string()).unwrap_or_default();
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

    MediaSessionSnapshot {
        is_playing,
        has_session: !title.is_empty() || is_playing,
        title,
        artist,
        app_name,
        thumbnail_bytes: thumb,
    }
}

pub fn send_media_command(cmd: &str) {
    let Ok(op) = GlobalSystemMediaTransportControlsSessionManager::RequestAsync() else { return };
    let Ok(mgr) = op.get() else { return };
    let Ok(session) = mgr.GetCurrentSession() else { return };
    match cmd {
        "play_pause" => { let _ = session.TryTogglePlayPauseAsync().and_then(|o| o.get()); }
        "next" => { let _ = session.TrySkipNextAsync().and_then(|o| o.get()); }
        "prev" => { let _ = session.TrySkipPreviousAsync().and_then(|o| o.get()); }
        _ => {}
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
    if low.contains("spotify") { "Spotify".into() }
    else if low.contains("opera") { "Opera".into() }
    else if low.contains("chrome") { "Chrome".into() }
    else if low.contains("msedge") || low.contains("edge") { "Edge".into() }
    else if low.contains("firefox") { "Firefox".into() }
    else if low.contains("vlc") { "VLC".into() }
    else {
        aumid.split(['!', '.', '\\']).next().unwrap_or("Medya").to_string()
    }
}
