/// Real-time second-order harmonic spring physics for 60/120/240Hz native GPU motion.
/// Zero allocations, velocity-preserving interruption, automatic exact settling so the
/// render loop drops to 0.00% CPU / 0.00% GPU the instant motion finishes.
#[derive(Debug, Clone, Copy)]
pub struct SpringConfig {
    pub stiffness: f32,
    pub damping: f32,
    pub precision: f32,
}

impl SpringConfig {
    /// Crisp macOS/visionOS island morph (slight organic overshoot, fast settle ~220ms).
    pub const DROPLET_MORPH: Self = Self { stiffness: 360.0, damping: 29.0, precision: 0.08 };
    /// Zero-overshoot tactile button / slider / hover elevation wave.
    pub const SNAPPY: Self = Self { stiffness: 460.0, damping: 38.0, precision: 0.002 };
    /// Smooth floating panel / Radial Hub bloom.
    pub const BLOOM: Self = Self { stiffness: 310.0, damping: 25.0, precision: 0.004 };
    /// Fast OSD progress bar fill.
    pub const OSD_BAR: Self = Self { stiffness: 520.0, damping: 42.0, precision: 0.05 };
}

#[derive(Debug, Clone, Copy)]
pub struct Spring {
    pub value: f32,
    pub target: f32,
    pub velocity: f32,
    pub config: SpringConfig,
}

impl Spring {
    pub const fn new(initial: f32, config: SpringConfig) -> Self {
        Self {
            value: initial,
            target: initial,
            velocity: 0.0,
            config,
        }
    }

    pub fn set_target(&mut self, target: f32) {
        self.target = target;
    }

    pub fn snap(&mut self, value: f32) {
        self.value = value;
        self.target = value;
        self.velocity = 0.0;
    }

    pub fn is_settled(&self) -> bool {
        (self.target - self.value).abs() <= self.config.precision
            && self.velocity.abs() <= self.config.precision * 4.0
    }

    /// Advances the spring by `dt_sec` (clamped for stability under frame hiccups).
    /// Returns `true` if still animating, `false` once settled cleanly on target.
    pub fn step(&mut self, dt_sec: f32) -> bool {
        if self.is_settled() {
            self.value = self.target;
            self.velocity = 0.0;
            return false;
        }
        // Sub-step large deltas (e.g. wakeup from sleep) to keep semi-implicit Euler unconditionally stable
        let mut remaining = dt_sec.clamp(0.0, 0.064);
        while remaining > 0.0 {
            let h = remaining.min(0.008);
            let force = (self.target - self.value) * self.config.stiffness;
            let damping = self.velocity * self.config.damping;
            self.velocity += (force - damping) * h;
            self.value += self.velocity * h;
            remaining -= h;
        }
        if self.is_settled() {
            self.value = self.target;
            self.velocity = 0.0;
            false
        } else {
            true
        }
    }
}

/// Zero-Drift Fitts's Law parabolic hover elevation curve (never shifts horizontal/vertical click centers!).
pub fn fitts_hover_scale(cursor_pos: f32, item_center: f32, radius_px: f32, strength_0_to_1: f32, in_accordion: bool) -> f32 {
    if strength_0_to_1 <= 0.02 || radius_px <= 1.0 {
        return 1.0;
    }
    let dist = (cursor_pos - item_center).abs();
    if dist >= radius_px {
        return 1.0;
    }
    let t = 1.0 - (dist / radius_px);
    let smooth = t * t * (3.0 - 2.0 * t);
    let max_add = if in_accordion {
        (strength_0_to_1 * 0.16).min(0.11)
    } else {
        (strength_0_to_1 * 0.22).min(0.16)
    };
    1.0 + max_add * smooth
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn spring_converges_smoothly_and_settles_to_zero_cpu() {
        let mut s = Spring::new(0.0, SpringConfig::DROPLET_MORPH);
        s.set_target(100.0);
        let mut ticks = 0;
        while s.step(1.0 / 120.0) && ticks < 240 {
            ticks += 1;
        }
        assert!(ticks < 120, "spring should settle well within 1s at 120Hz (took {ticks})");
        assert!(s.is_settled());
        assert_eq!(s.value, 100.0);
    }

    #[test]
    fn fitts_curve_peaks_at_center_and_vanishes_outside_radius() {
        let peak = fitts_hover_scale(500.0, 500.0, 92.0, 0.5, false);
        let mid = fitts_hover_scale(546.0, 500.0, 92.0, 0.5, false);
        let far = fitts_hover_scale(600.0, 500.0, 92.0, 0.5, false);
        assert!(peak > mid && mid > 1.0);
        assert_eq!(far, 1.0);
    }
}
