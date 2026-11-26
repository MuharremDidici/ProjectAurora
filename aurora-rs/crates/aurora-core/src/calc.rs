use std::time::{SystemTime, UNIX_EPOCH};

/// Smart Tidy ("Derli") category classification for unpinned Dock apps.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum TidyCategory {
    Dev,
    Web,
    Comm,
    Media,
    App,
}

impl TidyCategory {
    pub fn id(self) -> &'static str {
        match self {
            Self::Dev => "dev",
            Self::Web => "web",
            Self::Comm => "comm",
            Self::Media => "media",
            Self::App => "app",
        }
    }

    pub fn label_tr(self) -> &'static str {
        match self {
            Self::Dev => "Geliştirme",
            Self::Web => "Web",
            Self::Comm => "İletişim",
            Self::Media => "Medya",
            Self::App => "Uygulama",
        }
    }
}

pub fn classify_tidy_category(process_or_name: &str) -> TidyCategory {
    let s = process_or_name.to_ascii_lowercase();
    if [
        "code", "antigravity", "cursor", "studio", "devenv", "rider", "idea", "pycharm",
        "terminal", "windowsterminal", "pwsh", "powershell", "cmd", "git", "docker", "postman",
        "insomnia", "dbeaver", "ssms", "wsl", "cargo", "rust",
    ]
    .iter()
    .any(|k| s.contains(k))
    {
        return TidyCategory::Dev;
    }
    if ["chrome", "msedge", "edge", "firefox", "opera", "brave", "vivaldi", "arc", "zen"]
        .iter()
        .any(|k| s.contains(k))
    {
        return TidyCategory::Web;
    }
    if ["whatsapp", "teams", "discord", "slack", "telegram", "zoom", "outlook", "thunderbird", "skype", "signal"]
        .iter()
        .any(|k| s.contains(k))
    {
        return TidyCategory::Comm;
    }
    if ["spotify", "vlc", "mpv", "obs", "audacity", "premiere", "photoshop", "figma", "blender", "steam", "epic"]
        .iter()
        .any(|k| s.contains(k))
    {
        return TidyCategory::Media;
    }
    TidyCategory::App
}

/// Classification of a parsed OmniSearch instant result.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OmniQueryKind {
    Math,
    Percentage,
    Unit,
    Currency,
    Timezone,
    WebShortcut,
    QuickCommand,
}

/// Structured OmniSearch parse output consumed by both `evaluate_omni_expression` and `ShellState`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OmniParsedResult {
    pub kind: OmniQueryKind,
    pub primary_value: String,
    pub display_title: String,
    pub subtitle: String,
    pub badge: &'static str,
    pub is_copy_value: bool,
}

/// Reference currency rates (relative to TRY and USD) used when live market rates are unavailable.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct CurrencyRates {
    pub usd_try: f64,
    pub eur_try: f64,
    pub gbp_try: f64,
    pub chf_try: f64,
    pub jpy_try: f64,
    pub cad_try: f64,
    pub aud_try: f64,
    pub btc_usd: f64,
    pub eth_usd: f64,
}

impl Default for CurrencyRates {
    fn default() -> Self {
        Self {
            usd_try: 35.0,
            eur_try: 38.0,
            gbp_try: 44.5,
            chf_try: 39.5,
            jpy_try: 0.23,
            cad_try: 25.0,
            aud_try: 22.8,
            btc_usd: 90_000.0,
            eth_usd: 3_200.0,
        }
    }
}

/// Safe OmniSearch parser supporting math, percentages, unit conversions, currency expressions,
/// world-clock timezone queries (`saat tokyo/london/newyork`), web shortcuts (`g/yt/gh <q>`),
/// and quick shell commands (`> lock`, `> sleep`, `> odak`, `> tema`, `> zamanlayici 15`).
pub fn parse_omni_query(input: &str) -> Option<OmniParsedResult> {
    let unix_secs = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs();
    parse_omni_query_with_context(input, &CurrencyRates::default(), unix_secs)
}

/// Deterministic variant of `parse_omni_query` accepting explicit currency rates and UTC epoch seconds.
pub fn parse_omni_query_with_context(
    input: &str,
    rates: &CurrencyRates,
    unix_secs: u64,
) -> Option<OmniParsedResult> {
    let trimmed = input.trim();
    if trimmed.len() < 2 {
        return None;
    }

    if let Some(cmd) = try_quick_command(trimmed) {
        return Some(cmd);
    }

    if let Some(web) = try_web_shortcut(trimmed) {
        return Some(web);
    }

    if let Some(tz) = try_timezone_query_at(trimmed, unix_secs) {
        return Some(tz);
    }

    let expr_trimmed = trimmed.strip_prefix('=').map(str::trim).unwrap_or(trimmed);
    if expr_trimmed.len() < 2 {
        return None;
    }

    if let Some(pct) = try_percentage_expression(expr_trimmed) {
        return Some(pct);
    }

    if let Some(unit) = try_unit_conversion_parsed(expr_trimmed) {
        return Some(unit);
    }

    if let Some(curr) = try_currency_conversion(expr_trimmed, rates) {
        return Some(curr);
    }

    evaluate_math_parsed(expr_trimmed)
}

/// Safe recursive-descent math, unit, currency, timezone, web-shortcut, and quick-command evaluator.
pub fn evaluate_omni_expression(input: &str) -> Option<(String, String)> {
    let parsed = parse_omni_query(input)?;
    Some((parsed.primary_value, parsed.subtitle))
}

