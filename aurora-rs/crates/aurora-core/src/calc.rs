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

/// Safe recursive-descent math & unit expression evaluator for OmniSearch (`125*4+12%`, `180 cm to in`, `72 f to c`).
pub fn evaluate_omni_expression(input: &str) -> Option<(String, String)> {
    let trimmed = input.trim();
    if trimmed.len() < 2 {
        return None;
    }

    // Unit conversion check: "<num> <from> to/in <to>"
    if let Some(conv) = try_unit_conversion(trimmed) {
        return Some(conv);
    }

    // Must contain at least one arithmetic operator to avoid matching plain app names/numbers
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
    let formatted = if (val - val.round()).abs() < 1e-9 && val.abs() < 1e15 {
        format!("{}", val.round() as i64)
    } else {
        format!("{val:.4}")
            .trim_end_matches('0')
            .trim_end_matches('.')
            .to_string()
    };
    Some((formatted, format!("Hesaplama: {trimmed}")))
}

fn try_unit_conversion(input: &str) -> Option<(String, String)> {
    let parts: Vec<&str> = input.split_whitespace().collect();
    if parts.len() != 4 {
        return None;
    }
    if !matches!(parts[2].to_ascii_lowercase().as_str(), "to" | "in" | "->" | "=") {
        return None;
    }
    let v: f64 = parts[0].replace(',', ".").parse().ok()?;
    let from = parts[1].to_ascii_lowercase();
    let to = parts[3].to_ascii_lowercase();

    let (res, unit_lbl) = match (from.as_str(), to.as_str()) {
        ("km", "mi") | ("km", "mil") => (v * 0.621371, "mil"),
        ("mi", "km") | ("mil", "km") => (v * 1.609344, "km"),
        ("m", "ft") => (v * 3.28084, "ft"),
        ("ft", "m") => (v / 3.28084, "m"),
        ("cm", "in") | ("cm", "inch") => (v / 2.54, "inç"),
        ("in", "cm") | ("inch", "cm") => (v * 2.54, "cm"),
        ("kg", "lb") | ("kg", "lbs") => (v * 2.20462, "lb"),
        ("lb", "kg") | ("lbs", "kg") => (v / 2.20462, "kg"),
        ("c", "f") => (v * 9.0 / 5.0 + 32.0, "°F"),
        ("f", "c") => ((v - 32.0) * 5.0 / 9.0, "°C"),
        ("gb", "mb") => (v * 1024.0, "MB"),
        ("mb", "gb") => (v / 1024.0, "GB"),
        ("tb", "gb") => (v * 1024.0, "GB"),
        _ => return None,
    };
    let out = format!("{res:.2}").trim_end_matches('0').trim_end_matches('.').to_string();
    Some((format!("{out} {unit_lbl}"), format!("Birim Çeviri: {input}")))
}

fn parse_math_expr(expr: &str) -> Option<f64> {
    let bytes: Vec<char> = expr.chars().filter(|c| !c.is_whitespace()).collect();
    let mut pos = 0usize;
    let val = parse_add_sub(&bytes, &mut pos)?;
    if pos == bytes.len() { Some(val) } else { None }
}

fn parse_add_sub(s: &[char], p: &mut usize) -> Option<f64> {
    let mut lhs = parse_mul_div(s, p)?;
    while *p < s.len() {
        match s[*p] {
            '+' => { *p += 1; lhs += parse_mul_div(s, p)?; }
            '-' => { *p += 1; lhs -= parse_mul_div(s, p)?; }
            _ => break,
        }
    }
    Some(lhs)
}

fn parse_mul_div(s: &[char], p: &mut usize) -> Option<f64> {
    let mut lhs = parse_pow(s, p)?;
    while *p < s.len() {
        match s[*p] {
            '*' => { *p += 1; lhs *= parse_pow(s, p)?; }
            '/' => {
                *p += 1;
                let rhs = parse_pow(s, p)?;
                if rhs.abs() < 1e-15 { return None; }
                lhs /= rhs;
            }
            '%' => {
                *p += 1;
                if *p < s.len() && (s[*p].is_ascii_digit() || s[*p] == '(') {
                    let rhs = parse_pow(s, p)?;
                    if rhs.abs() < 1e-15 { return None; }
                    lhs %= rhs;
                } else {
                    lhs /= 100.0;
                }
            }
            _ => break,
        }
    }
    Some(lhs)
}

fn parse_pow(s: &[char], p: &mut usize) -> Option<f64> {
    let base = parse_unary(s, p)?;
    if *p < s.len() && s[*p] == '^' {
        *p += 1;
        let exp = parse_pow(s, p)?;
        Some(base.powf(exp))
    } else {
        Some(base)
    }
}

fn parse_unary(s: &[char], p: &mut usize) -> Option<f64> {
    if *p < s.len() && s[*p] == '-' {
        *p += 1;
        return Some(-parse_unary(s, p)?);
    }
    if *p < s.len() && s[*p] == '+' {
        *p += 1;
        return parse_unary(s, p);
    }
    parse_primary(s, p)
}

fn parse_primary(s: &[char], p: &mut usize) -> Option<f64> {
    if *p >= s.len() { return None; }
    if s[*p] == '(' {
        *p += 1;
        let v = parse_add_sub(s, p)?;
        if *p < s.len() && s[*p] == ')' {
            *p += 1;
            return Some(v);
        }
        return None;
    }
    let start = *p;
    while *p < s.len() && (s[*p].is_ascii_digit() || s[*p] == '.' || s[*p] == ',') {
        *p += 1;
    }
    if *p == start { return None; }
    let num_str: String = s[start..*p].iter().map(|&c| if c == ',' { '.' } else { c }).collect();
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
        assert_eq!(evaluate_omni_expression("2 ^ 10").unwrap().0, "1024");
        assert_eq!(evaluate_omni_expression("100 c to f").unwrap().0, "212 °F");
        assert_eq!(evaluate_omni_expression("100 cm to in").unwrap().0, "39.37 inç");
        assert!(evaluate_omni_expression("whatsapp").is_none());
    }

    #[test]
    fn turkish_fuzzy_search_matches_diacritics() {
        assert!(fuzzy_match_score("ayarlar", "Ayarlar", 0).unwrap() >= 800);
        assert!(fuzzy_match_score("guc", "Güç ve Pil", 5).unwrap() > 600);
        assert!(fuzzy_match_score("ekran alintisi", "Ekran Alıntısı", 2).unwrap() > 800);
    }
}
