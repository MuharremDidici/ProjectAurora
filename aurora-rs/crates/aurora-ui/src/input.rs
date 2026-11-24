/// UTF-8 & Turkish-IME-safe single/multi-line text input state for OmniSearch, Clipboard Search,
/// Agenda Notes, Scratchpad, and HEX Color input.
#[derive(Debug, Clone, Default)]
pub struct TextInputState {
    pub text: String,
    pub cursor_char: usize,
    pub focused: bool,
    pub placeholder: String,
}

impl TextInputState {
    pub fn new(placeholder: impl Into<String>) -> Self {
        Self {
            text: String::new(),
            cursor_char: 0,
            focused: false,
            placeholder: placeholder.into(),
        }
    }

    pub fn set_text(&mut self, s: impl Into<String>) {
        self.text = s.into();
        self.cursor_char = self.text.chars().count();
    }

    pub fn clear(&mut self) {
        self.text.clear();
        self.cursor_char = 0;
    }

    pub fn insert_char(&mut self, ch: char) {
        if ch.is_control() {
            return;
        }
        let byte_idx = self.char_to_byte(self.cursor_char);
        self.text.insert(byte_idx, ch);
        self.cursor_char += 1;
    }

    pub fn insert_str(&mut self, s: &str) {
        for ch in s.chars() {
            if !ch.is_control() || ch == '\n' {
                let byte_idx = self.char_to_byte(self.cursor_char);
                self.text.insert(byte_idx, ch);
                self.cursor_char += 1;
            }
        }
    }

    pub fn backspace(&mut self) {
        if self.cursor_char == 0 {
            return;
        }
        let start = self.char_to_byte(self.cursor_char - 1);
        let end = self.char_to_byte(self.cursor_char);
        self.text.replace_range(start..end, "");
        self.cursor_char -= 1;
    }

    pub fn delete(&mut self) {
        let len = self.text.chars().count();
        if self.cursor_char >= len {
            return;
        }
        let start = self.char_to_byte(self.cursor_char);
        let end = self.char_to_byte(self.cursor_char + 1);
        self.text.replace_range(start..end, "");
    }

    pub fn move_left(&mut self) {
        self.cursor_char = self.cursor_char.saturating_sub(1);
    }

    pub fn move_right(&mut self) {
        let len = self.text.chars().count();
        if self.cursor_char < len {
            self.cursor_char += 1;
        }
    }

    pub fn move_home(&mut self) {
        self.cursor_char = 0;
    }

    pub fn move_end(&mut self) {
        self.cursor_char = self.text.chars().count();
    }

    pub fn text_before_cursor(&self) -> &str {
        &self.text[..self.char_to_byte(self.cursor_char)]
    }

    fn char_to_byte(&self, char_idx: usize) -> usize {
        self.text
            .char_indices()
            .nth(char_idx)
            .map(|(b, _)| b)
            .unwrap_or(self.text.len())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn turkish_multibyte_editing_stays_utf8_safe() {
        let mut inp = TextInputState::new("Ara...");
        for c in "Çalışma Öğesi Şİ".chars() {
            inp.insert_char(c);
        }
        assert_eq!(inp.text, "Çalışma Öğesi Şİ");
        inp.backspace();
        inp.move_left();
        inp.insert_char('ğ');
        assert_eq!(inp.text, "Çalışma Öğesi ğŞ");
    }
}