/// Deterministic timezone query helper for tests and callers (`saat tokyo`, `saat london`, `saat newyork`).
pub fn evaluate_timezone_query_at(input: &str, unix_secs: u64) -> Option<(String, String)> {
    let parsed = try_timezone_query_at(input.trim(), unix_secs)?;
    Some((parsed.primary_value, parsed.subtitle))
}

fn evaluate_math_parsed(trimmed: &str) -> Option<OmniParsedResult> {
    if !trimmed.chars().any(|c| matches!(c, '+' | '-' | '*' | '/' | '^' | '%')) {
        return None;
    }
    if trimmed.chars().any(|c| c.is_alphabetic()) {
        return None;
    }

    let val = parse_math_expr(trimmed)?;
    if !val.is_finite() {
        return None;
    }
    let formatted = format_number(val, 4);
    Some(OmniParsedResult {
        kind: OmniQueryKind::Math,
        primary_value: formatted.clone(),
        display_title: formatted,
        subtitle: format!("Hesaplama: {trimmed}"),
        badge: "HESAP",
        is_copy_value: true,
    })
}

fn format_number(val: f64, decimals: usize) -> String {
    if !val.is_finite() {
        return "0".into();
    }
    let rounded = val.round();
    if (val - rounded).abs() < 1e-9 && rounded.abs() < 1e15 {
        return format!("{}", rounded as i64);
    }
    let s = format!("{val:.decimals$}");
    let trimmed = s.trim_end_matches('0').trim_end_matches('.');
    if trimmed == "-0" || trimmed.is_empty() {
        "0".into()
    } else {
        trimmed.to_string()
    }
}

fn try_quick_command(input: &str) -> Option<OmniParsedResult> {
    let rest = input.strip_prefix('>')?.trim();
    if rest.is_empty() {
        return None;
    }
    let mut parts = rest.split_whitespace();
    let cmd_raw = parts.next()?;
    let cmd = normalize_tr(cmd_raw);
    let arg_str: String = parts.collect::<Vec<_>>().join(" ");

    let (action, title, subtitle) = match cmd.as_str() {
        "lock" | "kilitle" | "kilit" => (
            "aurora:lock".to_string(),
            "Ekranı Kilitle".to_string(),
            "Hızlı Komut: Windows oturumunu kilitle".to_string(),
        ),
        "sleep" | "uyku" | "uyut" => (
            "aurora:sleep".to_string(),
            "Uyku Modu".to_string(),
            "Hızlı Komut: Bilgisayarı uyku moduna al".to_string(),
        ),
        "odak" | "focus" => (
            "aurora:focus".to_string(),
            "Odak Modu".to_string(),
            "Hızlı Komut: Odak modunu aç/kapat".to_string(),
        ),
        "tema" | "theme" => {
            if arg_str.is_empty() {
                (
                    "aurora:theme".to_string(),
                    "Tema Değiştir".to_string(),
                    "Hızlı Komut: Sonraki Aurora temasını uygula".to_string(),
                )
            } else {
                (
                    format!("aurora:theme:{arg_str}"),
                    format!("Tema Uygula: {arg_str}"),
                    format!("Hızlı Komut: '{arg_str}' temasını uygula"),
                )
            }
        }
        "zamanlayici" | "timer" => {
            let mins = arg_str
                .split_whitespace()
                .next()
                .and_then(|n| n.parse::<u32>().ok())
                .unwrap_or(25)
                .clamp(1, 240);
            (
                format!("aurora:timer:{mins}"),
                format!("{mins} Dakikalık Zamanlayıcı"),
                format!("Hızlı Komut: {mins} dk odak/geri sayım sayacı başlat"),
            )
        }
        _ => (
            format!("cmdrun:{rest}"),
            format!("Komut Çalıştır: {rest}"),
            "PowerShell üzerinden yürütülür".to_string(),
        ),
    };

    Some(OmniParsedResult {
        kind: OmniQueryKind::QuickCommand,
        primary_value: action,
        display_title: title,
        subtitle,
        badge: "KOMUT",
        is_copy_value: false,
    })
}

fn try_web_shortcut(input: &str) -> Option<OmniParsedResult> {
    let mut chars = input.char_indices();
    let split_idx = loop {
        match chars.next() {
            Some((idx, c)) if c.is_whitespace() => break Some(idx),
            Some(_) => {}
            None => break None,
        }
    }?;
    let prefix = input.get(..split_idx)?.to_ascii_lowercase();
    let query = input.get(split_idx..)?.trim();
    if query.is_empty() {
        return None;
    }

    let encoded = url_encode(query);
    let (url, title, subtitle) = match prefix.as_str() {
        "g" | "google" => (
            format!("https://www.google.com/search?q={encoded}"),
            format!("Google'da Ara: {query}"),
            format!("Google Arama: {query}"),
        ),
        "yt" | "youtube" => (
            format!("https://www.youtube.com/results?search_query={encoded}"),
            format!("YouTube'da Ara: {query}"),
            format!("YouTube Arama: {query}"),
        ),
        "gh" | "github" => (
            format!("https://github.com/search?q={encoded}"),
            format!("GitHub'da Ara: {query}"),
            format!("GitHub Arama: {query}"),
        ),
        _ => return None,
    };

    Some(OmniParsedResult {
        kind: OmniQueryKind::WebShortcut,
        primary_value: url,
        display_title: title,
        subtitle,
        badge: "WEB",
        is_copy_value: false,
    })
}

