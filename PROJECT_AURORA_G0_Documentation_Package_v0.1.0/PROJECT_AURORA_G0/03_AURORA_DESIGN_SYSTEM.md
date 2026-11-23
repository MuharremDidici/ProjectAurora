# PROJECT AURORA — Design System Specification

**Document ID:** AURORA-DS-001  
**Version:** 0.1.0-draft  
**Date:** 2026-11-17  
**Status:** G0 review draft — no UI implementation authorized

## 1. Design philosophy

Aurora must feel like a coherent premium desktop product, not a collection of unrelated widgets or a clone of another operating system.

Design principles:
1. **Clarity before decoration:** information hierarchy and state are always obvious.
2. **Premium restraint:** materials, gradients and glow are used intentionally.
3. **Functional honesty:** disabled, unavailable, loading and error states are explicit.
4. **Consistent interaction:** the same component behaves the same way across modules.
5. **Personalization without fragmentation:** themes may change visual expression but not usability contracts.
6. **Accessibility by default:** keyboard, screen reader, contrast and reduced motion are first-class.
7. **Performance-aware materials:** blur and animation must degrade gracefully.

## 2. Theme families

### 2.1 Aurora Glass — default recommendation
- Frosted translucent surfaces.
- Subtle background blur and fine edge highlights.
- Soft shadows with restrained depth.
- Neutral dark and light variants.
- A single configurable accent color.
- Motion that feels fluid but never delays interaction.

### 2.2 Obsidian Pro
- Dense, high-contrast professional surfaces.
- Minimal gradients and reduced blur.
- Strong typography hierarchy.
- Compact controls and clear separators.
- Low-motion option by default.

### 2.3 Aurora Flow
- Dynamic gradient accents and layered color fields.
- Carefully limited animated ambient backgrounds.
- Bright but controlled status colors.
- Strong contrast over every gradient.
- Animations pause when the surface is hidden.

### 2.4 Nebula
- Dark, futuristic surfaces.
- Neon accents used only for focus and active states.
- Fine luminous outlines, not excessive glow.
- Optional advanced visual effects.
- Reduced-effects mode for low-power hardware.

Themes are visual skins over shared semantic components. They cannot redefine command behavior, keyboard semantics or information architecture.

## 3. Semantic design tokens

Use semantic tokens rather than hard-coded colors in feature components.

### 3.1 Color roles
- `color.canvas`
- `color.surface.1`, `color.surface.2`, `color.surface.3`
- `color.surface.elevated`
- `color.text.primary`, `color.text.secondary`, `color.text.disabled`
- `color.border.subtle`, `color.border.strong`
- `color.accent.primary`, `color.accent.secondary`
- `color.state.success`, `color.state.warning`, `color.state.danger`, `color.state.info`
- `color.focus.ring`

### 3.2 Typography roles
- `type.display`
- `type.title`
- `type.section`
- `type.body`
- `type.body.strong`
- `type.caption`
- `type.numeric`
- Use a Windows-native system font stack initially; custom font choices require licensing and readability review.

### 3.3 Spacing scale
Base unit: 4 effective DIPs.
- `space.1 = 4`
- `space.2 = 8`
- `space.3 = 12`
- `space.4 = 16`
- `space.5 = 20`
- `space.6 = 24`
- `space.8 = 32`
- `space.10 = 40`

### 3.4 Radius scale
- `radius.xs = 6`
- `radius.sm = 10`
- `radius.md = 14`
- `radius.lg = 20`
- `radius.xl = 26`
- `radius.pill = 999`

Values are initial design tokens and must be validated at 100–200% display scaling.

### 3.5 Elevation and material
- `elevation.0` flat.
- `elevation.1` subtle separation.
- `elevation.2` floating cards.
- `elevation.3` transient panels.
- `material.blur.subtle`, `material.blur.standard`, `material.blur.strong`.
- `material.opacity.*` semantic levels.

Blur is optional decoration. If unsupported or expensive, replace it with opaque/near-opaque surfaces that preserve contrast.

### 3.6 Motion tokens
- `motion.fast = 100–140 ms`
- `motion.standard = 160–220 ms`
- `motion.emphasis = 240–320 ms`
- Use ease-out for entrance and ease-in for exit.
- Avoid spring/bounce motion for frequently repeated system controls.
- Honor reduced-motion and battery-saver settings.