fn url_encode(input: &str) -> String {
    let mut out = String::with_capacity(input.len() * 2);
    for b in input.as_bytes() {
        match *b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                out.push(*b as char);
            }
            b' ' => out.push('+'),
            other => {
                use std::fmt::Write;
                let _ = write!(out, "%{other:02X}");
            }
        }
    }
    out
}

fn try_timezone_query_at(input: &str, unix_secs: u64) -> Option<OmniParsedResult> {
    let norm = normalize_tr(input);
    let tokens: Vec<&str> = norm.split_whitespace().collect();
    if tokens.len() < 2 {
        return None;
    }

    let city_raw = if matches!(tokens[0], "saat" | "time" | "tz") {
        tokens[1..].join("")
    } else if matches!(tokens[tokens.len() - 1], "saat" | "saati" | "time") {
        tokens[..tokens.len() - 1].join("")
    } else {
        return None;
    };

    let (city_name, tz_abbr, offset_mins) = lookup_city_timezone(&city_raw)?;
    let utc_mins = (unix_secs / 60) as i64;
    let local_mins = (utc_mins + offset_mins).rem_euclid(1440);
    let hh = local_mins / 60;
    let mm = local_mins % 60;
    let time_str = format!("{hh:02}:{mm:02}");
    let offset_lbl = format_utc_offset(offset_mins);

    Some(OmniParsedResult {
        kind: OmniQueryKind::Timezone,
        primary_value: time_str.clone(),
        display_title: format!("{city_name} • {time_str}"),
        subtitle: format!("{city_name} ({tz_abbr}, {offset_lbl})"),
        badge: "SAAT",
        is_copy_value: true,
    })
}

fn format_utc_offset(offset_mins: i64) -> String {
    if offset_mins == 0 {
        return "UTC+0".into();
    }
    let sign = if offset_mins >= 0 { '+' } else { '-' };
    let abs = offset_mins.abs();
    let h = abs / 60;
    let m = abs % 60;
    if m == 0 {
        format!("UTC{sign}{h}")
    } else {
        format!("UTC{sign}{h}:{m:02}")
    }
}

fn lookup_city_timezone(compact_city: &str) -> Option<(&'static str, &'static str, i64)> {
    match compact_city {
        "tokyo" => Some(("Tokyo", "JST", 9 * 60)),
        "london" | "londra" => Some(("Londra", "GMT", 0)),
        "newyork" | "nyc" | "ny" => Some(("New York", "EST", -5 * 60)),
        "istanbul" | "ankara" | "izmir" | "turkiye" | "turkey" => Some(("İstanbul", "TRT", 3 * 60)),
        "berlin" | "munih" | "munich" | "frankfurt" => Some(("Berlin", "CET", 60)),
        "paris" => Some(("Paris", "CET", 60)),
        "amsterdam" => Some(("Amsterdam", "CET", 60)),
        "roma" | "rome" | "milano" | "milan" => Some(("Roma", "CET", 60)),
        "madrid" | "barcelona" => Some(("Madrid", "CET", 60)),
        "zurich" | "zurih" | "geneva" | "cenevre" => Some(("Zürih", "CET", 60)),
        "dubai" | "abudhabi" => Some(("Dubai", "GST", 4 * 60)),
        "seoul" | "seul" => Some(("Seul", "KST", 9 * 60)),
        "singapore" | "singapur" => Some(("Singapur", "SGT", 8 * 60)),
        "beijing" | "pekin" | "shanghai" | "sanghay" => Some(("Pekin", "CST", 8 * 60)),
        "hongkong" | "hk" => Some(("Hong Kong", "HKT", 8 * 60)),
        "sydney" | "sidney" | "melbourne" => Some(("Sidney", "AEST", 10 * 60)),
        "losangeles" | "la" | "sanfrancisco" | "sf" | "seattle" => Some(("Los Angeles", "PST", -8 * 60)),
        "chicago" => Some(("Chicago", "CST", -6 * 60)),
        "toronto" | "montreal" => Some(("Toronto", "EST", -5 * 60)),
        "moscow" | "moskova" => Some(("Moskova", "MSK", 3 * 60)),
        "mumbai" | "delhi" | "newdelhi" | "bangalore" => Some(("Mumbai", "IST", 5 * 60 + 30)),
        "cairo" | "kahire" => Some(("Kahire", "EET", 2 * 60)),
        "saopaulo" | "rio" => Some(("São Paulo", "BRT", -3 * 60)),
        "utc" | "gmt" => Some(("UTC", "UTC", 0)),
        _ => None,
    }
}

fn try_percentage_expression(input: &str) -> Option<OmniParsedResult> {
    let lower = normalize_tr(input);
    let tokens: Vec<&str> = lower.split_whitespace().collect();

    // Form 1: "<pct>% of <base>" | "<pct>% * <base>" | "<pct>% x <base>" | "<pct>% off <base>"
    // Also "<pct> % of <base>"
    let parsed = if tokens.len() == 3 && tokens[0].ends_with('%') {
        let pct_str = tokens[0].trim_end_matches('%');
        let pct = parse_f64_clean(pct_str)?;
        let base = parse_f64_clean(tokens[2])?;
        match tokens[1] {
            "of" | "*" | "x" => Some((pct / 100.0 * base, format!("{pct}% × {base}"))),
            "off" | "indirim" => Some((base * (1.0 - pct / 100.0), format!("{base} - %{pct}"))),
            "on" | "artis" => Some((base * (1.0 + pct / 100.0), format!("{base} + %{pct}"))),
            _ => None,
        }
    } else if tokens.len() == 4 && tokens[1] == "%" {
        let pct = parse_f64_clean(tokens[0])?;
        let base = parse_f64_clean(tokens[3])?;
        match tokens[2] {
            "of" | "*" | "x" => Some((pct / 100.0 * base, format!("{pct}% × {base}"))),
            "off" | "indirim" => Some((base * (1.0 - pct / 100.0), format!("{base} - %{pct}"))),
            "on" | "artis" => Some((base * (1.0 + pct / 100.0), format!("{base} + %{pct}"))),
            _ => None,
        }
    } else if tokens.len() == 3 && tokens[0].starts_with('%') && matches!(tokens[1], "of" | "*" | "x") {
        // Form 2: "%18 of 4500"
        let pct = parse_f64_clean(tokens[0].trim_start_matches('%'))?;
        let base = parse_f64_clean(tokens[2])?;
        Some((pct / 100.0 * base, format!("%{pct} × {base}")))
    } else if tokens.len() == 2 && tokens[0].starts_with('%') {
        // Form 3: "%18 4500"
        let pct = parse_f64_clean(tokens[0].trim_start_matches('%'))?;
        let base = parse_f64_clean(tokens[1])?;
        Some((pct / 100.0 * base, format!("%{pct} × {base}")))
    } else if tokens.len() == 2 && tokens[1].starts_with('%') {
        // Form 4: "4500 %18"
        let base = parse_f64_clean(tokens[0])?;
        let pct = parse_f64_clean(tokens[1].trim_start_matches('%'))?;
        Some((pct / 100.0 * base, format!("%{pct} × {base}")))
    } else {
        None
    }?;

    if !parsed.0.is_finite() {
        return None;
    }
    let formatted = format_number(parsed.0, 4);
    Some(OmniParsedResult {
        kind: OmniQueryKind::Percentage,
        primary_value: formatted.clone(),
        display_title: formatted,
        subtitle: format!("Yüzde Hesap: {}", parsed.1),
        badge: "YÜZDE",
        is_copy_value: true,
    })
}

fn parse_f64_clean(s: &str) -> Option<f64> {
    let cleaned = s.trim().replace(',', ".");
    let v: f64 = cleaned.parse().ok()?;
    if v.is_finite() { Some(v) } else { None }
}

fn split_leading_number_and_unit(token: &str) -> Option<(f64, &str)> {
    let mut split_at = None;
    let mut seen_digit = false;
    for (idx, ch) in token.char_indices() {
        if ch.is_ascii_digit() {
            seen_digit = true;
        } else if (ch == '.' || ch == ',') || (idx == 0 && (ch == '-' || ch == '+')) {
            // valid part of leading number
        } else {
            split_at = Some(idx);
            break;
        }
    }
    if !seen_digit {
        return None;
    }
    let idx = split_at?;
    let num_part = token.get(..idx)?;
    let unit_part = token.get(idx..)?.trim();
    if unit_part.is_empty() {
        return None;
    }
    let val = parse_f64_clean(num_part)?;
    Some((val, unit_part))
}

fn parse_conversion_parts(input: &str) -> Option<(f64, String, String)> {
    let parts: Vec<&str> = input.split_whitespace().collect();
    match parts.as_slice() {
        [num_str, from, op, to] if matches!(op.to_ascii_lowercase().as_str(), "to" | "in" | "->" | "=>"| "=") => {
            let v = parse_f64_clean(num_str)?;
            Some((v, (*from).to_string(), (*to).to_string()))
        }
        [num_unit, op, to] if matches!(op.to_ascii_lowercase().as_str(), "to" | "in" | "->" | "=>" | "=") => {
            let (v, from) = split_leading_number_and_unit(num_unit)?;
            Some((v, from.to_string(), (*to).to_string()))
        }
        _ => None,
    }
}

fn try_unit_conversion_parsed(input: &str) -> Option<OmniParsedResult> {
    let (v, from_raw, to_raw) = parse_conversion_parts(input)?;
    let from = normalize_tr(&from_raw.replace('°', ""));
    let to = normalize_tr(&to_raw.replace('°', ""));

    let (res, unit_lbl) = convert_unit_value(v, &from, &to)?;
    if !res.is_finite() {
        return None;
    }
    let out = format_number(res, 2);
    let full = format!("{out} {unit_lbl}");
    Some(OmniParsedResult {
        kind: OmniQueryKind::Unit,
        primary_value: full.clone(),
        display_title: full,
        subtitle: format!("Birim Çeviri: {input}"),
        badge: "BİRİM",
        is_copy_value: true,
    })
}