## 4. Layout and density

### 4.1 Taskbar
- Three density presets: Compact, Standard, Spacious.
- Edge positions: bottom, top, left, right only where validated.
- Icon sizes must scale with effective DPI.
- Active, pinned, running, attention and unavailable states must be distinct.
- Hover preview must not obscure the taskbar or cover the target window unnecessarily.
- Auto-hide reveal zone must be reliable and configurable.

### 4.2 Control Center
- Compact: essential quick toggles and sliders.
- Standard: primary cards plus media summary.
- Expanded: full controls, device selection and detail rows.
- Cards have consistent headers, status, primary action and optional secondary navigation.
- Card order and visibility are user-configurable.
- State must be clear: on, off, pending, unavailable, permission required, error.

### 4.3 Notification Center
- Group by source and time.
- Strong distinction between title, body, timestamp and actions.
- Avoid excessive card chrome.
- Clear all must be deliberate and reversible where feasible.
- Empty state should be useful, not decorative.

### 4.4 Widgets
- Shared header and overflow behavior.
- Standard size classes: Small, Medium, Large.
- Each widget exposes refresh status, error state and configuration.
- No widget may consume unbounded CPU through polling.

## 5. Component library

Required shared components:
- `AuroraSurface`
- `AuroraPanel`
- `AuroraCard`
- `AuroraIconButton`
- `AuroraToggle`
- `AuroraSlider`
- `AuroraSegmentedControl`
- `AuroraDeviceRow`
- `AuroraStatusPill`
- `AuroraNotificationCard`
- `AuroraWidgetFrame`
- `AuroraMenu`
- `AuroraTooltip`
- `AuroraEmptyState`
- `AuroraErrorState`
- `AuroraSkeleton`
- `AuroraFocusRing`

Every component specification must define:
- Anatomy.
- Variants.
- States.
- Keyboard behavior.
- Accessible name/role/value.
- Minimum target size.
- Theme token dependencies.
- Loading and error behavior.

## 6. Interaction standards

- Primary click targets activate immediately.
- Sliders update continuously but persistence may be debounced.
- Toggles show pending state during asynchronous system operations.
- Never visually commit a toggle before the underlying operation succeeds unless an explicit optimistic-state model and rollback are defined.
- Tooltips appear after a short delay and do not contain essential information.
- Context menus support keyboard invocation.
- Escape closes the topmost transient surface.
- Outside click closes transient panels unless the user is interacting with a child control.
- Focus returns to the invoking control when a panel closes.
- Global shortcuts must be configurable and conflict-checked.

## 7. Accessibility requirements

- Target WCAG 2.2 AA contrast for text and controls.
- Keyboard access to all P0 functions.
- Visible focus ring in every theme.
- UI Automation properties for controls.
- Screen-reader announcements for system operation success/failure.
- Reduced-motion mode.
- High-contrast compatibility.
- Do not encode status by hue alone; pair color with icon/text/shape.
- Support 100%, 125%, 150% and 200% scaling.

## 8. Responsive and multi-monitor rules

- Use effective DIPs for layout; convert to physical pixels only at platform boundaries.
- Each monitor has its own geometry, DPI and taskbar layout.
- Recalculate after display topology changes.
- Do not persist raw physical pixel coordinates as the only layout representation.
- Keep transient panels inside the monitor work area.
- Handle primary monitor changes without orphaning panels.

## 9. Theme package governance

- Themes are data-only in v1.
- No executable scripts, custom DLLs or arbitrary XAML.
- Versioned schema and strict allowlist of properties.
- Assets must be bounded by file size and dimensions.
- Reject absolute paths, path traversal and external network references.
- Provide default theme recovery.
- Imported theme preview must be sandboxed from applying until user confirms.

## 10. Design acceptance checklist

- [ ] Four theme families are visually distinct but share interaction behavior.
- [ ] Taskbar, Control Center, notifications and widgets share coherent typography and spacing.
- [ ] Every control has all required states.
- [ ] Keyboard focus is visible in all themes.
- [ ] Reduced-motion and high-contrast modes are defined.
- [ ] Mixed-DPI and multi-monitor layouts are specified.
- [ ] Theme import/export security constraints are documented.
- [ ] Blur fallback and low-power behavior are defined.