fn convert_unit_value(v: f64, from: &str, to: &str) -> Option<(f64, &'static str)> {
    // Temperature
    let temp = match (from, to) {
        ("c" | "celsius" | "santigrat", "f" | "fahrenheit") => Some((v * 9.0 / 5.0 + 32.0, "°F")),
        ("f" | "fahrenheit", "c" | "celsius" | "santigrat") => Some(((v - 32.0) * 5.0 / 9.0, "°C")),
        ("c" | "celsius" | "santigrat", "k" | "kelvin") => Some((v + 273.15, "K")),
        ("k" | "kelvin", "c" | "celsius" | "santigrat") => Some((v - 273.15, "°C")),
        ("f" | "fahrenheit", "k" | "kelvin") => Some(((v - 32.0) * 5.0 / 9.0 + 273.15, "K")),
        ("k" | "kelvin", "f" | "fahrenheit") => Some(((v - 273.15) * 9.0 / 5.0 + 32.0, "°F")),
        _ => None,
    };
    if temp.is_some() {
        return temp;
    }

    // Data storage (preserving 1024 binary factors)
    let data_factor = |u: &str| -> Option<(f64, &'static str)> {
        match u {
            "b" | "byte" | "bytes" => Some((1.0, "B")),
            "kb" => Some((1024.0, "KB")),
            "mb" => Some((1024.0 * 1024.0, "MB")),
            "gb" => Some((1024.0 * 1024.0 * 1024.0, "GB")),
            "tb" => Some((1024.0 * 1024.0 * 1024.0 * 1024.0, "TB")),
            _ => None,
        }
    };
    if let (Some((f_in, _)), Some((f_out, lbl))) = (data_factor(from), data_factor(to)) {
        return Some((v * f_in / f_out, lbl));
    }

    // Length
    let length_factor = |u: &str| -> Option<(f64, &'static str)> {
        match u {
            "mm" => Some((0.001, "mm")),
            "cm" => Some((0.01, "cm")),
            "m" | "metre" | "meter" => Some((1.0, "m")),
            "km" => Some((1000.0, "km")),
            "in" | "inch" | "inc" => Some((0.0254, "inç")),
            "ft" | "feet" | "foot" => Some((0.3048, "ft")),
            "yd" | "yard" => Some((0.9144, "yd")),
            "mi" | "mil" | "mile" | "miles" => Some((1609.344, "mil")),
            _ => None,
        }
    };
    if let (Some((f_in, _)), Some((f_out, lbl))) = (length_factor(from), length_factor(to)) {
        return Some((v * f_in / f_out, lbl));
    }

    // Weight / Mass
    let weight_factor = |u: &str| -> Option<(f64, &'static str)> {
        match u {
            "mg" => Some((0.001, "mg")),
            "g" | "gr" | "gram" => Some((1.0, "g")),
            "kg" => Some((1000.0, "kg")),
            "ton" | "t" => Some((1_000_000.0, "ton")),
            "oz" | "ons" => Some((28.349523125, "oz")),
            "lb" | "lbs" => Some((453.59237, "lb")),
            _ => None,
        }
    };
    if let (Some((f_in, _)), Some((f_out, lbl))) = (weight_factor(from), weight_factor(to)) {
        return Some((v * f_in / f_out, lbl));
    }

    // Speed
    let speed_factor = |u: &str| -> Option<(f64, &'static str)> {
        match u {
            "kmh" | "km/h" | "kph" => Some((1.0, "km/h")),
            "mph" => Some((1.609344, "mph")),
            "ms" | "m/s" => Some((3.6, "m/s")),
            "knot" | "kn" | "kt" => Some((1.852, "knot")),
            _ => None,
        }
    };
    if let (Some((f_in, _)), Some((f_out, lbl))) = (speed_factor(from), speed_factor(to)) {
        return Some((v * f_in / f_out, lbl));
    }

    // Time duration
    let time_factor = |u: &str| -> Option<(f64, &'static str)> {
        match u {
            "sn" | "s" | "sec" | "saniye" => Some((1.0, "sn")),
            "dk" | "min" | "dakika" => Some((60.0, "dk")),
            "saat" | "h" | "hr" | "hour" | "hours" => Some((3600.0, "saat")),
            "gun" | "d" | "day" | "days" => Some((86_400.0, "gün")),
            _ => None,
        }
    };
    if let (Some((f_in, _)), Some((f_out, lbl))) = (time_factor(from), time_factor(to)) {
        return Some((v * f_in / f_out, lbl));
    }

    // Volume
    let vol_factor = |u: &str| -> Option<(f64, &'static str)> {
        match u {
            "ml" => Some((0.001, "ml")),
            "l" | "lt" | "litre" => Some((1.0, "L")),
            "gal" | "gallon" => Some((3.785411784, "gal")),
            _ => None,
        }
    };
    if let (Some((f_in, _)), Some((f_out, lbl))) = (vol_factor(from), vol_factor(to)) {
        return Some((v * f_in / f_out, lbl));
    }

    None
}

fn try_currency_conversion(input: &str, rates: &CurrencyRates) -> Option<OmniParsedResult> {
    let (amount, from_raw, to_raw) = parse_currency_parts(input)?;
    let (from_code, from_try) = currency_in_try(&from_raw, rates)?;
    let (to_code, to_try) = currency_in_try(&to_raw, rates)?;
    if to_try <= 0.0 || !from_try.is_finite() || !to_try.is_finite() {
        return None;
    }
    let converted = amount * from_try / to_try;
    if !converted.is_finite() {
        return None;
    }

    let decimals = if matches!(to_code, "BTC" | "ETH") { 6 } else { 2 };
    let val_str = if matches!(to_code, "BTC" | "ETH") {
        format_number(converted, decimals)
    } else {
        format!("{converted:.2}")
    };
    let out = format!("{val_str} {to_code}");
    let amount_str = format_number(amount, 2);

    Some(OmniParsedResult {
        kind: OmniQueryKind::Currency,
        primary_value: out.clone(),
        display_title: out,
        subtitle: format!("Döviz Çeviri: {amount_str} {from_code} → {to_code} (referans kur)"),
        badge: "DÖVİZ",
        is_copy_value: true,
    })
}

fn parse_currency_parts(input: &str) -> Option<(f64, String, String)> {
    // Explicit "A C1 to C2" or "AC1 to C2"
    if let Some((v, from, to)) = parse_conversion_parts(input) {
        let f_norm = normalize_currency_token(&from)?;
        let t_norm = normalize_currency_token(&to)?;
        return Some((v, f_norm.to_string(), t_norm.to_string()));
    }

    // Symbol-prefixed explicit conversion: "$100 to try", "€50 in usd", "₺1000 to eur"
    let parts: Vec<&str> = input.split_whitespace().collect();
    if parts.len() == 3 && matches!(parts[1].to_ascii_lowercase().as_str(), "to" | "in" | "->" | "=>" | "=") {
        if let Some((v, sym)) = split_symbol_prefixed_currency(parts[0]) {
            let t_norm = normalize_currency_token(parts[2])?;
            return Some((v, sym.to_string(), t_norm.to_string()));
        }
    }

    // Shorthand single currency: "120 usd", "120usd", "$120", "€100", "1000 try"
    if parts.len() == 2 {
        if let (Some(v), Some(f_norm)) = (parse_f64_clean(parts[0]), normalize_currency_token(parts[1])) {
            let target = if f_norm == "TRY" { "USD" } else { "TRY" };
            return Some((v, f_norm.to_string(), target.to_string()));
        }
    } else if parts.len() == 1 {
        if let Some((v, sym)) = split_symbol_prefixed_currency(parts[0]) {
            let target = if sym == "TRY" { "USD" } else { "TRY" };
            return Some((v, sym.to_string(), target.to_string()));
        }
        if let Some((v, unit)) = split_leading_number_and_unit(parts[0]) {
            if let Some(f_norm) = normalize_currency_token(unit) {
                let target = if f_norm == "TRY" { "USD" } else { "TRY" };
                return Some((v, f_norm.to_string(), target.to_string()));
            }
        }
    }

    None
}

fn split_symbol_prefixed_currency(tok: &str) -> Option<(f64, &'static str)> {
    let mut chars = tok.chars();
    let first = chars.next()?;
    let code = match first {
        '$' => "USD",
        '€' => "EUR",
        '£' => "GBP",
        '₺' => "TRY",
        '¥' => "JPY",
        _ => return None,
    };
    let rest = tok.get(first.len_utf8()..)?;
    let v = parse_f64_clean(rest)?;
    Some((v, code))
}

fn normalize_currency_token(raw: &str) -> Option<&'static str> {
    let norm = normalize_tr(raw);
    match norm.as_str() {
        "usd" | "$" | "dolar" | "dollar" => Some("USD"),
        "eur" | "€" | "euro" | "avro" => Some("EUR"),
        "try" | "tl" | "₺" => Some("TRY"),
        "gbp" | "£" | "sterlin" | "pound" => Some("GBP"),
        "chf" | "frank" => Some("CHF"),
        "jpy" | "¥" | "yen" => Some("JPY"),
        "cad" => Some("CAD"),
        "aud" => Some("AUD"),
        "btc" | "bitcoin" => Some("BTC"),
        "eth" | "ethereum" => Some("ETH"),
        _ => None,
    }
}

fn currency_in_try(code: &str, rates: &CurrencyRates) -> Option<(&'static str, f64)> {
    match code {
        "TRY" => Some(("TRY", 1.0)),
        "USD" => Some(("USD", rates.usd_try)),
        "EUR" => Some(("EUR", rates.eur_try)),
        "GBP" => Some(("GBP", rates.gbp_try)),
        "CHF" => Some(("CHF", rates.chf_try)),
        "JPY" => Some(("JPY", rates.jpy_try)),
        "CAD" => Some(("CAD", rates.cad_try)),
        "AUD" => Some(("AUD", rates.aud_try)),
        "BTC" => Some(("BTC", rates.btc_usd * rates.usd_try)),
        "ETH" => Some(("ETH", rates.eth_usd * rates.usd_try)),
        _ => None,
    }
}

const MAX_EXPR_DEPTH: usize = 64;

fn parse_math_expr(expr: &str) -> Option<f64> {
    let chars: Vec<char> = expr.chars().filter(|c| !c.is_whitespace()).collect();
    if chars.is_empty() {
        return None;
    }
    let mut pos = 0usize;
    let val = parse_add_sub(&chars, &mut pos, 0)?;
    if pos == chars.len() { Some(val) } else { None }
}

fn parse_add_sub(s: &[char], p: &mut usize, depth: usize) -> Option<f64> {
    if depth > MAX_EXPR_DEPTH {
        return None;
    }
    let mut lhs = parse_mul_div(s, p, depth + 1)?;
    while let Some(&op) = s.get(*p) {
        match op {
            '+' => {
                *p += 1;
                lhs += parse_mul_div(s, p, depth + 1)?;
            }
            '-' => {
                *p += 1;
                lhs -= parse_mul_div(s, p, depth + 1)?;
            }
            _ => break,
        }
    }
    Some(lhs)
}

fn parse_mul_div(s: &[char], p: &mut usize, depth: usize) -> Option<f64> {
    if depth > MAX_EXPR_DEPTH {
        return None;
    }
    let mut lhs = parse_pow(s, p, depth + 1)?;
    while let Some(&op) = s.get(*p) {
        match op {
            '*' => {
                *p += 1;
                lhs *= parse_pow(s, p, depth + 1)?;
            }
            '/' => {
                *p += 1;
                let rhs = parse_pow(s, p, depth + 1)?;
                if rhs.abs() < 1e-15 {
                    return None;
                }
                lhs /= rhs;
            }
            '%' => {
                *p += 1;
                if let Some(&next) = s.get(*p) {
                    if next.is_ascii_digit() || next == '(' {
                        let rhs = parse_pow(s, p, depth + 1)?;
                        if rhs.abs() < 1e-15 {
                            return None;
                        }
                        lhs %= rhs;
                        continue;
                    }
                }
                lhs /= 100.0;
            }
            _ => break,
        }
    }
    Some(lhs)
}

fn parse_pow(s: &[char], p: &mut usize, depth: usize) -> Option<f64> {
    if depth > MAX_EXPR_DEPTH {
        return None;
    }
    let base = parse_unary(s, p, depth + 1)?;
    if s.get(*p) == Some(&'^') {
        *p += 1;
        let exp = parse_pow(s, p, depth + 1)?;
        Some(base.powf(exp))
    } else {
        Some(base)
    }
}

fn parse_unary(s: &[char], p: &mut usize, depth: usize) -> Option<f64> {
    if depth > MAX_EXPR_DEPTH {
        return None;
    }
    if s.get(*p) == Some(&'-') {
        *p += 1;
        return Some(-parse_unary(s, p, depth + 1)?);
    }
    if s.get(*p) == Some(&'+') {
        *p += 1;
        return parse_unary(s, p, depth + 1);
    }
    parse_primary(s, p, depth + 1)
}

fn parse_primary(s: &[char], p: &mut usize, depth: usize) -> Option<f64> {
    if depth > MAX_EXPR_DEPTH {
        return None;
    }
    let first = *s.get(*p)?;
    if first == '(' {
        *p += 1;
        let v = parse_add_sub(s, p, depth + 1)?;
        if s.get(*p) == Some(&')') {
            *p += 1;
            return Some(v);
        }
        return None;
    }
    let start = *p;
    while let Some(&c) = s.get(*p) {
        if c.is_ascii_digit() || c == '.' || c == ',' {
            *p += 1;
        } else {
            break;
        }
    }
    if *p == start {
        return None;
    }
    let slice = s.get(start..*p)?;
    let num_str: String = slice.iter().map(|&c| if c == ',' { '.' } else { c }).collect();
    num_str.parse::<f64>().ok()
}

/// Turkish-aware case-insensitive fuzzy scoring for OmniSearch apps & files.
pub fn fuzzy_match_score(query: &str, haystack: &str, frecency_hits: u32) -> Option<i32> {
    let q = normalize_tr(query);
    if q.is_empty() {
        return Some(frecency_hits.min(500) as i32);
    }
    let h = normalize_tr(haystack);
    let mut score = if h == q {
        1000
    } else if h.starts_with(&q) {
        800
    } else if let Some(idx) = h.find(&q) {
        600 - (idx.min(120) as i32)
    } else {
        // Subsequence match
        let mut q_chars = q.chars().peekable();
        let mut matched = 0i32;
        for hc in h.chars() {
            if let Some(&qc) = q_chars.peek() {
                if hc == qc {
                    q_chars.next();
                    matched += 1;
                }
            }
        }
        if q_chars.peek().is_some() {
            return None;
        }
        250 + matched * 6
    };
    score += (frecency_hits.min(100) as i32) * 3;
    Some(score)
}

pub fn normalize_tr(s: &str) -> String {
    s.chars()
        .map(|c| match c {
            'İ' | 'I' | 'ı' => 'i',
            'Ğ' | 'ğ' => 'g',
            'Ü' | 'ü' => 'u',
            'Ş' | 'ş' => 's',
            'Ö' | 'ö' => 'o',
            'Ç' | 'ç' => 'c',
            other => other.to_ascii_lowercase(),
        })
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn calculator_and_unit_converter_work_accurately() {
        assert_eq!(evaluate_omni_expression("(125 * 4) + 12").unwrap().0, "512");
        assert_eq!(evaluate_omni_expression("= 24 * 15").unwrap().0, "360");
        assert_eq!(evaluate_omni_expression("2 ^ 10").unwrap().0, "1024");
        assert_eq!(evaluate_omni_expression("100 c to f").unwrap().0, "212 °F");
        assert_eq!(evaluate_omni_expression("100 cm to in").unwrap().0, "39.37 inç");
        assert_eq!(evaluate_omni_expression("180cm to in").unwrap().0, "70.87 inç");
        assert_eq!(evaluate_omni_expression("2 gb to mb").unwrap().0, "2048 MB");
        assert_eq!(evaluate_omni_expression("10 km -> mi").unwrap().0, "6.21 mil");
        assert!(evaluate_omni_expression("whatsapp").is_none());
    }

    #[test]
    fn percentage_expressions_evaluate_cleanly() {
        assert_eq!(evaluate_omni_expression("18% of 4500").unwrap().0, "810");
        assert_eq!(evaluate_omni_expression("18 % of 4500").unwrap().0, "810");
        assert_eq!(evaluate_omni_expression("%25 200").unwrap().0, "50");
        assert_eq!(evaluate_omni_expression("20% off 500").unwrap().0, "400");
        assert_eq!(evaluate_omni_expression("200 * 15%").unwrap().0, "30");
    }

    #[test]
    fn currency_expressions_convert_with_default_and_custom_rates() {
        let rates = CurrencyRates {
            usd_try: 35.0,
            eur_try: 38.5,
            gbp_try: 44.0,
            ..Default::default()
        };
        let res = parse_omni_query_with_context("100 usd to try", &rates, 0).unwrap();
        assert_eq!(res.kind, OmniQueryKind::Currency);
        assert_eq!(res.primary_value, "3500.00 TRY");

        let eur = parse_omni_query_with_context("100 eur -> usd", &rates, 0).unwrap();
        assert_eq!(eur.primary_value, "110.00 USD");

        let sym = parse_omni_query_with_context("$20 to try", &rates, 0).unwrap();
        assert_eq!(sym.primary_value, "700.00 TRY");

        let short = parse_omni_query_with_context("120 usd", &rates, 0).unwrap();
        assert_eq!(short.primary_value, "4200.00 TRY");
    }

    #[test]
    fn timezone_queries_resolve_world_cities_accurately() {
        // 2026-01-01 12:00:00 UTC
        let noon_utc = 1_767_268_800u64;
        assert_eq!(evaluate_timezone_query_at("saat tokyo", noon_utc).unwrap().0, "21:00");
        assert_eq!(evaluate_timezone_query_at("saat london", noon_utc).unwrap().0, "12:00");
        assert_eq!(evaluate_timezone_query_at("saat newyork", noon_utc).unwrap().0, "07:00");
        assert_eq!(evaluate_timezone_query_at("saat new york", noon_utc).unwrap().0, "07:00");
        assert_eq!(evaluate_timezone_query_at("saat istanbul", noon_utc).unwrap().0, "15:00");
        assert!(evaluate_omni_expression("saat tokyo").is_some());
        assert!(evaluate_omni_expression("saat london").is_some());
        assert!(evaluate_omni_expression("saat newyork").is_some());
        assert!(evaluate_timezone_query_at("saat unknowncity", noon_utc).is_none());
    }

    #[test]
    fn web_shortcuts_encode_queries_safely() {
        let g = parse_omni_query("g rust async").unwrap();
        assert_eq!(g.kind, OmniQueryKind::WebShortcut);
        assert_eq!(g.primary_value, "https://www.google.com/search?q=rust+async");
        assert!(!g.is_copy_value);

        let yt = parse_omni_query("yt lofi beats").unwrap();
        assert_eq!(yt.primary_value, "https://www.youtube.com/results?search_query=lofi+beats");

        let gh = parse_omni_query("gh tokio-rs/tokio").unwrap();
        assert_eq!(gh.primary_value, "https://github.com/search?q=tokio-rs%2Ftokio");

        // Plain prefix without query must not hijack app search
        assert!(parse_omni_query("g ").is_none());
        assert!(parse_omni_query("google").is_none());
    }

    #[test]
    fn quick_commands_parse_cleanly_without_panicking() {
        assert_eq!(evaluate_omni_expression("> lock").unwrap().0, "aurora:lock");
        assert_eq!(evaluate_omni_expression("> sleep").unwrap().0, "aurora:sleep");
        assert_eq!(evaluate_omni_expression("> odak").unwrap().0, "aurora:focus");
        assert_eq!(evaluate_omni_expression("> tema").unwrap().0, "aurora:theme");
        assert_eq!(evaluate_omni_expression("> tema oled-carbon").unwrap().0, "aurora:theme:oled-carbon");
        assert_eq!(evaluate_omni_expression("> zamanlayici 15").unwrap().0, "aurora:timer:15");
        assert_eq!(evaluate_omni_expression("> zamanlayıcı 999").unwrap().0, "aurora:timer:240");
        assert!(evaluate_omni_expression(">   ").is_none());
    }

    #[test]
    fn malformed_and_extreme_inputs_never_panic() {
        let deep_parens = "(".repeat(200) + "1" + &")".repeat(200);
        let inputs = [
            "",
            " ",
            "=",
            ">",
            "1/0",
            "10 % 0",
            "(((1+2",
            "999999999999999999999999 ^ 99",
            "ğüşıöç 123 to abc",
            "saat ",
            "g ",
            &deep_parens,
        ];
        for inp in inputs {
            let _ = evaluate_omni_expression(inp);
            let _ = parse_omni_query(inp);
        }
    }

    #[test]
    fn turkish_fuzzy_search_matches_diacritics() {
        assert!(fuzzy_match_score("ayarlar", "Ayarlar", 0).unwrap() >= 800);
        assert!(fuzzy_match_score("guc", "Güç ve Pil", 5).unwrap() > 600);
        assert!(fuzzy_match_score("ekran alintisi", "Ekran Alıntısı", 2).unwrap() > 800);
    }
}
