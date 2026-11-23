namespace Aurora.App.Host;

public static class AuroraSurfaceTemplates
{
    public const string SharedDesignCss = """
        :root {
            --bg-glass-1: rgba(9, 13, 24, 0.96);
            --bg-glass-2: rgba(14, 21, 38, 0.985);
            --bg-card: rgba(255, 255, 255, 0.048);
            --bg-card-hover: rgba(255, 255, 255, 0.095);
            --bg-inset: rgba(0, 0, 0, 0.34);
            --border-rim: rgba(255, 255, 255, 0.16);
            --border-subtle: rgba(255, 255, 255, 0.085);
            --text-1: #F8FAFC;
            --text-2: #94A3B8;
            --accent-1: #38BDF8;
            --accent-2: #818CF8;
            --accent-ink: #04101F;
            --success: #34D399;
            --warning: #FBBF24;
            --danger: #FB7185;
            --font: 'Segoe UI Variable Display', 'Segoe UI Variable Text', 'Segoe UI', system-ui, sans-serif;
            --spring: cubic-bezier(0.22, 1.18, 0.36, 1);
            --ease: cubic-bezier(0.22, 1, 0.36, 1);
        }
        * { box-sizing: border-box; user-select: none; -webkit-user-drag: none; }
        html, body {
            margin: 0; padding: 0; width: 100%; height: 100%;
            background: transparent !important;
            overflow: hidden;
            font-family: var(--font);
            color: var(--text-1);
            -webkit-font-smoothing: antialiased;
        }
        .num { font-variant-numeric: tabular-nums; }
        ::-webkit-scrollbar { width: 5px; height: 5px; }
        ::-webkit-scrollbar-thumb { background: rgba(148,163,184,0.28); border-radius: 99px; }
        .svg-ic {
            width: 15px; height: 15px; display: inline-flex; align-items: center; justify-content: center;
            flex-shrink: 0; stroke: currentColor; fill: none;
            stroke-width: 1.85; stroke-linecap: round; stroke-linejoin: round;
        }
        .svg-ic.sm { width: 13px; height: 13px; }
        .svg-ic.lg { width: 18px; height: 18px; }
        .glass-shell {
            position: relative;
            background:
                radial-gradient(ellipse 65% 55% at 18% 0%, rgba(56, 189, 248, 0.12), transparent 65%),
                radial-gradient(ellipse 50% 45% at 88% 100%, rgba(129, 140, 248, 0.09), transparent 60%),
                linear-gradient(160deg, var(--bg-glass-1), var(--bg-glass-2));
            border: 1px solid var(--border-rim);
            box-shadow:
                0 24px 58px -14px rgba(0, 0, 0, 0.80),
                0 6px 20px -4px rgba(0, 0, 0, 0.45),
                inset 0 1px 0 rgba(255, 255, 255, 0.20),
                inset 0 0 0 0.5px rgba(255, 255, 255, 0.06);
        }
        .pill-btn {
            display: inline-flex; align-items: center; gap: 6px;
            height: 29px; padding: 0 11px;
            border-radius: 999px;
            border: 1px solid var(--border-subtle);
            background: var(--bg-card);
            color: var(--text-1);
            font-family: var(--font); font-size: 12px; font-weight: 600;
            letter-spacing: -0.01em;
            cursor: pointer;
            transition: transform 0.16s var(--ease), background 0.15s, border-color 0.15s, box-shadow 0.15s;
            white-space: nowrap;
        }
        .pill-btn:hover { background: var(--bg-card-hover); border-color: var(--border-rim); transform: translateY(-0.5px); }
        .pill-btn:active { transform: scale(0.96); }
        .pill-btn.accent {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
            border-color: transparent;
            box-shadow: 0 6px 18px -4px rgba(56, 189, 248, 0.48);
        }
        .pill-btn.accent:hover { filter: brightness(1.06); }
        .pill-btn.subtle { background: transparent; border-color: transparent; color: var(--text-2); }
        .pill-btn.subtle:hover { background: var(--bg-card); color: var(--text-1); border-color: var(--border-subtle); }
        .micro-btn {
            width: 26px; height: 26px; border-radius: 8px;
            border: 1px solid var(--border-subtle);
            background: var(--bg-card);
            color: var(--text-2);
            display: inline-flex; align-items: center; justify-content: center;
            cursor: pointer; flex-shrink: 0;
            transition: all 0.14s var(--ease);
        }
        .micro-btn:hover { background: var(--bg-card-hover); color: var(--text-1); border-color: var(--accent-1); }
        .micro-btn.active { background: rgba(56,189,248,0.22); color: var(--accent-1); border-color: var(--accent-1); }
        .micro-btn.danger:hover { background: rgba(251,113,133,0.22); color: var(--danger); border-color: var(--danger); }
        """;

    public const string SharedSvgJs = """
        const SVG = {
            search: '<svg class="svg-ic" viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>',
            bolt: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M13 2 4 14h7l-1 8 9-12h-7z"/></svg>',
            wifi: '<svg class="svg-ic" viewBox="0 0 24 24"><path d="M5 12.55a11 11 0 0 1 14.08 0"/><path d="M8.53 16.11a6 6 0 0 1 6.95 0"/><circle cx="12" cy="19.5" r="1"/></svg>',
            bluetooth: '<svg class="svg-ic" viewBox="0 0 24 24"><path d="m7 7 10 10-5 5V2l5 5L7 17"/></svg>',
            moon: '<svg class="svg-ic" viewBox="0 0 24 24"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>',
            sparkles: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m12 3 1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z"/><path d="M19 3v4"/><path d="M21 5h-4"/></svg>',
            vol: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M11 5 6 9H2v6h4l5 4z"/><path d="M15.54 8.46a5 5 0 0 1 0 7.07"/><path d="M19.07 4.93a10 10 0 0 1 0 14.14"/></svg>',
            mute: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M11 5 6 9H2v6h4l5 4z"/><path d="m22 9-6 6"/><path d="m16 9 6 6"/></svg>',
            sun: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M4.93 4.93l1.41 1.41m11.32 11.32 1.41 1.41M2 12h2m16 0h2M4.93 19.07l1.41-1.41m11.32-11.32 1.41-1.41"/></svg>',
            tray: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m18 15-6-6-6 6"/></svg>',
            calendar: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="18" rx="3"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>',
            globe: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M3.6 9h16.8M3.6 15h16.8M12 3a14.5 14.5 0 0 0 0 18 14.5 14.5 0 0 0 0-18z"/></svg>',
            inbox: '<svg class="svg-ic sm" viewBox="0 0 24 24"><polyline points="22 12 16 12 14 15 10 15 8 12 2 12"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z"/></svg>',
            pin: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M12 17v5"/><path d="M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z"/></svg>',
            snapL: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M12 4v16"/><path d="M3 4h9v16H3z" fill="currentColor" opacity="0.28"/></svg>',
            snapR: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M12 4v16"/><path d="M12 4h9v16h-9z" fill="currentColor" opacity="0.28"/></svg>',
            snapC: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="2"/><rect x="7" y="7" width="10" height="10" rx="1" fill="currentColor" opacity="0.28"/></svg>',
            max: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="2"/></svg>',
            min: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M5 12h14"/></svg>',
            close: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M18 6 6 18M6 6l12 12"/></svg>',
            chevD: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m6 9 6 6 6-6"/></svg>',
            chevU: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m18 15-6-6-6 6"/></svg>',
            chevL: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m15 18-6-6 6-6"/></svg>',
            chevR: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m9 18 6-6-6-6"/></svg>',
            lock: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/></svg>',
            sleep: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg>',
            desktop: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/></svg>',
            scissors: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="6" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><path d="M20 4 8.12 15.88M14.47 14.48 20 20M8.12 8.12 12 12"/></svg>',
            activity: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>',
            settings: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9c.26.6.85 1 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>',
            trash: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M3 6h18M8 6V4h8v2m-9 0 1 14h8l1-14"/></svg>',
            folder: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>',
            copy: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>',
            play: '<svg class="svg-ic sm" viewBox="0 0 24 24"><polygon points="6 3 20 12 6 21 6 3" fill="currentColor"/></svg>',
            pause: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="6" y="4" width="4" height="16" fill="currentColor"/><rect x="14" y="4" width="4" height="16" fill="currentColor"/></svg>',
            prev: '<svg class="svg-ic sm" viewBox="0 0 24 24"><polygon points="19 20 9 12 19 4 19 20" fill="currentColor"/><path d="M5 19V5"/></svg>',
            next: '<svg class="svg-ic sm" viewBox="0 0 24 24"><polygon points="5 4 15 12 5 20 5 4" fill="currentColor"/><path d="M19 5v14"/></svg>',
            plus: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M12 5v14M5 12h14"/></svg>',
            palette: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="13.5" cy="6.5" r="1.2" fill="currentColor"/><circle cx="17.5" cy="10.5" r="1.2" fill="currentColor"/><circle cx="8.5" cy="7.5" r="1.2" fill="currentColor"/><circle cx="6.5" cy="12.5" r="1.2" fill="currentColor"/><path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.93 0 1.5-.75 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c3.31 0 6-2.69 6-6 0-4.96-4.49-9-10-9z"/></svg>',
            sliders: '<svg class="svg-ic sm" viewBox="0 0 24 24"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg>',
            layers: '<svg class="svg-ic sm" viewBox="0 0 24 24"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>',
            power: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M18.36 6.64a9 9 0 1 1-12.73 0"/><line x1="12" y1="2" x2="12" y2="12"/></svg>',
            timer: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="12" cy="13" r="8"/><path d="M12 9v4l2.5 2.5M10 2h4"/></svg>',
            music: '<svg class="svg-ic lg" viewBox="0 0 24 24"><path d="M9 18V5l12-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="18" cy="16" r="3"/></svg>',
            monitor: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/></svg>',
            cloud: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z"/></svg>',
            bell: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>',
            clip: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="8" y="2" width="8" height="4" rx="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/></svg>',
            note: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>',
            exportPkg: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>',
            mic: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="9" y="2" width="6" height="11" rx="3"/><path d="M5 10a7 7 0 0 0 14 0"/><path d="M12 19v3M8 22h8"/></svg>',
            micOff: '<svg class="svg-ic sm" viewBox="0 0 24 24"><line x1="2" y1="2" x2="22" y2="22"/><path d="M18.89 13.23A7.12 7.12 0 0 0 19 12v-2"/><path d="M5 10v2a7 7 0 0 0 12 5"/><path d="M15 9.34V5a3 3 0 0 0-5.68-1.33"/><path d="M12 19v3M8 22h8"/></svg>',
            headphones: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M3 18v-6a9 9 0 0 1 18 0v6"/><path d="M21 19a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 19a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z"/></svg>',
            restart: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/></svg>',
            coffee: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M17 8h1a4 4 0 1 1 0 8h-1"/><path d="M3 8h14v9a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4Z"/><line x1="6" y1="2" x2="6" y2="4"/><line x1="10" y1="2" x2="10" y2="4"/><line x1="14" y1="2" x2="14" y2="4"/></svg>',
            scanText: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M3 7V5a2 2 0 0 1 2-2h2"/><path d="M17 3h2a2 2 0 0 1 2 2v2"/><path d="M21 17v2a2 2 0 0 1-2 2h-2"/><path d="M7 21H5a2 2 0 0 1-2-2v-2"/><path d="M7 9h10"/><path d="M7 12h7"/><path d="M7 15h9"/></svg>',
            pipette: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="m2 22 1-1h3l9-9"/><path d="M3 21v-3l9-9"/><path d="m15 6 3.4-3.4a2.1 2.1 0 1 1 3 3L18 9l.4.4a2.1 2.1 0 1 1-3 3l-3.8-3.8a2.1 2.1 0 1 1 3-3l.4.4Z"/></svg>',
            grid: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/></svg>',
            refresh: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"/><path d="M16 16h5v5"/></svg>',
            capsLock: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M12 4 5 12h4v4h6v-4h4L12 4Z"/><line x1="6" y1="20" x2="18" y2="20"/></svg>',
            moveWin: '<svg class="svg-ic sm" viewBox="0 0 24 24"><polyline points="5 9 2 12 5 15"/><polyline points="9 5 12 2 15 5"/><polyline points="15 19 12 22 9 19"/><polyline points="19 9 22 12 19 15"/><line x1="2" y1="12" x2="22" y2="12"/><line x1="12" y1="2" x2="12" y2="22"/></svg>',
            qr: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h3v3h-3zM20 14v3h-1M14 20h3M20 20h1"/></svg>',
            record: '<svg class="svg-ic sm" viewBox="0 0 24 24"><circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="3.5" fill="currentColor"/></svg>',
            camera: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/></svg>',
            battery: '<svg class="svg-ic sm" viewBox="0 0 24 24"><rect x="2" y="7" width="18" height="10" rx="2"/><path d="M22 11v2"/></svg>',
            zip: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M21 8v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8"/><path d="M23 4H1v4h22V4z"/><path d="M10 12h4"/></svg>',
            extLink: '<svg class="svg-ic sm" viewBox="0 0 24 24"><path d="M15 3h6v6"/><path d="M10 14 21 3"/><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/></svg>'
        };
        """;

    public static string BuildCrownHtml() => $$$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="UTF-8">
        <style>
        {{{SharedDesignCss}}}
        .stage {
            width: 100%; height: 100%;
            display: flex; flex-direction: column; align-items: center;
            padding-top: 0;
            pointer-events: none;
        }
        #crownWrap {
            pointer-events: auto;
            display: flex; flex-direction: column; align-items: center;
            padding: 0 14px 8px 14px;
            max-width: 100vw;
        }
        #crown {
            pointer-events: auto;
            position: relative;
            max-width: calc(100vw - 20px);
            overflow: hidden;
            transition:
                margin-top 0.30s var(--spring),
                height 0.30s var(--spring),
                border-radius 0.30s var(--spring),
                padding 0.28s var(--ease),
                box-shadow 0.28s var(--ease);
        }
        /* LIQUID DROPLET RESTING STATE ("Damla") — Zero Tab Clutter! */
        #crown.droplet {
            margin-top: 0;
            height: 25px;
            border-top: none;
            border-radius: 0 0 18px 18px;
            padding: 0 15px;
            display: inline-flex; align-items: center; gap: 8px;
            cursor: pointer;
            box-shadow:
                0 10px 26px -6px rgba(0, 0, 0, 0.72),
                0 2px 10px rgba(56, 189, 248, 0.18),
                inset 0 -1px 0 rgba(56, 189, 248, 0.35);
        }
        #crown.droplet::after {
            content: '';
            position: absolute; bottom: 0; left: 20%; right: 20%; height: 2px;
            border-radius: 99px;
            background: linear-gradient(90deg, transparent, var(--accent-1), var(--accent-2), transparent);
            opacity: 0.72;
        }
        /* LIVE HARDWARE OSD MORPH INSIDE LIQUID DROPLET (Boring.Notch / ModernFlyouts grade) */
        #crown.droplet.osd-active {
            height: 36px;
            min-width: 242px;
            padding: 0 15px;
            border-radius: 0 0 21px 21px;
            gap: 9px;
            box-shadow:
                0 14px 36px -8px rgba(0, 0, 0, 0.78),
                0 0 18px rgba(56, 189, 248, 0.28),
                inset 0 -1px 0 rgba(56, 189, 248, 0.55);
        }
        #crown.droplet.osd-active.osd-brightness {
            box-shadow:
                0 14px 36px -8px rgba(0, 0, 0, 0.78),
                0 0 18px rgba(245, 158, 11, 0.30),
                inset 0 -1px 0 rgba(251, 191, 36, 0.65);
        }
        #crown.droplet.osd-active.osd-mute,
        #crown.droplet.osd-active.osd-off {
            box-shadow:
                0 14px 36px -8px rgba(0, 0, 0, 0.78),
                0 0 18px rgba(251, 113, 133, 0.28),
                inset 0 -1px 0 rgba(251, 113, 133, 0.55);
        }
        .osd-icon {
            display: inline-flex; align-items: center; justify-content: center;
            color: var(--accent-1); flex-shrink: 0;
        }
        .osd-brightness .osd-icon { color: #FBBF24; }
        .osd-mute .osd-icon, .osd-off .osd-icon { color: var(--danger); }
        .osd-label {
            font-size: 11.5px; font-weight: 700; color: var(--text-1);
            white-space: nowrap; flex-shrink: 0;
        }
        .osd-bar-track {
            flex: 1; min-width: 92px; height: 6px; border-radius: 99px;
            background: rgba(148, 163, 184, 0.22); overflow: hidden;
        }
        .osd-bar-fill {
            height: 100%; border-radius: 99px;
            background: linear-gradient(90deg, var(--accent-1), var(--accent-2));
            transition: width 0.11s ease-out;
        }
        .osd-brightness .osd-bar-fill { background: linear-gradient(90deg, #FBBF24, #F59E0B); }
        .osd-mute .osd-bar-fill { background: var(--danger); }
        .osd-val {
            font-size: 11.5px; font-weight: 800; color: var(--text-1);
            min-width: 34px; text-align: right; flex-shrink: 0;
        }
        .osd-status-pill {
            padding: 2px 9px; border-radius: 99px; font-size: 10.5px; font-weight: 800;
            letter-spacing: 0.04em;
            background: rgba(56, 189, 248, 0.20); color: var(--accent-1);
        }
        .osd-off .osd-status-pill, .osd-mute .osd-status-pill {
            background: rgba(251, 113, 133, 0.20); color: var(--danger);
        }
        #osdFloat {
            pointer-events: none;
            margin-top: 8px; height: 34px; min-width: 230px; padding: 0 14px;
            border-radius: 999px;
            display: none; align-items: center; gap: 9px;
            animation: popDown 0.18s var(--spring);
        }
        #osdFloat.show { display: inline-flex; }
        .drop-dot {
            width: 6.5px; height: 6.5px; border-radius: 50%;
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            box-shadow: 0 0 9px var(--accent-1);
        }
        .drop-time {
            font-size: 11.5px; font-weight: 700; letter-spacing: 0.02em;
            color: var(--text-1);
        }
        .drop-pill-tag {
            font-size: 10px; font-weight: 700; color: var(--accent-1);
            padding: 1px 6px; border-radius: 99px;
            background: rgba(56, 189, 248, 0.14);
        }

        /* HOVER-EXPANDED LIQUID ACTION PILL */
        #crown.pill {
            margin-top: 7px;
            height: 43px;
            border-radius: 999px;
            padding: 0 8px;
            display: inline-flex; align-items: center; gap: 5px;
        }
        #crown.pill .pill-btn {
            height: 29px; padding: 0 8.5px; font-size: 11.5px; flex-shrink: 0;
        }
        #crown.pill .micro-btn {
            width: 28px; height: 28px; flex-shrink: 0;
        }

        /* FULL BENTO COMMAND DECK */
        #crown.expanded {
            margin-top: 8px;
            width: min(880px, calc(100vw - 24px));
            height: min(504px, calc(100vh - 36px));
            border-radius: 24px;
            padding: 14px 16px;
            display: flex; flex-direction: column; gap: 11px;
        }
        #crown.drag-over {
            margin-top: 7px;
            height: 48px;
            border-radius: 999px;
            padding: 0 22px;
            border-color: var(--accent-1) !important;
            box-shadow: 0 0 0 2px var(--accent-1), 0 18px 48px rgba(56, 189, 248, 0.45) !important;
        }

        .orb {
            width: 28px; height: 28px; border-radius: 50%;
            background: conic-gradient(from 180deg, var(--accent-1), var(--accent-2), #2DD4BF, var(--accent-1));
            display: flex; align-items: center; justify-content: center;
            color: #050B16; font-weight: 900; font-size: 13px;
            cursor: pointer; flex-shrink: 0;
            box-shadow: 0 0 16px rgba(56, 189, 248, 0.46);
        }
        .eq { display: inline-flex; align-items: flex-end; gap: 2px; height: 11px; }
        .eq span {
            width: 2.2px; background: var(--accent-1); border-radius: 2px;
        }
        #crown.pill .eq span {
            animation: bounce 0.85s ease-in-out infinite alternate;
        }
        .eq span:nth-child(1) { height: 5px; animation-delay: 0s; }
        .eq span:nth-child(2) { height: 10px; animation-delay: 0.2s; }
        .eq span:nth-child(3) { height: 7px; animation-delay: 0.4s; }
        @keyframes bounce { to { height: 11px; opacity: 0.65; } }

        .sep { width: 1px; height: 18px; background: var(--border-subtle); flex-shrink: 0; }
        .active-win-chip {
            display: inline-flex; align-items: center; gap: 5px;
            max-width: 112px; min-width: 0; flex-shrink: 1;
            font-size: 11.5px; font-weight: 600;
            color: var(--text-1); cursor: pointer;
            padding: 3px 6px; border-radius: 8px;
        }
        .active-win-chip:hover { background: var(--bg-card); }
        .active-win-chip img { width: 16px; height: 16px; border-radius: 4px; flex-shrink: 0; }
        .active-win-chip .txt { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

        .mini-tray-strip {
            display: inline-flex; align-items: center; gap: 4px;
            padding: 2px 6px 2px 7px; height: 29px;
            border-radius: 999px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            cursor: pointer; transition: all 0.15s var(--ease);
        }
        .mini-tray-strip:hover, .mini-tray-strip.open {
            background: var(--bg-card-hover); border-color: var(--accent-1);
        }
        .mini-tray-strip img { width: 15px; height: 15px; border-radius: 3.5px; display: block; }
        .mini-tray-count { font-size: 11px; font-weight: 700; color: var(--text-2); margin-left: 2px; }

        /* Expanded Deck */
        .deck-top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
        .tabs {
            display: flex; gap: 4px; background: var(--bg-inset);
            padding: 4px; border-radius: 999px; border: 1px solid var(--border-subtle);
            overflow-x: auto;
        }
        .tab {
            display: inline-flex; align-items: center; gap: 6px;
            padding: 6px 12px; border-radius: 999px; font-size: 11.5px; font-weight: 600;
            color: var(--text-2); cursor: pointer; border: none; background: transparent;
            white-space: nowrap; transition: all 0.16s var(--ease); font-family: var(--font);
        }
        .tab:hover { color: var(--text-1); }
        .tab.active {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
        }
        .deck-body { flex: 1; min-height: 0; overflow-y: auto; overflow-x: hidden; padding-right: 3px; display: flex; flex-direction: column; }
        .bento-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.15fr); gap: 11px; flex: 1; min-height: 0; }
        .bento-col { display: flex; flex-direction: column; gap: 9px; min-height: 0; }
        .card {
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            border-radius: 17px; padding: 13px;
        }
        .card-title {
            font-size: 10.5px; font-weight: 700; text-transform: uppercase;
            letter-spacing: 0.06em; color: var(--text-2); margin-bottom: 9px;
            display: flex; align-items: center; justify-content: space-between; gap: 8px;
        }
        .card-title-l { display: inline-flex; align-items: center; gap: 6px; }
        .studio-sec {
            margin-top: 8px;
            padding-top: 7px;
            border-top: 1px solid var(--border-subtle);
        }
        .studio-sec-lbl {
            font-size: 9.5px; font-weight: 700; text-transform: uppercase;
            letter-spacing: 0.07em; color: var(--text-2);
            margin-bottom: 5px;
            display: flex; align-items: center; justify-content: space-between; gap: 6px;
        }
        .studio-chip-row {
            display: flex; flex-wrap: wrap; gap: 5px;
        }
        .studio-tip {
            font-size: 10px; font-weight: 500; text-transform: none; letter-spacing: 0;
            color: var(--accent-1); opacity: 0.88;
        }
        .toggle-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .tile {
            padding: 10px 11px; border-radius: 13px; border: 1px solid var(--border-subtle);
            background: var(--bg-card); cursor: pointer;
            display: flex; align-items: center; justify-content: space-between; gap: 8px;
            transition: all 0.15s var(--ease);
        }
        .tile:hover { background: var(--bg-card-hover); border-color: var(--border-rim); }
        .tile.on {
            background: linear-gradient(135deg, rgba(56,189,248,0.22), rgba(129,140,248,0.18));
            border-color: var(--accent-1);
        }
        .tile .t-row { display: flex; align-items: center; gap: 8px; min-width: 0; }
        .tile .t-icon {
            width: 28px; height: 28px; border-radius: 9px;
            background: rgba(255,255,255,0.07); color: var(--text-2);
            display: flex; align-items: center; justify-content: center; flex-shrink: 0;
        }
        .tile.on .t-icon { background: rgba(56,189,248,0.24); color: var(--accent-1); }
        .tile .t-name { font-size: 12px; font-weight: 700; }
        .tile .t-sub { font-size: 10.5px; color: var(--text-2); margin-top: 1px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .slider-row { display: flex; align-items: center; gap: 9px; margin-bottom: 8px; }
        .slider-row input[type=range] {
            flex: 1; accent-color: var(--accent-1); height: 5px; cursor: pointer;
        }
        .media-hero {
            display: flex; align-items: center; gap: 13px;
            background: linear-gradient(135deg, rgba(56,189,248,0.13), rgba(129,140,248,0.09));
            border: 1px solid rgba(56,189,248,0.28); border-radius: 17px; padding: 12px;
        }
        .media-art {
            width: 54px; height: 54px; border-radius: 13px; object-fit: cover;
            background: rgba(255,255,255,0.07); color: var(--accent-1); flex-shrink: 0;
            display: flex; align-items: center; justify-content: center;
        }
        textarea.scratchpad {
            width: 100%; height: 86px; resize: none;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            border-radius: 11px; padding: 9px 10px; color: var(--text-1);
            font-family: var(--font); font-size: 12px; outline: none;
            user-select: text;
        }
        textarea.scratchpad:focus { border-color: var(--accent-1); }
        .clip-item {
            display: flex; align-items: center; gap: 7px;
            padding: 7px 9px; border-radius: 10px; background: var(--bg-card);
            border: 1px solid var(--border-subtle); font-size: 11.5px;
            margin-bottom: 5px; cursor: pointer; overflow: hidden;
            text-overflow: ellipsis; white-space: nowrap;
        }
        .clip-item:hover { border-color: var(--accent-1); background: rgba(56,189,248,0.12); }

        /* Flyouts: Tray, Calendar, Dropzone, Omnibar */
        .flyout-panel {
            pointer-events: auto;
            margin-top: 8px; border-radius: 22px; padding: 15px;
            max-width: calc(100vw - 24px);
            display: none; flex-direction: column; gap: 11px;
            animation: flyIn 0.20s var(--spring);
        }
        .flyout-panel.open { display: flex; }
        @keyframes flyIn {
            from { opacity: 0; transform: translateY(-8px) scale(0.97); }
            to { opacity: 1; transform: translateY(0) scale(1); }
        }
        #trayPopup { width: min(540px, calc(100vw - 24px)); }
        #calendarPopup { width: min(400px, calc(100vw - 24px)); }
        #dropPopup { width: min(480px, calc(100vw - 24px)); }
        #omnibar {
            pointer-events: auto;
            margin-top: 8px; width: min(740px, calc(100vw - 24px));
            border-radius: 24px; padding: 15px;
            display: none; flex-direction: column; gap: 11px;
            animation: flyIn 0.18s var(--spring);
        }
        #omnibar.open { display: flex; }

        .omni-input-wrap {
            display: flex; align-items: center; gap: 10px;
            background: var(--bg-inset); border: 1.5px solid var(--accent-1);
            border-radius: 14px; padding: 9px 13px;
            box-shadow: 0 0 22px rgba(56, 189, 248, 0.16);
        }
        .omni-input-wrap input {
            flex: 1; background: transparent; border: none; outline: none;
            color: var(--text-1); font-size: 14.5px; font-family: var(--font);
            user-select: text;
        }
        .calc-banner {
            padding: 10px 14px; border-radius: 13px;
            background: linear-gradient(135deg, rgba(52,211,153,0.18), rgba(56,189,248,0.14));
            border: 1px solid var(--success);
            display: flex; align-items: center; justify-content: space-between;
            cursor: pointer;
        }
        .app-grid {
            display: grid; grid-template-columns: repeat(auto-fill, minmax(156px, 1fr));
            gap: 7px; max-height: 256px; overflow-y: auto; padding-right: 4px;
        }
        .app-card {
            position: relative;
            display: flex; align-items: center; gap: 9px;
            padding: 8px 10px; border-radius: 12px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            cursor: pointer; transition: all 0.14s var(--ease);
        }
        .app-card:hover {
            background: rgba(56,189,248,0.14);
            border-color: var(--accent-1);
            transform: translateY(-1px);
        }
        .app-card img { width: 24px; height: 24px; border-radius: 6px; flex-shrink: 0; }
        .app-card .app-name {
            font-size: 12px; font-weight: 600;
            overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1;
        }
        .app-card .pin-mini {
            opacity: 0; width: 22px; height: 22px; border-radius: 6px;
            display: inline-flex; align-items: center; justify-content: center;
            background: rgba(255,255,255,0.12); color: var(--text-1);
            transition: opacity 0.12s;
        }
        .app-card:hover .pin-mini { opacity: 1; }

        .omni-list { max-height: 256px; overflow-y: auto; display: flex; flex-direction: column; gap: 5px; }
        .omni-row {
            display: flex; align-items: center; gap: 10px;
            padding: 8px 11px; border-radius: 12px; cursor: pointer;
            background: var(--bg-card); border: 1px solid transparent;
        }
        .omni-row.sel, .omni-row:hover {
            background: rgba(56,189,248,0.16); border-color: var(--accent-1);
        }
        .omni-row img { width: 22px; height: 22px; border-radius: 6px; }

        .tray-grid {
            display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 8px;
            max-height: 270px; overflow-y: auto; padding-right: 4px;
        }
        .tray-item {
            display: flex; align-items: center; gap: 9px;
            padding: 9px 10px; border-radius: 13px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            cursor: pointer; transition: all 0.14s var(--ease);
        }
        .tray-item:hover { background: var(--bg-card-hover); border-color: var(--accent-1); }
        .tray-item img { width: 25px; height: 25px; border-radius: 6px; flex-shrink: 0; }

        .cal-weekdays, .cal-days {
            display: grid; grid-template-columns: repeat(7, 1fr); gap: 5px; text-align: center;
        }
        .cal-weekdays span { font-size: 10.5px; font-weight: 700; color: var(--text-2); padding: 4px 0; }
        .cal-day {
            position: relative; height: 34px; border-radius: 10px;
            display: flex; align-items: center; justify-content: center;
            font-size: 12.5px; font-weight: 600; cursor: pointer;
            border: 1px solid transparent; transition: all 0.13s var(--ease);
        }
        .cal-day:hover { background: var(--bg-card-hover); border-color: var(--border-rim); }
        .cal-day.muted { opacity: 0.30; }
        .cal-day.today {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); font-weight: 800;
        }
        .cal-day.selected:not(.today) { border-color: var(--accent-1); background: rgba(56,189,248,0.16); }
        .cal-day .note-dot {
            position: absolute; bottom: 3px; width: 4.5px; height: 4.5px;
            border-radius: 50%; background: var(--accent-1);
        }
        .cal-day.today .note-dot { background: #04101F; }

        .drop-zone-box {
            border: 1.5px dashed var(--accent-1);
            border-radius: 14px; padding: 14px;
            text-align: center; background: rgba(56, 189, 248, 0.06);
            cursor: pointer; transition: all 0.15s var(--ease);
        }
        .drop-zone-box:hover, .drop-zone-box.drag-active { background: rgba(56, 189, 248, 0.16); }
        .shelf-file-row {
            display: flex; align-items: center; gap: 9px;
            padding: 8px 10px; border-radius: 11px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            margin-bottom: 6px;
        }
        .shelf-file-row img { width: 22px; height: 22px; border-radius: 5px; flex-shrink: 0; }
        .power-strip {
            display: flex; align-items: center; justify-content: space-between; gap: 6px; flex-wrap: wrap;
            padding-top: 9px; border-top: 1px solid var(--border-subtle);
        }
        .power-strip .pill-btn {
            height: 26px; padding: 0 8px; font-size: 11px; gap: 4px;
        }

        /* Toast */
        #toast {
            pointer-events: auto;
            margin-top: 8px; padding: 10px 15px; border-radius: 16px;
            display: none; align-items: center; gap: 10px; max-width: 440px;
            font-size: 12px;
        }
        #toast.show { display: flex; }
        .omni-filters {
            display: flex; align-items: center; gap: 5px; flex-wrap: wrap;
        }
        .omni-chip {
            height: 24px; padding: 0 10px; border-radius: 99px;
            border: 1px solid var(--border-subtle);
            background: var(--bg-card); color: var(--text-2);
            font-family: var(--font); font-size: 11px; font-weight: 700;
            cursor: pointer; transition: all 0.14s var(--ease);
        }
        .omni-chip:hover { color: var(--text-1); border-color: var(--border-rim); }
        .omni-chip.active {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); border-color: transparent;
        }
        .omni-row-actions {
            display: inline-flex; align-items: center; gap: 5px; flex-shrink: 0;
        }
        .color-swatch {
            width: 30px; height: 30px; border-radius: 9px;
            border: 1.5px solid rgba(255,255,255,0.35);
            box-shadow: 0 4px 12px rgba(0,0,0,0.35);
            flex-shrink: 0;
        }
        .notif-card {
            display: flex; align-items: flex-start; gap: 9px;
            padding: 9px 11px; border-radius: 13px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            transition: background 0.14s, border-color 0.14s;
        }
        .notif-card:hover { background: var(--bg-card-hover); border-color: var(--border-rim); }
        .notif-app-tag {
            font-size: 10px; font-weight: 800; letter-spacing: 0.03em;
            color: var(--accent-1); text-transform: uppercase;
        }
        #onboardingCard {
            pointer-events: auto;
            margin-top: 14px;
            width: min(560px, calc(100vw - 24px));
            border-radius: 24px;
            padding: 18px 20px;
            display: none;
            flex-direction: column;
            gap: 13px;
            animation: popDown 0.22s var(--spring);
        }
        #onboardingCard.open { display: flex; }
        .onb-tips {
            display: grid; grid-template-columns: 1fr 1fr; gap: 8px;
        }
        .onb-tip {
            padding: 9px 11px; border-radius: 13px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            font-size: 11.5px; line-height: 1.35;
        }
        .onb-tip strong { display: block; font-size: 12px; color: var(--text-1); margin-bottom: 2px; }
        .onb-tip span { color: var(--text-2); }
        .audio-out-strip {
            display: flex; flex-wrap: wrap; gap: 5px;
            margin-top: 8px; padding-top: 7px;
            border-top: 1px solid var(--border-subtle);
        }
        .pill-btn.danger-arm {
            background: rgba(251,113,133,0.22) !important;
            border-color: var(--danger) !important;
            color: var(--danger) !important;
        }
        .pill-btn.awake-on {
            background: rgba(245,158,11,0.20) !important;
            border-color: rgba(245,158,11,0.65) !important;
            color: #F59E0B !important;
        }
        .app-route-sel {
            height: 22px; max-width: 96px; padding: 0 5px;
            border-radius: 7px; border: 1px solid var(--border-subtle);
            background: var(--bg-inset); color: var(--text-1);
            font-family: var(--font); font-size: 10px; font-weight: 600;
            outline: none; cursor: pointer; flex-shrink: 0;
        }
        .app-route-sel option { background: var(--bg-glass-2); color: var(--text-1); }
        .clip-search {
            width: 100%; height: 31px; padding: 0 11px; margin-bottom: 8px;
            border-radius: 10px; border: 1px solid var(--border-subtle);
            background: var(--bg-inset); color: var(--text-1);
            font-family: var(--font); font-size: 12px; outline: none;
            user-select: text; flex-shrink: 0;
        }
        .clip-search:focus { border-color: var(--accent-1); box-shadow: 0 0 0 2px rgba(56,189,248,0.16); }
        .clip-vault-list {
            display: flex; flex-direction: column; gap: 6px;
            flex: 1; min-height: 0; overflow-y: auto; overflow-x: hidden;
            padding-right: 5px;
            overscroll-behavior: contain;
            scrollbar-gutter: stable;
        }
        .clip-vault-list::-webkit-scrollbar,
        .deck-body::-webkit-scrollbar { width: 6px; }
        .clip-vault-list::-webkit-scrollbar-track,
        .deck-body::-webkit-scrollbar-track { background: transparent; }
        .clip-vault-list::-webkit-scrollbar-thumb,
        .deck-body::-webkit-scrollbar-thumb {
            background: rgba(148, 163, 184, 0.34);
            border-radius: 99px;
        }
        .clip-vault-list::-webkit-scrollbar-thumb:hover,
        .deck-body::-webkit-scrollbar-thumb:hover { background: var(--accent-1); }
        .clip-card {
            display: flex; align-items: center; gap: 9px;
            padding: 8px 10px; border-radius: 12px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            cursor: pointer; flex-shrink: 0;
            transition: border-color 0.14s, background 0.14s, transform 0.12s;
        }
        .clip-card:hover { border-color: var(--accent-1); background: var(--bg-card-hover); }
        .clip-card.pinned {
            border-color: rgba(56,189,248,0.52);
            background: linear-gradient(135deg, rgba(56,189,248,0.08), var(--bg-inset));
        }
        .clip-thumb {
            width: 58px; height: 38px; object-fit: cover; border-radius: 7px;
            border: 1px solid var(--border-rim); flex-shrink: 0; background: #000;
        }
        .clip-text-preview {
            font-size: 12px; font-weight: 600; line-height: 1.32;
            overflow: hidden; text-overflow: ellipsis;
            display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;
            word-break: break-word;
        }
        .clip-meta { font-size: 10px; color: var(--text-2); display: flex; gap: 5px; align-items: center; margin-top: 2px; }
        .clip-actions { display: inline-flex; gap: 4px; flex-shrink: 0; }
        .clip-actions .micro-btn { width: 25px; height: 25px; border-radius: 7px; }
        .clip-color-swatch {
            width: 28px; height: 28px; border-radius: 8px; flex-shrink: 0;
            border: 1.5px solid rgba(255,255,255,0.28);
            box-shadow: inset 0 1px 0 rgba(255,255,255,0.22), 0 3px 10px rgba(0,0,0,0.35);
        }

        /* v2.2 Hardware & Productivity Visual Polish */
        .priv-dot {
            width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0;
            display: inline-block;
        }
        .priv-dot.cam { background: #22C55E; box-shadow: 0 0 8px #22C55E; }
        .priv-dot.mic { background: #F97316; box-shadow: 0 0 8px #F97316; }
        .rec-dot {
            width: 8px; height: 8px; border-radius: 50%;
            background: #FB7185; box-shadow: 0 0 10px #FB7185;
            animation: recBlink 1s ease-in-out infinite alternate;
        }
        @keyframes recBlink { from { opacity: 1; } to { opacity: 0.35; } }
        #crown.droplet.rec-live {
            height: 33px; padding: 0 12px; gap: 8px;
            border-radius: 0 0 20px 20px;
            box-shadow: 0 12px 32px -6px rgba(251,113,133,0.42), inset 0 -1px 0 rgba(251,113,133,0.70);
        }
        .drop-targets-row {
            display: flex; align-items: center; gap: 8px; width: 100%;
        }
        .drop-target-slot {
            display: inline-flex; align-items: center; gap: 7px;
            height: 36px; padding: 0 14px; border-radius: 999px;
            background: var(--bg-card); border: 1px dashed var(--border-rim);
            color: var(--text-1); font-size: 11.5px; font-weight: 700;
            transition: all 0.14s var(--ease);
        }
        .drop-target-slot.active-zone {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); border-style: solid; border-color: transparent;
            transform: scale(1.04);
            box-shadow: 0 8px 20px -4px rgba(56,189,248,0.50);
        }
        #qrShareCard {
            pointer-events: auto;
            margin-top: 8px; width: min(360px, calc(100vw - 24px));
            border-radius: 22px; padding: 14px;
            display: none; flex-direction: column; gap: 10px;
            animation: flyIn 0.20s var(--spring);
        }
        #qrShareCard.open { display: flex; }
        .qr-svg-wrap {
            width: 148px; height: 148px; border-radius: 14px;
            overflow: hidden; background: #FFFFFF; padding: 4px;
            flex-shrink: 0; box-shadow: 0 10px 24px -8px rgba(0,0,0,0.55);
        }
        .qr-svg-wrap svg { width: 100%; height: 100%; display: block; }
        .bt-batt-strip {
            display: flex; flex-wrap: wrap; gap: 5px;
            margin-top: 7px; padding-top: 7px; border-top: 1px solid var(--border-subtle);
        }
        .bt-batt-chip {
            display: inline-flex; align-items: center; gap: 6px;
            padding: 4px 8px; border-radius: 10px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            font-size: 10.5px; font-weight: 700; max-width: 192px;
        }
        .bt-batt-bar {
            width: 26px; height: 5px; border-radius: 99px;
            background: rgba(148,163,184,0.22); overflow: hidden; flex-shrink: 0;
        }
        .bt-batt-fill { height: 100%; border-radius: 99px; }
        .spark-tile {
            padding: 9px 11px; border-radius: 13px; border: 1px solid var(--border-subtle);
            background: var(--bg-card); display: flex; flex-direction: column; gap: 5px;
        }
        .spark-tile.accent-tile {
            background: linear-gradient(145deg, rgba(56,189,248,0.12), rgba(129,140,248,0.08));
            border-color: rgba(56,189,248,0.34);
        }
        .spark-svg { width: 100%; height: 28px; display: block; overflow: visible; }
        .proc-row {
            display: flex; align-items: center; justify-content: space-between; gap: 8px;
            padding: 5px 8px; border-radius: 10px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            font-size: 11.5px;
        }
        </style>
        </head>
        <body>
        <div class="stage">
            <div id="crownWrap">
                <div id="crown" class="glass-shell droplet"></div>
            </div>
            <div id="osdFloat" class="glass-shell"></div>
            <div id="qrShareCard" class="glass-shell"></div>
            <div id="trayPopup" class="glass-shell flyout-panel"></div>
            <div id="calendarPopup" class="glass-shell flyout-panel"></div>
            <div id="dropPopup" class="glass-shell flyout-panel"></div>
            <div id="notifPopup" class="glass-shell flyout-panel"></div>
            <div id="onboardingCard" class="glass-shell"></div>
            <div id="omnibar" class="glass-shell">
                <div class="omni-input-wrap">
                    <span style="color:var(--accent-1);display:inline-flex;">
                        <svg class="svg-ic lg" viewBox="0 0 24 24"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
                    </span>
                    <input id="omniInput" type="text" placeholder="Uygulama, dosya, ayar, hesap (18% of 4500), birim (100 km to mi), > komut..." autocomplete="off" />
                    <span class="pill-btn" onclick="closeOmni()">ESC</span>
                </div>
                <div id="omniFilters" class="omni-filters">
                    <button class="omni-chip active" data-filter="all" onclick="setOmniFilter('all')">Tümü</button>
                    <button class="omni-chip" data-filter="app" onclick="setOmniFilter('app')">Uygulamalar</button>
                    <button class="omni-chip" data-filter="file" onclick="setOmniFilter('file')">Dosyalar</button>
                    <button class="omni-chip" data-filter="setting" onclick="setOmniFilter('setting')">Ayarlar & Sistem</button>
                    <button class="omni-chip" data-filter="window" onclick="setOmniFilter('window')">Açık Pencereler</button>
                    <span style="margin-left:auto;font-size:10.5px;color:var(--text-2);">Tab: Filtre • &gt; Komut</span>
                </div>
                <div id="calcBox" style="display:none;"></div>
                <div id="omniGridWrap"></div>
                <div id="omniResults" class="omni-list" style="display:none;"></div>
                <div class="power-strip">
                    <div style="display:flex;gap:5px;flex-wrap:wrap;">
                        <button class="pill-btn" onclick="send('showDesktop');closeOmni();" id="pwDesktop"></button>
                        <button class="pill-btn" onclick="closeOmni();send('startScreenOcr');" id="pwOcr" title="Ekrandaki herhangi bir görsel veya videodan metin kopyala (Çevrimdışı OCR)"></button>
                        <button class="pill-btn" onclick="closeOmni();send('startColorPicker');" id="pwColor" title="Ekrandan piksel rengi al (HEX / RGB / HSL)"></button>
                        <button class="pill-btn" onclick="closeOmni();send('startGifRecording');" id="pwGif" title="Seçili ekran bölgesini animasyonlu GIF olarak kaydet (10 FPS)"></button>
                        <button class="pill-btn" onclick="send('launchPath',{path:'ms-screenclip:'});closeOmni();" id="pwSnip"></button>
                        <button class="pill-btn" onclick="send('launchPath',{path:'taskmgr.exe'});closeOmni();" id="pwTask"></button>
                        <button class="pill-btn" onclick="send('openRecycleBin');closeOmni();" id="pwTrash"></button>
                        <button class="pill-btn" onclick="send('openUri',{uri:'ms-settings:'});closeOmni();" id="pwSettings"></button>
                    </div>
                    <div style="display:flex;gap:5px;flex-wrap:wrap;">
                        <button class="pill-btn" onclick="send('sleepPc');closeOmni();" id="pwSleep"></button>
                        <button class="pill-btn" onclick="send('lockPc');closeOmni();" id="pwLock"></button>
                        <button class="pill-btn" onclick="confirmPowerAction('restart')" id="pwRestart"></button>
                        <button class="pill-btn" onclick="confirmPowerAction('shutdown')" id="pwShutdown"></button>
                    </div>
                </div>
            </div>
            <div id="toast" class="glass-shell"></div>
        </div>

        <script>
        {{{SharedSvgJs}}}

        let state = null;
        let appCatalog = [];
        let themesCatalog = [];
        let searchFrecency = {};
        let omniFilter = 'all';
        let fileSearchSeq = 0;
        let fileSearchTimer = null;
        let latestFileHits = [];
        let latestFileQuery = '';
        let expanded = false;
        let crownHovered = false;
        let crownPinned = false;
        let hoverLeaveTimer = null;
        let activeTab = 'control';

        function getInstalledApps() { return appCatalog.length ? appCatalog : (state?.installedApps || []); }
        function getThemesList() { return themesCatalog.length ? themesCatalog : (state?.themes || []); }
        function notifyMetricsVisible() { send('setMetricsActive', { active: Boolean(expanded && activeTab === 'metrics') }); }
        let omniOpen = false;
        let trayOpen = false;
        let calOpen = false;
        let dropOpen = false;
        let notifOpen = false;
        let manualOnboardingOpen = false;
        let onboardingDismissedLocal = false;
        let armedPowerAction = null;
        let armedPowerTimer = null;
        let calMonthOffset = 0;
        let selectedDateKey = '';
        let isDraggingFile = false;
        let dragDropZone = 'shelf';
        let liveGifRec = null;
        let omniIdx = 0;
        let currentOmniItems = [];
        let scratchText = '';
        let clipQuery = '';
        let clipFilter = 'all';
        let lastExpandedSig = '';
        let lastDropletSig = '';
        let activeOsd = null;
        let osdHideTimer = null;
        let toastTimer = null;

        function send(action, payload = {}) {
            window.chrome.webview.postMessage({ action, ...payload });
        }

        function closeAllSubPopups() {
            omniOpen = false;
            trayOpen = false;
            calOpen = false;
            dropOpen = false;
            notifOpen = false;
            manualOnboardingOpen = false;
        }

        function isOnboardingVisible() {
            return (manualOnboardingOpen || (Boolean(state?.showOnboarding) && !onboardingDismissedLocal))
                && !expanded && !omniOpen && !trayOpen && !calOpen && !dropOpen && !notifOpen;
        }

        function isAnyPopupOpen() {
            return expanded || omniOpen || trayOpen || calOpen || dropOpen || notifOpen || isOnboardingVisible() || Boolean(state?.qrShare);
        }

        function syncFocusDemand() {
            if (isAnyPopupOpen()) send('requestInteractiveFocus');
            else send('releaseInteractiveFocus');
        }

        function reportHitRects() {
            const rects = [];
            for (const id of ['crown', 'qrShareCard', 'trayPopup', 'calendarPopup', 'dropPopup', 'notifPopup', 'onboardingCard', 'omnibar', 'toast']) {
                const el = document.getElementById(id);
                if (!el || getComputedStyle(el).display === 'none') continue;
                const r = el.getBoundingClientRect();
                if (r.width > 0 && r.height > 0) {
                    const padX = (id === 'crown' && !expanded && !crownHovered) ? 18 : 4;
                    const padB = (id === 'crown' && !expanded && !crownHovered) ? 10 : 4;
                    rects.push({ x: r.left - padX, y: Math.max(0, r.top - 4), w: r.width + padX * 2, h: r.height + padB + 4 });
                }
            }
            window.chrome.webview.postMessage({
                type: 'hitRects',
                vw: window.innerWidth,
                vh: window.innerHeight,
                rects
            });
        }

        function applyThemeVars(t) {
            if (!t) return;
            const root = document.documentElement.style;
            const isLight = t.colorMode === 'Light';
            root.setProperty('--bg-glass-1', isLight ? 'rgba(248,250,252,0.97)' : hexToRgba(t.colors.surface1, 0.965));
            root.setProperty('--bg-glass-2', isLight ? 'rgba(235,241,250,0.988)' : hexToRgba(t.colors.surface2, 0.985));
            root.setProperty('--bg-card', isLight ? 'rgba(15,23,42,0.045)' : 'rgba(255,255,255,0.05)');
            root.setProperty('--bg-card-hover', isLight ? 'rgba(15,23,42,0.095)' : 'rgba(255,255,255,0.105)');
            root.setProperty('--bg-inset', isLight ? 'rgba(15,23,42,0.06)' : 'rgba(0,0,0,0.34)');
            root.setProperty('--border-rim', isLight ? 'rgba(15,23,42,0.16)' : 'rgba(255,255,255,0.16)');
            root.setProperty('--border-subtle', isLight ? 'rgba(15,23,42,0.085)' : 'rgba(255,255,255,0.085)');
            root.setProperty('--text-1', t.colors.textPrimary);
            root.setProperty('--text-2', t.colors.textSecondary);
            root.setProperty('--accent-1', t.colors.accentPrimary);
            root.setProperty('--accent-2', t.colors.accentSecondary);
            root.setProperty('--accent-ink', isLight ? '#FFFFFF' : '#04101F');
        }

        function hexToRgba(hex, alpha) {
            const h = (hex || '#121A2F').replace('#', '');
            const bigint = parseInt(h.length === 8 ? h.slice(2) : h, 16);
            const r = (bigint >> 16) & 255, g = (bigint >> 8) & 255, b = bigint & 255;
            return `rgba(${r},${g},${b},${alpha})`;
        }

        function toggleCrown(force) {
            expanded = typeof force === 'boolean' ? force : !expanded;
            if (expanded) {
                closeAllSubPopups();
                crownHovered = true;
            }
            notifyMetricsVisible();
            renderAll();
            syncFocusDemand();
        }

        function toggleTrayPopup() {
            const next = !trayOpen;
            expanded = false;
            closeAllSubPopups();
            trayOpen = next;
            if (next) crownHovered = true;
            renderAll();
            syncFocusDemand();
        }

        function toggleCalendarPopup() {
            const next = !calOpen;
            expanded = false;
            closeAllSubPopups();
            calOpen = next;
            if (next) crownHovered = true;
            renderAll();
            syncFocusDemand();
        }

        function toggleDropPopup() {
            const next = !dropOpen;
            expanded = false;
            closeAllSubPopups();
            dropOpen = next;
            if (next) crownHovered = true;
            renderAll();
            syncFocusDemand();
        }

        function toggleNotifPopup() {
            const next = !notifOpen;
            expanded = false;
            closeAllSubPopups();
            notifOpen = next;
            if (next) {
                crownHovered = true;
                send('markNotificationsRead');
            }
            renderAll();
            syncFocusDemand();
        }

        function openOmni() {
            expanded = false;
            closeAllSubPopups();
            omniOpen = true;
            crownHovered = true;
            omniIdx = 0;
            omniFilter = 'all';
            latestFileHits = [];
            latestFileQuery = '';
            document.querySelectorAll('.omni-chip').forEach(c => {
                c.classList.toggle('active', c.getAttribute('data-filter') === 'all');
            });
            renderAll();
            syncFocusDemand();
            setTimeout(() => {
                const inp = document.getElementById('omniInput');
                if (inp) { inp.value = ''; inp.focus(); renderOmniList(''); }
            }, 30);
        }

        function closeOmni() {
            omniOpen = false;
            renderAll();
            syncFocusDemand();
        }

        function escHtml(s) {
            return String(s || '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
        }

        function computeExpandedDeckSignature() {
            if (!state) return '';
            // Deliberately excludes state.clock / state.shortDate so 1-second clock ticks NEVER
            // destroy the Expanded Deck DOM or reset scrollTop inside Kalıcı Pano Kasası!
            const core = {
                tab: activeTab,
                themeId: state.theme?.id,
                profile: state.profileName
            };
            if (activeTab === 'shelf') {
                core.clipFilter = clipFilter;
                core.clipCount = state.clipboardCount;
                core.clipAuto = Boolean(state.clipboardAutoPaste);
                core.clips = (state.clipboard || []).map(c => `${c.id}:${c.pinned ? 1 : 0}:${c.time}`);
                core.shelf = (state.dropShelf || []).map(f => f.path);
                core.vd = state.virtualDesktop?.list?.map(d => `${d.index}:${d.current ? 1 : 0}:${d.name}`);
                core.focus = state.focus;
            } else if (activeTab === 'control') {
                core.wifi = state.wifi;
                core.bt = state.bluetooth;
                core.btDevs = (state.btDevices || []).map(d => `${d.id}:${d.percent}`);
                core.monBr = (state.monitorBrightness || []).map(b => `${b.id}:${b.percent}`);
                core.gifRec = liveGifRec || state.gifRec;
                core.quiet = state.quietHours;
                core.awake = state.awake;
                core.audio = state.audio;
                core.mic = state.mic;
                core.outs = (state.audioOutputs || []).map(e => `${e.id}:${e.isDefault ? 1 : 0}`);
                core.sessions = state.audioSessions;
                core.bright = state.brightness;
                core.bat = state.battery;
                core.media = state.media ? `${state.media.title}|${state.media.isPlaying}` : null;
            } else if (activeTab === 'metrics') {
                core.metrics = state.metrics;
                core.topProcs = (state.topProcesses || []).map(p => `${p.name}:${p.memoryMb}`);
                core.weather = state.weather;
                core.notifs = (state.notifications || []).map(n => n.id);
            } else {
                core.studio = [
                    state.dockMagnification, state.nativeTaskbarHidden, state.dockMode,
                    state.dockDensity, state.dockEdge, state.dockScalePercent, state.dockOffsetPercent,
                    state.hardwareOsdEnabled, state.altDragEnabled,
                    state.showClockSeconds, state.snapZonesEnabled,
                    state.snapZonePreset, state.perMonitorFilter, state.dwmLivePreview,
                    state.sysNotifBridge, state.winKeyOmni, state.omniHotkeyPreset,
                    state.startupEnabled, state.updateStatus, state.updateAvailable, state.updateLatestVersion,
                    state.updateDownloading, state.updateDownloadPercent, state.autoCheckUpdatesEnabled,
                    state.updateDownloadUrl,
                    (state.profiles || []).map(p => `${p.id}:${p.active ? 1 : 0}`)
                ];
            }
            return JSON.stringify(core);
        }

        function getOsdIconSvg(osd) {
            if (!osd) return SVG.vol;
            if (osd.kind === 'mute') return SVG.mute;
            if (osd.kind === 'brightness') return SVG.sun;
            if (osd.kind === 'mic') return osd.active ? SVG.mic : SVG.micOff;
            if (osd.kind === 'capslock' || osd.kind === 'numlock') return SVG.capsLock;
            if (osd.kind === 'bluetooth' || osd.kind === 'bluetoothLow') return SVG.bluetooth;
            return SVG.vol;
        }

        function formatRecTime(sec) {
            const s = Math.max(0, Number(sec) || 0);
            return `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
        }

        function getBattColor(pct) {
            if (pct <= 15) return 'var(--danger)';
            if (pct <= 35) return 'var(--warning)';
            return 'var(--success)';
        }

        function renderQrShareCard() {
            const el = document.getElementById('qrShareCard');
            const qr = state?.qrShare;
            if (!el || !qr || expanded || omniOpen) {
                if (el) { el.className = 'glass-shell'; el.innerHTML = ''; }
                return;
            }
            el.className = 'glass-shell open';
            el.innerHTML = `
                <div style="display:flex;align-items:center;justify-content:space-between;gap:8px;">
                    <span class="card-title-l" style="font-size:11px;font-weight:800;color:var(--accent-1);text-transform:uppercase;letter-spacing:0.05em;">
                        ${SVG.qr}<span>Yerel Wi-Fi QR Paylaşım</span>
                    </span>
                    <button class="micro-btn" onclick="send('stopQrShare')" title="Paylaşımı Kapat">${SVG.close}</button>
                </div>
                <div style="display:flex;align-items:center;gap:12px;">
                    <div class="qr-svg-wrap">${qr.svg}</div>
                    <div style="flex:1;min-width:0;display:flex;flex-direction:column;gap:5px;">
                        <div style="font-size:13px;font-weight:800;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${escHtml(qr.name)}">${escHtml(qr.name)}</div>
                        <div style="font-size:11px;color:var(--text-2);" class="num">${escHtml(qr.size)} · Aynı Wi-Fi</div>
                        <div style="font-size:10px;color:var(--accent-1);word-break:break-all;font-family:monospace;background:var(--bg-inset);padding:4px 6px;border-radius:7px;">${escHtml(qr.url)}</div>
                        ${qr.downloads > 0 ? `<div style="font-size:10.5px;font-weight:700;color:var(--success);">✓ ${qr.downloads} kez indirildi</div>` : `<div style="font-size:10.5px;color:var(--text-2);">Telefon kamerasıyla okutun</div>`}
                    </div>
                </div>
                <div style="display:flex;gap:6px;justify-content:flex-end;">
                    <button class="pill-btn" style="height:26px;font-size:11px;" onclick="send('copyQrUrl')">${SVG.copy}<span>Linki Kopyala</span></button>
                    <button class="pill-btn accent" style="height:26px;font-size:11px;" onclick="send('stopQrShare')">Tamam</button>
                </div>
            `;
        }

        function buildOsdInnerHtml(osd) {
            const hasBar = typeof osd.percent === 'number';
            const pct = hasBar ? Math.max(0, Math.min(100, osd.percent)) : 0;
            return `
                <span class="osd-icon">${getOsdIconSvg(osd)}</span>
                <span class="osd-label">${escHtml(osd.label)}</span>
                ${hasBar
                    ? `<div class="osd-bar-track"><div class="osd-bar-fill" style="width:${pct}%;"></div></div><span class="osd-val num">${escHtml(osd.valueText)}</span>`
                    : `<span class="osd-status-pill">${escHtml(osd.valueText)}</span>`}
            `;
        }

        function getOsdToneClass(osd) {
            if (!osd) return '';
            if (osd.kind === 'brightness') return 'osd-brightness';
            if (osd.kind === 'mute' || !osd.active) return 'osd-off';
            return 'osd-' + osd.kind;
        }

        function renderAll() {
            if (!state) return;
            applyThemeVars(state.theme);

            document.getElementById('omnibar').className = omniOpen ? 'glass-shell open' : 'glass-shell';
            renderTrayPopup();
            renderCalendarPopup();
            renderDropPopup();
            renderNotifPopup();
            renderOnboardingCard();
            renderQrShareCard();

            const osdFloat = document.getElementById('osdFloat');
            const restingDroplet = !expanded && !isDraggingFile && !crownHovered && !crownPinned && !isAnyPopupOpen();
            if (osdFloat) {
                if (activeOsd && !restingDroplet && !omniOpen) {
                    osdFloat.className = 'glass-shell show ' + getOsdToneClass(activeOsd);
                    osdFloat.innerHTML = buildOsdInnerHtml(activeOsd);
                } else {
                    osdFloat.className = 'glass-shell';
                }
            }

            const crown = document.getElementById('crown');
            if (expanded) {
                lastDropletSig = '';
                const sig = computeExpandedDeckSignature();
                if (!crown.classList.contains('expanded') || sig !== lastExpandedSig) {
                    const prevClipScroll = document.getElementById('clipVaultList')?.scrollTop || 0;
                    const prevDeckScroll = crown.querySelector('.deck-body')?.scrollTop || 0;
                    lastExpandedSig = sig;
                    crown.className = 'glass-shell expanded';
                    crown.innerHTML = buildExpandedDeckHtml();
                    const sp = document.getElementById('scratchArea');
                    if (sp) {
                        sp.value = scratchText || state.scratchpad || '';
                        sp.addEventListener('input', e => {
                            scratchText = e.target.value;
                            send('saveScratchpad', { text: scratchText });
                        });
                    }
                    const cs = document.getElementById('clipSearchInput');
                    if (cs) {
                        cs.addEventListener('input', e => {
                            clipQuery = e.target.value;
                            const listEl = document.getElementById('clipVaultList');
                            if (listEl) listEl.innerHTML = buildClipboardListHtml(state.clipboard || []);
                        });
                    }
                    const newClipList = document.getElementById('clipVaultList');
                    if (newClipList && prevClipScroll > 0) newClipList.scrollTop = prevClipScroll;
                    const newDeckBody = crown.querySelector('.deck-body');
                    if (newDeckBody && prevDeckScroll > 0) newDeckBody.scrollTop = prevDeckScroll;
                }
            } else {
                lastExpandedSig = '';
                const recState = liveGifRec || state.gifRec || { recording: false, elapsed: 0 };
                if (isDraggingFile) {
                lastDropletSig = '';
                crown.className = 'glass-shell pill drag-over';
                crown.innerHTML = `
                    <div class="drop-targets-row">
                        <div class="drop-target-slot ${dragDropZone==='shelf'?'active-zone':''}" data-drop-zone="shelf">
                            ${SVG.inbox}<span>Süper Rafa Ekle</span>
                        </div>
                        <div class="drop-target-slot ${dragDropZone==='qr'?'active-zone':''}" data-drop-zone="qr">
                            ${SVG.qr}<span>QR ile Telefona Gönder</span>
                        </div>
                        <div class="drop-target-slot ${dragDropZone==='convert'?'active-zone':''}" data-drop-zone="convert">
                            ${SVG.zip}<span>Hızlı Dönüştür / ZIP</span>
                        </div>
                    </div>
                `;
                } else if (recState.recording && !crownHovered && !crownPinned && !isAnyPopupOpen()) {
                    lastDropletSig = '';
                    crown.className = 'glass-shell droplet rec-live';
                    crown.innerHTML = `
                        <span class="rec-dot"></span>
                        <span class="num" style="font-size:11.5px;font-weight:800;color:#FB7185;">REC ${formatRecTime(recState.elapsed)}</span>
                        <button class="pill-btn" style="height:21px;padding:0 9px;font-size:10px;border-color:rgba(251,113,133,0.65);color:#FB7185;" onclick="event.stopPropagation();send('stopGifRecording')">Durdur</button>
                    `;
                } else if (!crownHovered && !crownPinned && !isAnyPopupOpen()) {
                    // LIVE HARDWARE OSD MORPH INSIDE QUIET LIQUID DROPLET ("Damla")
                    if (activeOsd) {
                        lastDropletSig = '';
                        crown.className = 'glass-shell droplet osd-active ' + getOsdToneClass(activeOsd);
                        crown.innerHTML = buildOsdInnerHtml(activeOsd);
                    } else {
                        // QUIET LIQUID DROPLET ("Damla") STATE — Fast-path clock updates avoid full DOM rebuilds!
                        const media = state.media;
                        const shelfCount = (state.dropShelf || []).length;
                        const unread = state.unreadNotifCount || 0;
                        const micMuted = Boolean(state.mic?.supported && state.mic?.muted);
                        const awakeOn = Boolean(state.awake?.active);
                        const camLive = Boolean(state.privacy?.camera);
                        const micLive = Boolean(state.privacy?.mic);
                        const dSig = `${Boolean(media && media.isPlaying)}|${state.focus.running ? state.focus.label : ''}|${awakeOn ? state.awake.shortLabel : ''}|${micMuted}|${unread}|${shelfCount}|${camLive}|${micLive}`;
                        if (crown.classList.contains('droplet') && !crown.classList.contains('osd-active') && !crown.classList.contains('rec-live') && dSig === lastDropletSig) {
                            const tEl = crown.querySelector('.drop-time');
                            if (tEl && tEl.textContent !== state.clock) tEl.textContent = state.clock;
                            return;
                        }
                        lastDropletSig = dSig;
                        crown.className = 'glass-shell droplet';
                        crown.innerHTML = `
                            <span class="drop-dot"></span>
                            <span class="drop-time num">${escHtml(state.clock)}</span>
                            ${camLive ? `<span class="priv-dot cam" title="Kamera aktif: ${escHtml((state.privacy.cameraApps||[]).join(', '))}"></span>` : ''}
                            ${micLive ? `<span class="priv-dot mic" title="Mikrofon aktif: ${escHtml((state.privacy.micApps||[]).join(', '))}"></span>` : ''}
                            ${media && media.isPlaying ? `<div class="eq"><span></span><span></span><span></span></div>` : ''}
                            ${state.focus.running ? `<span class="drop-pill-tag num">${escHtml(state.focus.label)}</span>` : ''}
                            ${awakeOn ? `<span class="drop-pill-tag num" style="color:#F59E0B;background:rgba(245,158,11,0.18);display:inline-flex;align-items:center;gap:3px;">${SVG.coffee}${escHtml(state.awake.shortLabel)}</span>` : ''}
                            ${micMuted ? `<span class="drop-pill-tag" style="color:var(--danger);background:rgba(251,113,133,0.18);display:inline-flex;align-items:center;">${SVG.micOff}</span>` : ''}
                            ${unread > 0 ? `<span class="drop-pill-tag num" style="display:inline-flex;align-items:center;gap:3px;">${SVG.bell}${unread}</span>` : ''}
                            ${shelfCount > 0 ? `<span class="drop-pill-tag num">${shelfCount}</span>` : ''}
                        `;
                    }
                } else {
                lastDropletSig = '';
                // FLUID HOVER-REVEALED ACTION PILL
                crown.className = 'glass-shell pill';
                const aw = state.activeWindow;
                const media = state.media;
                const volLabel = state.audio.muted ? 'Sessiz' : `%${state.audio.volume}`;
                const trays = state.trayApps || [];
                const shelfCount = (state.dropShelf || []).length;
                const kb = state.keyboard || { code: 'TR', name: 'Türkçe Q' };

                const topTrayIcons = trays.filter(t => t.icon).slice(0, window.innerWidth < 1140 ? 2 : 3);
                const vd = state.virtualDesktop || { index: 0, count: 1, name: 'Masaüstü 1' };
                const vdLabel = vd.count > 1 ? `M${vd.index + 1}/${vd.count}` : 'M1';
                const showSnapBtns = window.innerWidth >= 1180;

                crown.innerHTML = `
                    <button class="pill-btn accent" onclick="openOmni()" title="Tüm Uygulamalar & Evrensel Arama (${escHtml(state.omniHotkeyPreset || 'Alt+Space')})">
                        ${SVG.search}<span>Ara</span>
                    </button>
                    <button id="vdCapsule" class="pill-btn num" onclick="send('switchVirtualDesktopDelta',{delta:1})" oncontextmenu="event.preventDefault();send('openTaskView');" title="${escHtml(vd.name)} • Fare Tekerleği / Tık: Masaüstü Değiştir • Sağ Tık: Görev Görünümü">
                        ${SVG.layers}<span>${escHtml(vdLabel)}</span>
                    </button>
                    <button class="pill-btn" onclick="send('cycleProfile')" title="Çalışma Profilini Değiştir">
                        ${SVG.bolt}<span>${escHtml(state.profileName)}</span>
                    </button>
                    ${aw ? `
                        <div class="sep"></div>
                        <div class="active-win-chip" onclick="toggleCrown(true)" title="${escHtml(aw.title)}">
                            ${aw.icon ? `<img src="data:image/png;base64,${aw.icon}"/>` : ''}
                            <span class="txt">${escHtml(aw.processName)}</span>
                        </div>
                        ${showSnapBtns ? `
                            <button class="micro-btn" onclick="send('arrangeActive',{mode:'left'})" title="Aktif Pencereyi Sola Yasla (%50)">${SVG.snapL}</button>
                            <button class="micro-btn" onclick="send('arrangeActive',{mode:'right'})" title="Aktif Pencereyi Sağa Yasla (%50)">${SVG.snapR}</button>
                        ` : ''}
                    ` : ''}
                    ${media ? `
                        <div class="sep"></div>
                        <button class="pill-btn" onclick="send('mediaCmd',{cmd:'playpause'})" title="${escHtml(media.title)} — ${escHtml(media.artist)}">
                            ${media.isPlaying ? '<div class="eq"><span></span><span></span><span></span></div>' : SVG.play}
                            <span style="max-width:82px;overflow:hidden;text-overflow:ellipsis;">${escHtml(media.title)}</span>
                        </button>
                    ` : ''}
                    ${state.focus.running ? `
                        <button class="pill-btn accent num" onclick="send('toggleFocus')" title="Odak Sayacı Aktif">${SVG.timer}<span>${escHtml(state.focus.label)}</span></button>
                    ` : ''}
                    ${state.awake?.active ? `
                        <button class="pill-btn awake-on num" onclick="send('cycleAwake')" title="${escHtml(state.awake.fullLabel)} • Değiştirmek için tıklayın">${SVG.coffee}<span>${escHtml(state.awake.shortLabel)}</span></button>
                    ` : ''}
                    ${recState.recording ? `
                        <button class="pill-btn num" style="border-color:rgba(251,113,133,0.65);color:#FB7185;background:rgba(251,113,133,0.14);" onclick="send('stopGifRecording')" title="GIF Ekran Kaydını Durdur ve Kaydet">
                            <span class="rec-dot"></span><span>${formatRecTime(recState.elapsed)}</span>
                        </button>
                    ` : ''}
                    ${state.privacy?.camera ? `
                        <button class="pill-btn" style="border-color:rgba(34,197,94,0.55);color:#4ADE80;background:rgba(34,197,94,0.12);" onclick="send('openUri',{uri:'ms-settings:privacy-webcam'})" title="Kamera aktif: ${escHtml((state.privacy.cameraApps||[]).join(', '))}">
                            <span class="priv-dot cam"></span><span>${escHtml((state.privacy.cameraApps||['Kamera'])[0])}</span>
                        </button>
                    ` : ''}
                    ${state.privacy?.mic ? `
                        <button class="pill-btn" style="border-color:rgba(249,115,22,0.55);color:#FB923C;background:rgba(249,115,22,0.12);" onclick="send('toggleMicMute')" title="Mikrofon aktif: ${escHtml((state.privacy.micApps||[]).join(', '))} (Susturmak için tıklayın)">
                            <span class="priv-dot mic"></span><span>${escHtml((state.privacy.micApps||['Mikrofon'])[0])}</span>
                        </button>
                    ` : ''}
                    ${(state.btDevices || []).length > 0 ? (() => {
                        const bt = state.btDevices[0];
                        return `<button class="pill-btn num" style="${bt.percent<=15?'border-color:var(--danger);color:var(--danger);':''}" onclick="activeTab='control';toggleCrown(true)" title="${escHtml(state.btDevices.map(d=>`${d.name}: %${d.percent}`).join(' · '))}">
                            ${SVG.bluetooth}<span>%${bt.percent}</span>
                        </button>`;
                    })() : ''}
                    <div class="sep"></div>
                    <button class="pill-btn ${dropOpen || shelfCount > 0 ? 'accent' : ''}" onclick="toggleDropPopup()" title="Sürükle-Bırak Dosya Rafı (${shelfCount})">
                        ${SVG.inbox}${shelfCount > 0 ? `<span class="num">${shelfCount}</span>` : ''}
                    </button>
                    <div class="mini-tray-strip ${trayOpen ? 'open' : ''}" onclick="toggleTrayPopup()" title="Sistem Tepsisi & Arka Plan Uygulamaları (${trays.length})">
                        ${topTrayIcons.map(t => `<img src="data:image/png;base64,${t.icon}"/>`).join('')}
                        ${SVG.tray}
                        <span class="mini-tray-count num">${trays.length}</span>
                    </div>
                    <button class="pill-btn" onclick="send('cycleKeyboard')" title="Klavye Düzeni: ${escHtml(kb.name)} (Değiştirmek için tıklayın)">
                        ${SVG.globe}<span>${escHtml(kb.code)}</span>
                    </button>
                    <button id="volCapsule" class="pill-btn num" onclick="toggleCrown(true)" oncontextmenu="event.preventDefault();send('toggleMute');" title="Fare Tekerleği: Sesi Aç/Kıs • Sağ Tık: Sessiz">
                        ${state.audio.muted ? SVG.mute : SVG.vol}<span>${volLabel}</span>
                    </button>
                    ${state.mic?.supported && state.mic?.muted ? `
                    <button class="micro-btn danger" style="border-color:var(--danger);color:var(--danger);background:rgba(251,113,133,0.16);" onclick="send('toggleMicMute')" title="Mikrofon Sessiz (Açmak için tıklayın)">${SVG.micOff}</button>` : ''}
                    <button class="pill-btn num ${calOpen ? 'accent' : ''}" onclick="toggleCalendarPopup()" title="Aylık Takvim ve Günlük Ajandayı Aç">
                        ${SVG.calendar}<span id="crownClockPill">${escHtml(state.clock)} • ${escHtml(state.shortDate || '')}</span>
                    </button>
                    <button class="pill-btn num ${(notifOpen || (state.unreadNotifCount || 0) > 0) ? 'accent' : ''}" style="padding:0 9px;" onclick="toggleNotifPopup()" title="Canlı Windows & Aurora Bildirimleri">
                        ${SVG.bell}${(state.unreadNotifCount || 0) > 0 ? `<span>${state.unreadNotifCount}</span>` : ((state.notifications || []).length > 0 ? `<span>${state.notifications.length}</span>` : '')}
                    </button>
                    <button class="micro-btn ${crownPinned ? 'active' : ''}" onclick="send('toggleCrownPinned')" title="${crownPinned ? 'Otomatik Damla Moduna Dön' : 'Üst Çubuğu Açık Sabitle'}">${SVG.pin}</button>
                    <button class="micro-btn" onclick="toggleCrown(true)" title="Aurora Kontrol Güvertesini Aç">${SVG.chevD}</button>
                `;

                const vc = document.getElementById('volCapsule');
                if (vc) {
                    vc.addEventListener('wheel', e => {
                        e.preventDefault();
                        send('adjustVolumeDelta', { delta: e.deltaY < 0 ? 4 : -4 });
                    }, { passive: false });
                }
                const vdc = document.getElementById('vdCapsule');
                if (vdc) {
                    vdc.addEventListener('wheel', e => {
                        e.preventDefault();
                        send('switchVirtualDesktopDelta', { delta: e.deltaY > 0 ? 1 : -1 });
                    }, { passive: false });
                }
                }
            }

            requestAnimationFrame(reportHitRects);
        }

        function buildExpandedDeckHtml() {
            const tabs = [
                ['control', SVG.sliders, 'Kontrol & Medya'],
                ['shelf', SVG.layers, 'Süper Raf & Pencere'],
                ['metrics', SVG.activity, 'Telemetri & Hava'],
                ['studio', SVG.palette, 'Stüdyo & Ayarlar']
            ];

            let body = '';
            if (activeTab === 'control') body = buildControlTab();
            else if (activeTab === 'shelf') body = buildShelfTab();
            else if (activeTab === 'metrics') body = buildMetricsTab();
            else body = buildStudioTab();

            return `
                <div class="deck-top">
                    <div style="display:flex;align-items:center;gap:9px;flex-shrink:0;">
                        <div class="orb" onclick="openOmni()" title="Tüm Uygulamalar & Arama (Alt+Space)">✦</div>
                        <div style="white-space:nowrap;">
                            <div style="font-size:13px;font-weight:800;letter-spacing:-0.01em;">Aurora</div>
                            <div style="font-size:10.5px;color:var(--text-2);">${escHtml(state.profileName)}</div>
                        </div>
                    </div>
                    <div class="tabs">
                        ${tabs.map(([id, ic, label]) => `<button class="tab ${activeTab===id?'active':''}" onclick="activeTab='${id}';notifyMetricsVisible();renderAll()">${ic}<span>${label}</span>${(id==='studio' && state.updateAvailable) ? `<span style="width:7px;height:7px;border-radius:50%;background:var(--success);box-shadow:0 0 8px var(--success);margin-left:2px;"></span>` : ''}</button>`).join('')}
                    </div>
                    <button class="pill-btn" onclick="toggleCrown(false)">${SVG.chevU}<span>Küçült</span></button>
                </div>
                <div class="deck-body">${body}</div>
            `;
        }

        function buildControlTab() {
            const m = state.media;
            const sessions = state.audioSessions || [];
            const monBrights = state.monitorBrightness || [];
            const btDevs = state.btDevices || [];
            const recState = liveGifRec || state.gifRec || { recording: false, elapsed: 0 };
            return `
                <div class="bento-grid">
                    <div style="display:flex;flex-direction:column;gap:9px;">
                        <div class="toggle-grid">
                            <div class="tile ${state.wifi.on?'on':''}" onclick="send('toggleWifi')">
                                <div class="t-row">
                                    <div class="t-icon">${SVG.wifi}</div>
                                    <div style="min-width:0;"><div class="t-name">Wi-Fi</div><div class="t-sub">${escHtml(state.wifi.status)}</div></div>
                                </div>
                                <span class="micro-btn" onclick="event.stopPropagation();send('openUri',{uri:'ms-settings:network-wifi'})">${SVG.settings}</span>
                            </div>
                            <div class="tile ${state.bluetooth.on?'on':''}" onclick="send('toggleBluetooth')">
                                <div class="t-row">
                                    <div class="t-icon">${SVG.bluetooth}</div>
                                    <div style="min-width:0;"><div class="t-name">Bluetooth</div><div class="t-sub">${btDevs.length ? `${btDevs.length} Pil Bağlı` : escHtml(state.bluetooth.status)}</div></div>
                                </div>
                                <span class="micro-btn" onclick="event.stopPropagation();send('refreshBluetoothBatteries')" title="Bluetooth cihaz pillerini tara">${SVG.refresh}</span>
                            </div>
                            <div class="tile ${state.quietHours?'on':''}" onclick="send('toggleQuietHours')">
                                <div class="t-row">
                                    <div class="t-icon">${SVG.moon}</div>
                                    <div style="min-width:0;"><div class="t-name">Odak Modu</div><div class="t-sub">${state.quietHours?'Bildirimler Sessiz':'Açık'}</div></div>
                                </div>
                            </div>
                            <div class="tile ${state.awake?.active?'on':''}" onclick="send('cycleAwake')" title="Bilgisayarın uykuya geçmesini ve ekran kararmasını tek tıkla engeller">
                                <div class="t-row">
                                    <div class="t-icon" style="${state.awake?.active ? 'color:#F59E0B;' : ''}">${SVG.coffee}</div>
                                    <div style="min-width:0;"><div class="t-name">Uyanık Tut</div><div class="t-sub">${escHtml(state.awake?.active ? state.awake.shortLabel : 'Kapalı')}</div></div>
                                </div>
                                <span class="micro-btn" onclick="event.stopPropagation();send('setAwakeMode',{mode:state.awake?.active?'off':'indefinite'})" title="${state.awake?.active?'Kapat':'Süresiz Aç'}">${state.awake?.active ? SVG.close : SVG.bolt}</span>
                            </div>
                        </div>

                        <div class="card" style="padding:11px 12px;flex:1;overflow-y:auto;">
                            <div class="card-title" style="margin-bottom:7px;">
                                <span class="card-title-l">${SVG.vol}<span>Ses, DDC/CI Parlaklık & Pil</span></span>
                                <div style="display:flex;align-items:center;gap:5px;">
                                    ${state.mic?.supported ? `
                                        <button class="pill-btn ${state.mic.muted ? 'danger-arm' : ''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="send('toggleMicMute')" title="${escHtml(state.mic.name)}">
                                            ${state.mic.muted ? SVG.micOff : SVG.mic}<span>${state.mic.muted ? 'Mik Kapalı' : 'Mik Açık'}</span>
                                        </button>` : ''}
                                    <button class="micro-btn" style="width:21px;height:21px;" onclick="send('refreshMonitorsBrightness')" title="DDC/CI harici monitör parlaklığını yeniden tara">${SVG.refresh}</button>
                                </div>
                            </div>
                            <div class="slider-row" style="margin-bottom:6px;">
                                <button class="micro-btn" onclick="send('toggleMute')">${state.audio.muted ? SVG.mute : SVG.vol}</button>
                                <input type="range" min="0" max="100" value="${state.audio.volume}" oninput="send('setVolume',{value:+this.value})" />
                                <span class="num" style="width:36px;text-align:right;font-size:11.5px;font-weight:700;">%${state.audio.volume}</span>
                            </div>
                            ${monBrights.length > 0 ? monBrights.slice(0, 3).map(mb => `
                            <div class="slider-row" style="margin-bottom:5px;gap:7px;">
                                <span class="micro-btn" title="${escHtml(mb.name)} (${mb.isInternal ? 'Dahili WMI' : 'Harici DDC/CI'})">${SVG.sun}</span>
                                <span style="width:76px;font-size:10.5px;font-weight:600;color:var(--text-2);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${escHtml(mb.name)}">${escHtml(mb.name)}</span>
                                <input type="range" min="${mb.isInternal ? 5 : 0}" max="100" value="${mb.percent}" oninput="send('setMonitorBrightness',{id:'${escHtml(mb.id)}',name:'${escHtml(mb.name)}',value:+this.value});this.nextElementSibling.textContent='%'+this.value;" />
                                <span class="num" style="width:34px;text-align:right;font-size:11px;font-weight:700;">%${mb.percent}</span>
                            </div>`).join('') : (state.brightness.supported ? `
                            <div class="slider-row" style="margin-bottom:5px;">
                                <span class="micro-btn">${SVG.sun}</span>
                                <input type="range" min="5" max="100" value="${state.brightness.value}" oninput="send('setBrightness',{value:+this.value})" />
                                <span class="num" style="width:36px;text-align:right;font-size:11.5px;font-weight:700;">%${state.brightness.value}</span>
                            </div>` : '')}
                            ${(state.audioOutputs || []).length > 1 ? `
                            <div class="audio-out-strip">
                                ${(state.audioOutputs || []).slice(0, 4).map((ep, idx) => `
                                    <button class="pill-btn ${ep.isDefault ? 'accent' : ''}" style="height:23px;padding:0 8px;font-size:10px;max-width:165px;" onclick="selectAudioOutputIdx(${idx})" title="${escHtml(ep.fullName || ep.name)}">
                                        ${SVG.headphones}<span style="overflow:hidden;text-overflow:ellipsis;">${escHtml(ep.name)}</span>
                                    </button>
                                `).join('')}
                            </div>` : ''}
                            ${btDevs.length > 0 ? `
                            <div class="bt-batt-strip">
                                ${btDevs.slice(0, 4).map(d => {
                                    const col = getBattColor(d.percent);
                                    return `
                                    <div class="bt-batt-chip" title="${escHtml(d.name)} · %${d.percent} (${escHtml(d.source)})">
                                        <span style="color:${col};display:inline-flex;">${SVG.bluetooth}</span>
                                        <span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(d.name)}</span>
                                        <div class="bt-batt-bar"><div class="bt-batt-fill" style="width:${d.percent}%;background:${col};"></div></div>
                                        <span class="num" style="color:${col};">%${d.percent}</span>
                                    </div>`;
                                }).join('')}
                            </div>` : ''}
                        </div>
                    </div>

                    <div style="display:flex;flex-direction:column;gap:9px;">
                        <div class="media-hero">
                            ${m && m.artwork ? `<img class="media-art" src="data:image/png;base64,${m.artwork}"/>` : `<div class="media-art">${SVG.music}</div>`}
                            <div style="flex:1;min-width:0;">
                                <div style="font-size:10px;color:var(--accent-1);font-weight:700;letter-spacing:0.06em;">${m ? escHtml(m.app) : 'MEDYA HAZIR'}</div>
                                <div style="font-size:13.5px;font-weight:700;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${m ? escHtml(m.title) : 'Aktif medya oynatıcı bekleniyor'}</div>
                                <div style="font-size:11.5px;color:var(--text-2);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${m ? escHtml(m.artist) : 'Spotify, YouTube veya tarayıcı başlatın'}</div>
                                ${m ? `
                                <div style="display:flex;gap:6px;margin-top:8px;">
                                    <button class="micro-btn" onclick="send('mediaCmd',{cmd:'previous'})">${SVG.prev}</button>
                                    <button class="pill-btn accent" style="height:26px;" onclick="send('mediaCmd',{cmd:'playpause'})">${m.isPlaying ? SVG.pause + '<span>Duraklat</span>' : SVG.play + '<span>Oynat</span>'}</button>
                                    <button class="micro-btn" onclick="send('mediaCmd',{cmd:'next'})">${SVG.next}</button>
                                </div>` : ''}
                            </div>
                        </div>

                        <div class="card" style="flex:1;padding:11px 12px;">
                            <div class="card-title">
                                <span class="card-title-l">${SVG.sliders}<span>Uygulama Ses Mikseri</span></span>
                                <div style="display:flex;align-items:center;gap:4px;">
                                    <button class="pill-btn" style="height:21px;padding:0 7px;font-size:10px;" onclick="expanded=false;renderAll();send('startScreenOcr');" title="Ekrandan Metin Yakala (OCR)">${SVG.scanText}<span>OCR</span></button>
                                    <button class="pill-btn" style="height:21px;padding:0 7px;font-size:10px;" onclick="expanded=false;renderAll();send('startColorPicker');" title="Ekrandan Renk Seç (HEX/RGB)">${SVG.pipette}<span>Renk</span></button>
                                    <button class="pill-btn ${recState.recording?'danger-arm':''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="expanded=false;renderAll();send('${recState.recording?'stopGifRecording':'startGifRecording'}');" title="Seçili ekran bölgesini animasyonlu GIF kaydet (10 FPS)">${SVG.record}<span>${recState.recording ? formatRecTime(recState.elapsed) : 'GIF'}</span></button>
                                    <span class="num" style="margin-left:2px;">Pil %${state.battery.percent}</span>
                                </div>
                            </div>
                            ${sessions.length === 0 ? `<div style="font-size:11.5px;color:var(--text-2);">Şu an ses çalan aktif uygulama oturumu yok.</div>` :
                              sessions.slice(0, 4).map(s => {
                                const outs = state.audioOutputs || [];
                                const showRoute = Boolean(state.perAppRoutingSupported) && outs.length > 1;
                                const curDev = (s.routedDeviceId || '').toLowerCase();
                                return `
                                <div class="slider-row" style="margin-bottom:6px;gap:6px;">
                                    <span style="width:76px;font-size:11px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${escHtml(s.name)}">${escHtml(s.name)}</span>
                                    <input type="range" min="0" max="100" value="${s.volume}" oninput="send('setAppVolume',{pid:${s.pid},value:+this.value})" />
                                    <span class="num" style="width:30px;text-align:right;font-size:10.5px;">%${s.volume}</span>
                                    ${showRoute ? `
                                        <select class="app-route-sel" onchange="routeAppAudioByIdx(${s.pid}, this.value)" title="Bu uygulamanın ses çıkış cihazını bağımsız yönlendir">
                                            <option value="-1" ${!curDev ? 'selected' : ''}>Varsayılan</option>
                                            ${outs.map((ep, idx) => `<option value="${idx}" ${curDev && (ep.id || '').toLowerCase() === curDev ? 'selected' : ''}>${escHtml(ep.name)}</option>`).join('')}
                                        </select>` : ''}
                                </div>`;
                              }).join('')}
                        </div>
                    </div>
                </div>
            `;
        }

        function buildShelfTab() {
            const clips = state.clipboard || [];
            const shelfFiles = state.dropShelf || [];
            return `
                <div class="bento-grid" style="grid-template-columns:minmax(0,0.92fr) minmax(0,1.22fr);">
                    <div class="bento-col">
                        <div class="card" style="padding:11px 12px;">
                            <div class="card-title" style="margin-bottom:7px;"><span class="card-title-l">${SVG.snapL}<span>Pencere Düzenleri & Odak</span></span><span class="num">${state.focus.running ? state.focus.label : '1 Tık'}</span></div>
                            <div style="display:flex;flex-wrap:wrap;gap:5px;margin-bottom:6px;">
                                <button class="pill-btn" style="height:24px;font-size:10.5px;padding:0 8px;" onclick="send('applyWindowLayout',{preset:'code6535'})">${SVG.snapL}<span>65/35</span></button>
                                <button class="pill-btn" style="height:24px;font-size:10.5px;padding:0 8px;" onclick="send('applyWindowLayout',{preset:'split5050'})">${SVG.snapR}<span>50/50</span></button>
                                <button class="pill-btn" style="height:24px;font-size:10.5px;padding:0 8px;" onclick="send('applyWindowLayout',{preset:'studio3'})">${SVG.layers}<span>Üçlü</span></button>
                                <button class="pill-btn" style="height:24px;font-size:10.5px;padding:0 8px;" onclick="send('applyWindowLayout',{preset:'zen75'})">${SVG.snapC}<span>Zen</span></button>
                            </div>
                            <div style="display:flex;gap:4px;align-items:center;flex-wrap:wrap;margin-bottom:6px;padding-top:5px;border-top:1px solid var(--border-subtle);">
                                ${(state.virtualDesktop?.list || []).slice(0, 3).map(d => `
                                    <button class="pill-btn ${d.current?'accent':''}" style="height:23px;font-size:10px;padding:0 8px;" onclick="send('switchVirtualDesktopIndex',{index:${d.index}})">${escHtml(d.name)}</button>
                                `).join('')}
                                <button class="pill-btn" style="height:23px;font-size:10px;padding:0 7px;" onclick="send('createVirtualDesktop')">${SVG.plus}<span>Yeni</span></button>
                            </div>
                            <div style="display:flex;gap:4px;align-items:center;flex-wrap:wrap;">
                                <button class="pill-btn ${state.focus.running?'accent':''}" style="height:23px;font-size:10.5px;padding:0 8px;" onclick="send('startFocusMinutes',{minutes:25})">${SVG.timer}<span>25 dk</span></button>
                                <button class="pill-btn" style="height:23px;font-size:10.5px;padding:0 8px;" onclick="send('startFocusMinutes',{minutes:45})">45 dk</button>
                                <button class="pill-btn" style="height:23px;font-size:10.5px;padding:0 8px;" onclick="send('startFocusMinutes',{minutes:5})">5 dk</button>
                                ${state.focus.running ? `<button class="micro-btn danger" style="width:23px;height:23px;" onclick="send('toggleFocus')">${SVG.close}</button>` : ''}
                            </div>
                        </div>

                        <div class="card" style="padding:10px 12px;">
                            <div class="card-title" style="margin-bottom:6px;">
                                <span class="card-title-l">${SVG.inbox}<span>Dosya Rafı (${shelfFiles.length})</span></span>
                                <div style="display:flex;gap:8px;align-items:center;">
                                    <span style="cursor:pointer;color:var(--accent-2);" onclick="expanded=false;renderAll();send('pickAndShareQr');" title="Dosya seç ve telefonuna QR ile aktar">QR Gönder</span>
                                    <span style="cursor:pointer;color:var(--accent-1);" onclick="send('pickShelfFile')">+ Seç</span>
                                </div>
                            </div>
                            ${shelfFiles.length === 0 ? `
                                <div class="drop-zone-box" style="padding:9px 10px;" onclick="send('pickShelfFile')">
                                    <div style="font-size:11.5px;font-weight:700;color:var(--accent-1);">Dosyaları Üst Adaya Sürükle-Bırak</div>
                                    <div style="font-size:10.5px;color:var(--text-2);margin-top:2px;">Raf · QR Telefona Gönder · Hızlı Dönüştür/ZIP</div>
                                </div>
                            ` : `
                                <div style="max-height:76px;overflow-y:auto;">
                                    ${shelfFiles.map((f, idx) => `
                                        <div class="shelf-file-row">
                                            ${f.icon ? `<img src="data:image/png;base64,${f.icon}"/>` : SVG.folder}
                                            <div style="flex:1;min-width:0;cursor:pointer;" onclick="send('openShelfFile',{index:${idx}})" title="${escHtml(f.path)}">
                                                <div style="font-size:11.5px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(f.name)}</div>
                                            </div>
                                            <button class="micro-btn" style="width:22px;height:22px;" onclick="expanded=false;renderAll();send('shareShelfFileQr',{index:${idx}})" title="QR ile Telefona Gönder">${SVG.qr}</button>
                                            <button class="micro-btn" style="width:22px;height:22px;" onclick="send('convertShelfFile',{index:${idx}})" title="Hızlı Dönüştür / ZIP Sıkıştır">${SVG.zip}</button>
                                            <button class="micro-btn" style="width:22px;height:22px;" onclick="send('revealShelfFile',{index:${idx}})" title="Klasörde Göster">${SVG.folder}</button>
                                            <button class="micro-btn danger" style="width:22px;height:22px;" onclick="send('removeShelfFile',{index:${idx}})" title="Kaldır">${SVG.close}</button>
                                        </div>
                                    `).join('')}
                                </div>
                            `}
                        </div>

                        <div class="card" style="flex:1;display:flex;flex-direction:column;min-height:0;padding:10px 12px;">
                            <div class="card-title" style="margin-bottom:6px;"><span class="card-title-l">${SVG.note}<span>Hızlı Cep Notu</span></span><span>Oto-Kayıt</span></div>
                            <textarea id="scratchArea" class="scratchpad" style="flex:1;min-height:54px;height:100%;" placeholder="Fikir, link veya görev yaz; otomatik kaydedilir..."></textarea>
                        </div>
                    </div>

                    <div class="card" style="display:flex;flex-direction:column;min-height:0;overflow:hidden;">
                        <div class="card-title" style="margin-bottom:7px;flex-wrap:wrap;">
                            <span class="card-title-l">${SVG.clip}<span>Pano Kasası (${state.clipboardCount ?? clips.length})</span></span>
                            <div style="display:flex;gap:4px;align-items:center;flex-wrap:wrap;">
                                <button class="pill-btn ${state.clipboardAutoPaste?'accent':''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="send('toggleClipboardAutoPaste')" title="Açıkken öğeye tıklamak önceki pencereye doğrudan Ctrl+V yapar">
                                    ${SVG.bolt}<span>Oto-Yapıştır</span>
                                </button>
                                <button class="omni-chip ${clipFilter==='all'?'active':''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="setClipFilter('all')">Tümü</button>
                                <button class="omni-chip ${clipFilter==='pinned'?'active':''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="setClipFilter('pinned')">Sabit</button>
                                <button class="omni-chip ${clipFilter==='text'?'active':''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="setClipFilter('text')">Metin</button>
                                <button class="omni-chip ${clipFilter==='image'?'active':''}" style="height:21px;padding:0 7px;font-size:10px;" onclick="setClipFilter('image')">Görsel</button>
                                <button class="pill-btn" style="height:21px;padding:0 6px;font-size:10px;" onclick="send('clearUnpinnedClips')" title="Sabitlenmemiş tüm öğeleri temizle">Temizle</button>
                            </div>
                        </div>
                        <input id="clipSearchInput" class="clip-search" type="text" value="${escHtml(clipQuery)}" placeholder="Pano geçmişinde ara (metin, renk, link, kaynak)..." />
                        <div id="clipVaultList" class="clip-vault-list">${buildClipboardListHtml(clips)}</div>
                    </div>
                </div>
            `;
        }

        function buildSparklineSvg(values, color, gradId, fixedMax) {
            const arr = (Array.isArray(values) && values.length >= 2) ? values : [0, 0];
            const maxV = fixedMax || Math.max(10, ...arr);
            const w = 160, h = 28, pad = 2;
            const pts = arr.map((v, i) => {
                const x = (i / Math.max(1, arr.length - 1)) * w;
                const y = h - pad - ((Math.max(0, Math.min(maxV, v)) / maxV) * (h - pad * 2));
                return `${x.toFixed(1)},${y.toFixed(1)}`;
            });
            const area = `0,${h} ${pts.join(' ')} ${w},${h}`;
            return `<svg class="spark-svg" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none">
                <defs><linearGradient id="${gradId}" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="${color}" stop-opacity="0.34"/><stop offset="100%" stop-color="${color}" stop-opacity="0.02"/></linearGradient></defs>
                <polygon points="${area}" fill="url(#${gradId})"/>
                <polyline points="${pts.join(' ')}" fill="none" stroke="${color}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>`;
        }

        function buildMetricsTab() {
            const m = state.metrics;
            const topProcs = state.topProcesses || [];
            const cpuSpark = buildSparklineSvg(m.cpuHist, 'var(--accent-1)', 'gCpu', 100);
            const ramSpark = buildSparklineSvg(m.ramHist, 'var(--accent-2)', 'gRam', 100);
            const netSpark = buildSparklineSvg(m.netHist, 'var(--success)', 'gNet', 0);
            return `
                <div class="bento-grid">
                    <div style="display:flex;flex-direction:column;gap:9px;">
                        <div class="card" style="padding:11px 12px;">
                            <div class="card-title" style="margin-bottom:7px;"><span class="card-title-l">${SVG.activity}<span>Canlı Telemetri & Eğriler</span></span><span class="num" style="font-size:10px;color:var(--accent-1);">Canlı 1.5s</span></div>
                            <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px;">
                                <div class="spark-tile accent-tile">
                                    <div style="display:flex;justify-content:space-between;align-items:baseline;">
                                        <span class="t-name">CPU</span>
                                        <span class="num" style="font-size:16px;font-weight:800;">%${m.cpu}</span>
                                    </div>
                                    ${cpuSpark}
                                </div>
                                <div class="spark-tile accent-tile">
                                    <div style="display:flex;justify-content:space-between;align-items:baseline;">
                                        <span class="t-name">RAM</span>
                                        <span class="num" style="font-size:16px;font-weight:800;">%${m.ramPct}</span>
                                    </div>
                                    ${ramSpark}
                                    <div class="t-sub num" style="margin-top:-2px;">${m.ramUsed} / ${m.ramTotal} GB</div>
                                </div>
                                <div class="spark-tile" style="grid-column:span 2;">
                                    <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;">
                                        <span class="t-name">Ağ Akışı</span>
                                        <span class="num" style="font-size:11.5px;font-weight:700;color:var(--success);">↓ ${m.rxKbps} KB/s · ↑ ${m.txKbps} KB/s</span>
                                    </div>
                                    ${netSpark}
                                </div>
                            </div>
                            <div style="margin-top:7px;font-size:10.5px;color:var(--text-2);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">GPU: <strong style="color:var(--text-1);">${escHtml(m.gpuName)}</strong></div>
                        </div>

                        <div class="card" style="padding:10px 12px;flex:1;">
                            <div class="card-title" style="margin-bottom:6px;"><span class="card-title-l">${SVG.layers}<span>En Çok RAM Kullananlar (Top 3)</span></span><span>Tek Tık Kapat</span></div>
                            <div style="display:flex;flex-direction:column;gap:5px;">
                                ${topProcs.length === 0 ? `<div style="font-size:11px;color:var(--text-2);">Süreç listesi hazırlanıyor...</div>` :
                                  topProcs.map(p => `
                                    <div class="proc-row">
                                        <div style="min-width:0;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">
                                            <strong style="font-weight:700;">${escHtml(p.displayName)}</strong>
                                            ${p.instances > 1 ? `<span style="font-size:10px;color:var(--text-2);"> (${p.instances})</span>` : ''}
                                        </div>
                                        <span class="num" style="font-weight:700;color:var(--accent-1);flex-shrink:0;">${p.memoryMb} MB</span>
                                        <button class="micro-btn danger" style="width:22px;height:22px;" onclick="send('killTopProcess',{name:'${escHtml(p.name)}',pid:${p.pid}})" title="${escHtml(p.displayName)} uygulamasını güvenli kapat">${SVG.close}</button>
                                    </div>
                                  `).join('')}
                            </div>
                        </div>
                    </div>

                    <div style="display:flex;flex-direction:column;gap:10px;">
                        <div class="card">
                            <div class="card-title"><span class="card-title-l">${SVG.cloud}<span>Hava Durumu</span></span><span>${escHtml(state.weather.city)}</span></div>
                            <div style="display:flex;align-items:center;justify-content:space-between;">
                                <div>
                                    <div class="num" style="font-size:24px;font-weight:800;">${state.weather.temp}°C</div>
                                    <div style="font-size:12px;color:var(--text-2);">${escHtml(state.weather.summary)}</div>
                                </div>
                                <button class="pill-btn" onclick="send('toggleWeatherNet')">${SVG.globe}<span>${state.weather.live ? 'Canlı Veri Açık' : 'Çevrimdışı Mod'}</span></button>
                            </div>
                        </div>

                        <div class="card" style="flex:1;overflow-y:auto;">
                            <div class="card-title"><span class="card-title-l">${SVG.bell}<span>Canlı Windows & Sistem Bildirimleri</span></span><span style="cursor:pointer;color:var(--accent-1);" onclick="send('clearNotifications')">Temizle</span></div>
                            ${(state.notifications || []).slice(0, 4).map(n => `
                                <div style="font-size:11.5px;padding:5px 0;border-bottom:1px solid var(--border-subtle);">
                                    <div style="display:flex;justify-content:space-between;gap:6px;align-items:center;">
                                        <strong style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${n.app ? `<span style="color:var(--accent-1);">${escHtml(n.app)}</span> · ` : ''}${escHtml(n.title)}</strong>
                                        ${n.time ? `<span class="num" style="font-size:10px;color:var(--text-2);flex-shrink:0;">${escHtml(n.time)}</span>` : ''}
                                    </div>
                                    ${n.body ? `<div style="color:var(--text-2);font-size:11px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;margin-top:1px;">${escHtml(n.body)}</div>` : ''}
                                </div>
                            `).join('') || `<div style="font-size:11.5px;color:var(--text-2);">Aktif bildirim yok.</div>`}
                        </div>
                    </div>
                </div>
            `;
        }

        function buildStudioTab() {
            return `
                <div class="bento-grid">
                    <div class="card">
                        <div class="card-title"><span class="card-title-l">${SVG.palette}<span>Tema Aileleri</span></span><span>Canlı Geçiş</span></div>
                        <div class="toggle-grid">
                            ${getThemesList().map(t => `
                                <div class="tile ${state.theme.id===t.id?'on':''}" onclick="send('applyTheme',{id:'${t.id}'})">
                                    <div><div class="t-name">${escHtml(t.name)}</div><div class="t-sub">${escHtml(t.mode)}</div></div>
                                    <span style="width:14px;height:14px;border-radius:50%;background:${t.accent};display:inline-block;flex-shrink:0;"></span>
                                </div>
                            `).join('')}
                        </div>

                        <div style="margin-top:11px;" class="card-title"><span class="card-title-l">${SVG.bolt}<span>Çalışma Profilleri</span></span></div>
                        <div style="display:flex;flex-wrap:wrap;gap:6px;">
                            ${(state.profiles || []).map(p => `
                                <button class="pill-btn ${p.active?'accent':''}" onclick="send('switchProfile',{id:'${p.id}'})">${escHtml(p.name)}</button>
                            `).join('')}
                        </div>
                    </div>

                    <div class="card" style="display:flex;flex-direction:column;justify-content:space-between;gap:8px;">
                        <div>
                            <div class="card-title" style="margin-bottom:6px;">
                                <span class="card-title-l">${SVG.monitor}<span>Dock & Görünüm</span></span>
                                <span class="studio-tip">Alt+Sol Tık: Taşı · Alt+Sağ Tık: Boyut</span>
                            </div>
                            <div class="slider-row" style="margin-bottom:6px;" title="Simgeleri sağa/sola kaydırmadan (0px hedef sapması) sadece yukarı/yana yükselterek vurgular">
                                <span style="font-size:11.5px;width:112px;color:var(--text-1);font-weight:600;">Simge Hover</span>
                                <input type="range" min="0" max="80" value="${state.dockMagnification}" oninput="send('setMagnification',{value:+this.value})" />
                                <span class="num" style="width:34px;text-align:right;font-size:11px;color:var(--text-2);">%${state.dockMagnification}</span>
                            </div>
                            <div class="studio-chip-row">
                                <button class="pill-btn ${(state.dockEdge && state.dockEdge!=='Bottom') || (state.dockOffsetPercent && state.dockOffsetPercent!==0)?'accent':''}" style="height:24px;font-size:10.5px;" onclick="cycleDockEdge()" title="Dock kenar konumunu değiştir (veya Dock üzerinde Alt+Sol Tık ile sürükleyip Alt/Sol/Sağ kenara bırak)">
                                    ${SVG.snapL}<span>Konum: ${state.dockEdge==='Left' ? 'Sol' : (state.dockEdge==='Right' ? 'Sağ' : 'Alt')}</span>
                                </button>
                                <button class="pill-btn ${(state.dockDensity!=='Standard' || (state.dockScalePercent && state.dockScalePercent!==100))?'accent':''}" style="height:24px;font-size:10.5px;" onclick="cycleDockDensity()" title="Dock yoğunluğunu değiştir (Alt+Sağ Tık Sürükle ile canlı %75–%135 ölçeklenebilir)">
                                    ${SVG.sliders}<span>Boyut: ${(state.dockScalePercent && state.dockScalePercent!==100) ? ('%' + state.dockScalePercent) : (state.dockDensity==='Compact' ? 'Kompakt' : (state.dockDensity==='Cozy' ? 'Geniş' : 'Standart'))}</span>
                                </button>
                                <button class="pill-btn ${state.dockMode!=='AlwaysVisible'?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('cycleDockVisibilityMode')">
                                    ${SVG.chevD}<span>Dock: ${state.dockMode==='SmartDodge' ? 'Akıllı Kaçınma' : (state.dockMode==='AutoHide' ? 'Oto-Gizle' : 'Sabit')}</span>
                                </button>
                                <button class="pill-btn ${state.nativeTaskbarHidden?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleHideNativeTaskbar')" title="Aurora açıkken Windows görev çubuğunu tamamen kapatır; kapanınca otomatik geri açar">
                                    ${SVG.desktop}<span>Win Barı: ${state.nativeTaskbarHidden ? 'Kapalı' : 'Açık'}</span>
                                </button>
                                <button class="pill-btn ${state.perMonitorFilter?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('togglePerMonitorFilter')">
                                    ${SVG.monitor}<span>Ekran: ${state.perMonitorFilter ? 'Bu Ekran' : 'Tümü'} (${state.monitorCount || 1})</span>
                                </button>
                                <button class="pill-btn ${state.dwmLivePreview!==false?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleDwmLivePreview')">
                                    ${SVG.layers}<span>DWM 60 FPS</span>
                                </button>
                                <button class="pill-btn ${state.showClockSeconds?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleClockSeconds')">
                                    ${SVG.timer}<span>Saniye: ${state.showClockSeconds ? 'Açık' : 'Kapalı'}</span>
                                </button>
                                ${((state.dockScalePercent && state.dockScalePercent!==100) || (state.dockOffsetPercent && state.dockOffsetPercent!==0)) ? `
                                <button class="pill-btn" style="height:24px;font-size:10.5px;" onclick="send('resetDockTransform')" title="Dock konumunu ortala ve boyutunu %100'e sıfırla">
                                    <span>Dock Sıfırla</span>
                                </button>` : ''}
                            </div>

                            <div class="studio-sec">
                                <div class="studio-sec-lbl"><span>Pencere, OSD & Kısayollar</span></div>
                                <div class="studio-chip-row">
                                    <button class="pill-btn ${state.altDragEnabled!==false?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleAltDrag')" title="Pencerelerde ve Dock'ta: Alt+Sol Tık ile taşı/kenara yasla, Alt+Sağ Tık ile boyutlandır">
                                        ${SVG.moveWin}<span>Alt+Sürükle: ${state.altDragEnabled!==false ? 'Açık' : 'Kapalı'}</span>
                                    </button>
                                    <button class="pill-btn ${state.snapZonesEnabled!==false?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleSnapZones')" title="Pencere sürüklerken Shift tuşuna basınca cam bölgeler açılır">
                                        ${SVG.grid}<span>Shift+Bölge: ${state.snapZonesEnabled!==false ? 'Açık' : 'Kapalı'}</span>
                                    </button>
                                    <button class="pill-btn" style="height:24px;font-size:10.5px;" onclick="cycleSnapZonePreset()" title="Shift+Sürükle bölge yerleşim şablonunu değiştir">
                                        ${SVG.snapL}<span>Bölge: ${snapPresetLabel(state.snapZonePreset)}</span>
                                    </button>
                                    <button class="pill-btn ${state.hardwareOsdEnabled!==false?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleHardwareOsd')" title="Ses, Parlaklık, Mikrofon, CapsLock ve NumLock değişimlerini Üst Damla'da canlı göster (Windows ses kutusunu gizler)">
                                        ${SVG.vol}<span>Canlı OSD: ${state.hardwareOsdEnabled!==false ? 'Açık' : 'Kapalı'}</span>
                                    </button>
                                    <button class="pill-btn ${state.sysNotifBridge!==false?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleSysNotifBridge')">
                                        ${SVG.bell}<span>Bildirimler</span>
                                    </button>
                                    <button class="pill-btn ${state.winKeyOmni?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleWinKeyOmni')">
                                        ${SVG.search}<span>Win Tuşu</span>
                                    </button>
                                    <button class="pill-btn" style="height:24px;font-size:10.5px;" onclick="send('cycleOmniHotkey')">
                                        ${SVG.bolt}<span>${escHtml(state.omniHotkeyPreset || 'Alt+Space')}</span>
                                    </button>
                                </div>
                            </div>

                            <div class="studio-sec">
                                <div class="studio-sec-lbl"><span>Sistem & Yedekleme</span></div>
                                <div class="studio-chip-row">
                                    <button class="pill-btn ${state.startupEnabled?'accent':''}" style="height:24px;font-size:10.5px;" onclick="send('toggleStartup')">
                                        ${SVG.bolt}<span>Oto-Başlat</span>
                                    </button>
                                    <button class="pill-btn" style="height:24px;font-size:10.5px;" onclick="send('exportBackup')">
                                        ${SVG.exportPkg}<span>Yedekle</span>
                                    </button>
                                    <button class="pill-btn" style="height:24px;font-size:10.5px;" onclick="send('importBackup')">
                                        ${SVG.inbox}<span>Geri Yükle</span>
                                    </button>
                                    <button class="pill-btn" style="height:24px;font-size:10.5px;" onclick="openOnboardingTour()">
                                        ${SVG.sparkles}<span>Hızlı Tur</span>
                                    </button>
                                </div>
                            </div>
                        </div>

                        ${state.updateAvailable ? `
                        <div style="display:flex;align-items:center;justify-content:space-between;gap:8px;padding:7px 10px;border-radius:12px;background:linear-gradient(135deg,rgba(52,211,153,0.16),rgba(56,189,248,0.12));border:1px solid rgba(52,211,153,0.45);margin-bottom:7px;">
                            <div style="min-width:0;flex:1;">
                                <div style="font-size:11.5px;font-weight:800;color:var(--text-1);display:flex;align-items:center;gap:6px;">
                                    <span style="color:var(--success);">✦</span>
                                    <span>Yeni Sürüm Hazır: v${escHtml(state.updateLatestVersion || '')}</span>
                                </div>
                                ${state.updateDownloading ? `
                                <div style="margin-top:4px;height:5px;border-radius:99px;background:rgba(255,255,255,0.12);overflow:hidden;">
                                    <div style="height:100%;width:${Math.max(4, Math.min(100, state.updateDownloadPercent || 0))}%;background:linear-gradient(90deg,var(--success),var(--accent-1));transition:width 0.18s ease;"></div>
                                </div>` : `
                                <div style="font-size:10px;color:var(--text-2);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">Ayarlarınız korunarak GitHub üzerinden tek tıkla güncellenir</div>`}
                            </div>
                            <button class="pill-btn accent" style="height:26px;font-size:10.5px;flex-shrink:0;" ${state.updateDownloading ? 'disabled' : ''} onclick="send('installAvailableUpdate')">
                                ${SVG.refresh}<span>${state.updateDownloading ? escHtml(state.updateStatus || 'İndiriliyor...') : 'İndir ve Güncelle'}</span>
                            </button>
                        </div>` : ''}

                        <div style="display:flex;justify-content:space-between;align-items:center;padding-top:8px;border-top:1px solid var(--border-subtle);gap:5px;flex-wrap:nowrap;">
                            <div style="display:flex;align-items:center;gap:5px;min-width:0;flex-wrap:nowrap;overflow:hidden;">
                                <button class="pill-btn" style="height:24px;padding:0 9px;font-size:10.5px;" ${state.updateDownloading ? 'disabled' : ''} onclick="send('checkForUpdates')" title="GitHub Releases üzerinden yeni sürümü kontrol et">
                                    ${SVG.refresh}<span>Kontrol Et · ${escHtml(state.updateStatus || ('v' + (state.version || '2.0.0')))}</span>
                                </button>
                                <button class="pill-btn ${state.autoCheckUpdatesEnabled!==false?'accent':''}" style="height:24px;padding:0 8px;font-size:10px;" onclick="send('toggleAutoCheckUpdates')" title="Yeni sürüm çıktığında Üst Damla'da otomatik bildirim göster">
                                    <span>Oto: ${state.autoCheckUpdatesEnabled!==false ? 'Açık' : 'Kapalı'}</span>
                                </button>
                                ${state.repoUrl ? `<button class="pill-btn subtle" style="height:24px;font-size:10px;padding:0 7px;" onclick="send('openUri',{uri:state.repoUrl})" title="GitHub deposunu aç"><span>GitHub</span></button>` : ''}
                            </div>
                            <button class="pill-btn" style="border-color:var(--danger);color:var(--danger);height:24px;padding:0 9px;font-size:10.5px;flex-shrink:0;" onclick="send('exitAurora')">${SVG.power}<span>Çıkış</span></button>
                        </div>
                    </div>
                </div>
            `;
        }

        function normTr(s) {
            return String(s || '')
                .replace(/[İIı]/g, 'i')
                .replace(/[Şş]/g, 's')
                .replace(/[Ğğ]/g, 'g')
                .replace(/[Üü]/g, 'u')
                .replace(/[Öö]/g, 'o')
                .replace(/[Çç]/g, 'c')
                .toLowerCase();
        }

        function setOmniFilter(f) {
            omniFilter = f || 'all';
            omniIdx = 0;
            document.querySelectorAll('.omni-chip').forEach(c => {
                c.classList.toggle('active', c.getAttribute('data-filter') === omniFilter);
            });
            const inp = document.getElementById('omniInput');
            renderOmniList(inp ? inp.value : '');
        }

        function tryEvalMath(expr) {
            let cleaned = (expr || '').trim().replace(/,/g, '.');
            const pctMatch = cleaned.match(/^(\d+(?:\.\d+)?)\s*%\s*(?:of|x|\*)\s*(\d+(?:\.\d+)?)$/i);
            if (pctMatch) {
                return Math.round((parseFloat(pctMatch[1]) / 100.0) * parseFloat(pctMatch[2]) * 10000) / 10000;
            }
            const safePow = cleaned.replace(/\^/g, '**');
            if (!/^[0-9+\-*/().\s%]+$/.test(cleaned) && !/^[0-9+\-*/().\s^]+$/.test(cleaned)) return null;
            if (!/[+\-*/%^]/.test(cleaned)) return null;
            try {
                const val = Function(`"use strict"; return (${safePow});`)();
                return (typeof val === 'number' && isFinite(val)) ? Math.round(val * 10000) / 10000 : null;
            } catch { return null; }
        }

        function tryConvertUnit(raw) {
            const m = (raw || '').trim().replace(/,/g, '.').match(/^(-?\d+(?:\.\d+)?)\s*([a-zA-ZçğıöşüÇĞİÖŞÜ°]+)\s*(?:to|in|->|=|>)\s*([a-zA-ZçğıöşüÇĞİÖŞÜ°]+)$/i);
            if (!m) return null;
            const val = parseFloat(m[1]);
            const from = normTr(m[2].replace('°', ''));
            const to = normTr(m[3].replace('°', ''));
            if (!isFinite(val)) return null;

            const linearGroups = [
                { km: 1000, m: 1, cm: 0.01, mm: 0.001, mi: 1609.344, mil: 1609.344, ft: 0.3048, in: 0.0254, inc: 0.0254 },
                { kg: 1000, g: 1, gr: 1, mg: 0.001, lb: 453.59237, lbs: 453.59237, oz: 28.3495 },
                { b: 1, byte: 1, kb: 1024, mb: 1048576, gb: 1073741824, tb: 1099511627776 },
                { kmh: 1, mph: 1.609344, ms: 3.6 },
                { sn: 1, s: 1, sec: 1, dk: 60, min: 60, saat: 3600, h: 3600, hr: 3600, gun: 86400, d: 86400 }
            ];
            for (const grp of linearGroups) {
                if (grp[from] !== undefined && grp[to] !== undefined) {
                    const out = Math.round(((val * grp[from]) / grp[to]) * 10000) / 10000;
                    return { label: `${m[1]} ${m[2]}`, value: `${out} ${m[3]}`, copyText: String(out) };
                }
            }
            // Temperature conversions
            const tUnits = { c: 'c', f: 'f', k: 'k' };
            if (tUnits[from] && tUnits[to]) {
                let cVal = from === 'c' ? val : (from === 'f' ? (val - 32) * 5 / 9 : val - 273.15);
                let out = to === 'c' ? cVal : (to === 'f' ? cVal * 9 / 5 + 32 : cVal + 273.15);
                out = Math.round(out * 100) / 100;
                return { label: `${m[1]}°${from.toUpperCase()}`, value: `${out}°${to.toUpperCase()}`, copyText: String(out) };
            }
            return null;
        }

        function tryParseColor(raw) {
            const s = (raw || '').trim();
            const hexM = s.match(/^#?([0-9a-f]{6})$/i);
            if (hexM && s.startsWith('#')) {
                const hex = '#' + hexM[1].toUpperCase();
                const n = parseInt(hexM[1], 16);
                const rgb = `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`;
                return { css: hex, primary: hex, secondary: rgb };
            }
            const rgbM = s.match(/^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$/i);
            if (rgbM) {
                const r = Math.min(255, +rgbM[1]), g = Math.min(255, +rgbM[2]), b = Math.min(255, +rgbM[3]);
                const hex = '#' + [r, g, b].map(x => x.toString(16).padStart(2, '0')).join('').toUpperCase();
                return { css: hex, primary: hex, secondary: `rgb(${r}, ${g}, ${b})` };
            }
            return null;
        }

        function scoreSearchCandidate(normTitle, normSub, normKw, qNorm, tokens, frecencyBoost) {
            if (!qNorm) return frecencyBoost;
            let base = 0;
            if (normTitle === qNorm) base = 1000;
            else if (normTitle.startsWith(qNorm)) base = 780;
            else {
                const words = normTitle.split(/[\s\-_./\\()]+/).filter(Boolean);
                const acronym = words.map(w => w[0]).join('');
                if (words.some(w => w.startsWith(qNorm))) base = 650;
                else if (qNorm.length >= 2 && acronym.startsWith(qNorm)) base = 590;
                else if (normTitle.includes(qNorm)) base = 440;
                else if (normKw && normKw.includes(qNorm)) base = 390;
                else if (normSub && normSub.includes(qNorm)) base = 310;
                else if (tokens.length > 1 && tokens.every(t => normTitle.includes(t) || normKw.includes(t) || normSub.includes(t))) base = 370;
                else if (qNorm.length >= 3) {
                    // Ordered fuzzy subsequence check
                    let ti = 0;
                    for (let i = 0; i < normTitle.length && ti < qNorm.length; i++) {
                        if (normTitle[i] === qNorm[ti]) ti++;
                    }
                    if (ti === qNorm.length) base = 210;
                }
            }
            if (base === 0) return 0;
            return base + Math.min(280, frecencyBoost * 35);
        }

        function scheduleLocalFileSearch(rawQ) {
            clearTimeout(fileSearchTimer);
            const trimmed = (rawQ || '').trim();
            if (trimmed.length < 2 || trimmed.startsWith('>')) {
                latestFileHits = [];
                latestFileQuery = '';
                return;
            }
            if (trimmed === latestFileQuery) return;
            fileSearchTimer = setTimeout(() => {
                fileSearchSeq++;
                latestFileQuery = trimmed;
                send('searchLocalFiles', { query: trimmed, seq: fileSearchSeq });
            }, 70);
        }

        function renderOmniList(query, skipFileDispatch = false) {
            if (!state) return;
            const rawQ = (query || '').trim();
            const qNorm = normTr(rawQ);
            const tokens = qNorm.split(/\s+/).filter(Boolean);
            const calcBox = document.getElementById('calcBox');
            const gridWrap = document.getElementById('omniGridWrap');
            const listWrap = document.getElementById('omniResults');

            // 1. Instant Zero-Click Power Banners (Calculator / Unit Converter / Color Swatch)
            const unitVal = tryConvertUnit(rawQ);
            const colorVal = !unitVal ? tryParseColor(rawQ) : null;
            const mathVal = (!unitVal && !colorVal) ? tryEvalMath(rawQ) : null;

            if (unitVal) {
                calcBox.style.display = 'block';
                calcBox.innerHTML = `<div class="calc-banner" onclick="send('copyText',{text:'${escHtml(unitVal.copyText)}'});closeOmni();">
                    <div><div style="font-size:10.5px;color:var(--success);font-weight:700;letter-spacing:0.05em;">ANLIK BİRİM ÇEVİRİCİ • ${escHtml(unitVal.label)}</div><div class="num" style="font-size:21px;font-weight:800;">= ${escHtml(unitVal.value)}</div></div>
                    <span class="pill-btn accent">${SVG.copy}<span>Kopyala</span></span>
                </div>`;
            } else if (colorVal) {
                calcBox.style.display = 'block';
                calcBox.innerHTML = `<div class="calc-banner" onclick="send('copyText',{text:'${escHtml(colorVal.primary)}'});closeOmni();">
                    <div style="display:flex;align-items:center;gap:11px;">
                        <span class="color-swatch" style="background:${escHtml(colorVal.css)};"></span>
                        <div><div style="font-size:10.5px;color:var(--accent-1);font-weight:700;letter-spacing:0.05em;">RENK ÖNİZLEME</div><div class="num" style="font-size:15px;font-weight:800;">${escHtml(colorVal.primary)} <span style="font-size:12px;font-weight:600;color:var(--text-2);">• ${escHtml(colorVal.secondary)}</span></div></div>
                    </div>
                    <span class="pill-btn accent">${SVG.copy}<span>HEX Kopyala</span></span>
                </div>`;
            } else if (mathVal !== null) {
                calcBox.style.display = 'block';
                calcBox.innerHTML = `<div class="calc-banner" onclick="send('copyText',{text:'${mathVal}'});closeOmni();">
                    <div><div style="font-size:10.5px;color:var(--success);font-weight:700;letter-spacing:0.05em;">ANLIK HESAPLAYICI</div><div class="num" style="font-size:22px;font-weight:800;">= ${mathVal}</div></div>
                    <span class="pill-btn accent">${SVG.copy}<span>Panoya Kopyala</span></span>
                </div>`;
            } else {
                calcBox.style.display = 'none';
            }

            if (!qNorm && omniFilter === 'all') {
                listWrap.style.display = 'none';
                gridWrap.style.display = 'block';
                const allInstalled = getInstalledApps().filter(a => a.kind !== 'setting');
                const sorted = [...allInstalled].sort((a, b) => (searchFrecency[b.path] || 0) - (searchFrecency[a.path] || 0));
                const apps = sorted.slice(0, 48);
                gridWrap.innerHTML = `
                    <div class="card-title" style="margin-bottom:8px;">
                        <span>Tüm Yüklü Uygulamalar (${allInstalled.length} • Store + Win32)</span>
                        <span style="font-weight:500;">Sık kullanılanlar üstte • Sabitlemek için üzerine gelin</span>
                    </div>
                    <div class="app-grid">
                        ${apps.map((a, i) => `
                            <div class="app-card" onclick="execOmni(${i})">
                                ${a.icon ? `<img src="data:image/png;base64,${a.icon}"/>` : SVG.layers}
                                <span class="app-name" title="${escHtml(a.name)}">${escHtml(a.name)}</span>
                                <span class="pin-mini" onclick="event.stopPropagation();pinAppByPath(${i});" title="Dock'a Sabitle">${SVG.pin}</span>
                            </div>
                        `).join('')}
                    </div>
                `;
                currentOmniItems = apps.map(a => ({
                    name: a.name,
                    path: a.path,
                    run: () => send('launchPath', { path: a.path })
                }));
                requestAnimationFrame(reportHitRects);
                return;
            }

            if (!skipFileDispatch && qNorm.length >= 2) {
                scheduleLocalFileSearch(rawQ);
            }

            gridWrap.style.display = 'none';
            listWrap.style.display = 'flex';

            const scored = [];

            // A. Direct Terminal Command Mode ("> ipconfig", "> ping 8.8.8.8")
            if (rawQ.startsWith('>') && rawQ.length > 1) {
                const cmdText = rawQ.slice(1).trim();
                scored.push({
                    score: 2500,
                    badge: 'PowerShell',
                    title: `Komutu Çalıştır: ${cmdText}`,
                    sub: 'Yeni PowerShell penceresinde yürütülür',
                    icon: null,
                    svgIcon: SVG.bolt,
                    run: () => send('launchPath', { path: 'cmdrun:' + cmdText })
                });
            }

            // B. Direct URL / Domain Launcher ("github.com", "localhost:3000", "https://...")
            if (/^(https?:\/\/[^\s]+|localhost:\d+[^\s]*|[a-z0-9-]+(\.[a-z0-9-]+)+\/?[^\s]*)$/i.test(rawQ) && !rawQ.includes('\\')) {
                const url = /^https?:\/\//i.test(rawQ) ? rawQ : ('https://' + rawQ);
                scored.push({
                    score: 1800,
                    badge: 'Web Adresi',
                    title: `${rawQ} Adresini Aç`,
                    sub: url,
                    icon: null,
                    svgIcon: SVG.globe,
                    run: () => send('openUri', { uri: url })
                });
            }

            // C. Installed Apps + Windows 11 Settings + System Power Tools
            for (const a of getInstalledApps()) {
                const isSettingOrTool = (a.kind === 'setting' || a.kind === 'tool');
                if (omniFilter === 'app' && isSettingOrTool) continue;
                if (omniFilter === 'setting' && !isSettingOrTool) continue;
                if (omniFilter === 'file' || omniFilter === 'window') continue;

                const frec = searchFrecency[a.path] || 0;
                const sc = qNorm
                    ? scoreSearchCandidate(normTr(a.name), normTr(a.category), normTr(a.keywords || ''), qNorm, tokens, frec)
                    : (500 + frec * 35);
                if (sc <= 0) continue;
                const kindBoost = a.kind === 'app' ? 45 : (a.kind === 'setting' ? 25 : 20);
                scored.push({
                    score: sc + kindBoost,
                    badge: a.category || (isSettingOrTool ? 'Sistem' : 'Uygulama'),
                    title: a.name,
                    sub: a.kind === 'setting' ? 'Windows 11 Ayar Sayfası' : (a.kind === 'tool' ? 'Windows Sistem Aracı' : 'Uygulamayı Başlat'),
                    icon: a.icon,
                    svgIcon: a.kind === 'setting' ? SVG.settings : SVG.layers,
                    canPin: a.kind === 'app',
                    pinName: a.name,
                    pinPath: a.path,
                    run: () => send('launchPath', { path: a.path })
                });
            }

            // D. Live Open Windows (instant focus!)
            if (omniFilter === 'all' || omniFilter === 'window') {
                for (const w of (state.openWindows || [])) {
                    const sc = qNorm
                        ? scoreSearchCandidate(normTr(w.title), normTr(w.processName), 'acik pencere', qNorm, tokens, 1)
                        : 600;
                    if (sc <= 0) continue;
                    scored.push({
                        score: sc + 55,
                        badge: 'Açık Pencere',
                        title: w.title,
                        sub: `${w.processName} • Öne Getir`,
                        icon: w.icon,
                        svgIcon: SVG.desktop,
                        run: () => send('focusWindow', { hwnd: w.hwnd })
                    });
                }
            }

            // E. Real Local Files & Folders (Desktop, Documents, Downloads, Recent, Direct Paths!)
            if ((omniFilter === 'all' || omniFilter === 'file') && latestFileHits.length > 0 && latestFileQuery === rawQ) {
                for (const f of latestFileHits) {
                    const frec = searchFrecency[f.path] || 0;
                    scored.push({
                        score: (f.score || 400) + Math.min(200, frec * 30),
                        badge: f.badge || (f.isDir ? 'Klasör' : 'Dosya'),
                        title: f.name,
                        sub: f.subtitle || f.path,
                        icon: f.icon,
                        svgIcon: f.isDir ? SVG.folder : SVG.note,
                        revealPath: f.path,
                        run: () => send('launchPath', { path: f.path })
                    });
                }
            }

            // F. System Tray Background Apps
            if (omniFilter === 'all' || omniFilter === 'app') {
                for (const t of (state.trayApps || [])) {
                    if (!qNorm) continue;
                    const sc = scoreSearchCandidate(normTr(t.name), normTr(t.subtitle || ''), 'tepsi arka plan', qNorm, tokens, 0);
                    if (sc <= 0) continue;
                    scored.push({
                        score: sc + 10,
                        badge: 'Sistem Tepsisi',
                        title: t.name,
                        sub: t.subtitle || `${t.memoryMb} MB arka plan`,
                        icon: t.icon,
                        svgIcon: SVG.tray,
                        run: () => send('openTrayAppByKey', { key: t.key })
                    });
                }
            }

            // G. Built-In Aurora Quick Commands
            if (omniFilter === 'all' || omniFilter === 'setting') {
                const quickCmds = [
                    { badge: 'Düzen', title: 'Kodlama Düzeni Uygula (Sol %65 + Sağ %35)', sub: '2 pencereyi akıllı yerleştir', kw: 'kodlama duzen tiling layout 65 35', run: () => send('applyWindowLayout', { preset: 'code6535' }) },
                    { badge: 'Düzen', title: 'Eşit İkili Düzen Uygula (50 / 50)', sub: '2 pencereyi yan yana yasla', kw: 'ikili duzen split 50 50 tiling', run: () => send('applyWindowLayout', { preset: 'split5050' }) },
                    { badge: 'Düzen', title: 'Üçlü Stüdyo Düzeni (50 / 25 / 25)', sub: '3 pencereyi tek tıkla yerleştir', kw: 'uclu studyo duzen 3 pencere tiling', run: () => send('applyWindowLayout', { preset: 'studio3' }) },
                    { badge: 'Masaüstü', title: 'Yeni Sanal Masaüstü Oluştur', sub: 'Ctrl+Win+D', kw: 'yeni sanal masaustu virtual desktop', run: () => send('createVirtualDesktop') },
                    { badge: 'Masaüstü', title: 'Sonraki Sanal Masaüstüne Geç', sub: 'Sanal masaüstü geçişi', kw: 'sonraki masaustu desktop gec', run: () => send('switchVirtualDesktopDelta', { delta: 1 }) },
                    { badge: 'Pencere', title: 'Aktif Pencereyi Sol Yarıya Yasla (%50)', sub: 'Snap Left', kw: 'sol yasla snap left', run: () => send('arrangeActive', { mode: 'left' }) },
                    { badge: 'Pencere', title: 'Aktif Pencereyi Sağ Yarıya Yasla (%50)', sub: 'Snap Right', kw: 'sag yasla snap right', run: () => send('arrangeActive', { mode: 'right' }) },
                    { badge: 'Pencere', title: 'Aktif Pencereyi Zen Modunda Ortala (%75)', sub: 'Snap Center', kw: 'zen ortala center', run: () => send('arrangeActive', { mode: 'center' }) },
                    { badge: 'Üretkenlik', title: '25 Dakika Odak (Pomodoro) Başlat', sub: 'Üst Damla Adada canlı sayaç', kw: 'pomodoro odak timer sayac', run: () => send('startFocusMinutes', { minutes: 25 }) },
                    { badge: 'Araçlar', title: 'Ekrandan Metin Yakala (Çevrimdışı OCR)', sub: 'Görsel veya videodan metin kopyala', kw: 'ocr metin yakala tara resim yazi kopyala text extractor', run: () => send('startScreenOcr') },
                    { badge: 'Araçlar', title: 'Ekrandan Renk Seç (Renk Damlalığı HEX / RGB)', sub: 'Canlı büyüteçle piksel rengi al', kw: 'renk sec damlalik color picker eyedropper hex rgb hsl', run: () => send('startColorPicker') },
                    { badge: 'Güç', title: 'Uyanık Tut (1 Saat — Ekran Kararmaz)', sub: 'Awake / Caffeine Modu', kw: 'uyanik tut awake caffeine uyku engelle ekran acik', run: () => send('setAwakeMode', { mode: '1h' }) },
                    { badge: 'Güç', title: 'Uyanık Tut (Süresiz)', sub: 'Kapatılana kadar uyku devre dışı', kw: 'uyanik tut suresiz awake forever', run: () => send('setAwakeMode', { mode: 'indefinite' }) },
                    { badge: 'Güç', title: 'Uyanık Tut Modunu Kapat', sub: 'Normal güç yönetimine dön', kw: 'uyanik kapat awake off', run: () => send('setAwakeMode', { mode: 'off' }) },
                    { badge: 'Görünüm', title: 'Kompakt / İnce Dock Moduna Geç', sub: 'Dizüstü ekranları için ince görev çubuğu', kw: 'kompakt ince kucuk dock density boyut', run: () => send('setDockDensity', { density: 'Compact' }) },
                    { badge: 'Görünüm', title: 'Standart Dock Boyutuna Dön', sub: 'Varsayılan dock yoğunluğu', kw: 'standart normal dock boyut', run: () => send('setDockDensity', { density: 'Standard' }) },
                    { badge: 'Görünüm', title: 'Dock Konumu: Sol Dikey Kenar', sub: 'Geniş ekranlar için sol dikey dock', kw: 'sol dikey dock kenar left vertical', run: () => send('setDockEdge', { edge: 'Left' }) },
                    { badge: 'Görünüm', title: 'Dock Konumu: Sağ Dikey Kenar', sub: 'Geniş ekranlar için sağ dikey dock', kw: 'sag dikey dock kenar right vertical', run: () => send('setDockEdge', { edge: 'Right' }) },
                    { badge: 'Görünüm', title: 'Dock Konumu: Alt Yatay Kenar', sub: 'Klasik alt orta dock', kw: 'alt yatay dock kenar bottom', run: () => send('setDockEdge', { edge: 'Bottom' }) },
                    { badge: 'Görünüm', title: 'Dock Boyutunu ve Konumunu Sıfırla', sub: 'Ortala ve %100 boyutuna dön', kw: 'dock sifirla reset boyut konum ortala', run: () => send('resetDockTransform') },
                    { badge: 'Pencere', title: 'Alt+Sürükle Pencere & Dock Yönetimi Aç / Kapat', sub: 'Alt+Sol Tık: Taşı • Alt+Sağ Tık: Boyutlandır', kw: 'alt surukle drag tasi boyutlandir altsnap dock', run: () => send('toggleAltDrag') },
                    { badge: 'Görünüm', title: 'Üst Damla Canlı Donanım OSD Aç / Kapat', sub: 'Ses, parlaklık ve CapsLock dinamik ada göstergesi', kw: 'osd hud canli damla ses parlaklik capslock', run: () => send('toggleHardwareOsd') },
                    { badge: 'Görünüm', title: 'Saatte Saniyeyi Göster / Gizle', sub: 'Üst damla saatine saniye ekle (HH:mm:ss)', kw: 'saat saniye seconds clock', run: () => send('toggleClockSeconds') },
                    { badge: 'Sistem', title: 'Mikrofonu Sessize Al / Aç', sub: 'Global Mic Mute', kw: 'mikrofon mic mute sessiz toplanti', run: () => send('toggleMicMute') },
                    { badge: 'Sistem', title: 'Güncellemeleri Kontrol Et (GitHub)', sub: 'Yeni Project Aurora sürümü var mı kontrol et', kw: 'guncelleme kontrol update surum github yeni', run: () => send('checkForUpdates') },
                    { badge: 'Sistem', title: 'Yeni Sürümü İndir ve Güncelle', sub: 'GitHub Releases üzerinden tek tıkla otomatik kur', kw: 'guncelle indir yukle install update aurora', run: () => send('installAvailableUpdate') },
                    { badge: 'Sistem', title: 'Aurora Ayarlarını Yedekle (.aurora-backup)', sub: 'Tema, sabitler ve tüm tercihleri kaydet', kw: 'yedek backup export disa aktar', run: () => send('exportBackup') },
                    { badge: 'Sistem', title: 'Bilgisayarı Uyku Moduna Al', sub: 'Sleep', kw: 'uyku sleep kapat', run: () => send('sleepPc') },
                    { badge: 'Sistem', title: 'Ekranı Kilitle', sub: 'Lock PC', kw: 'kilit lock oturum', run: () => send('lockPc') },
                    { badge: 'Tema', title: 'Aurora Kristal Açık Teması', sub: 'Yüksek kontrastlı açık cam', kw: 'acik tema light', run: () => send('applyTheme', { id: 'aurora-glass-light' }) },
                    { badge: 'Tema', title: 'Obsidian Pro Koyu Teması', sub: 'Profesyonel koyu cam', kw: 'koyu tema dark obsidian', run: () => send('applyTheme', { id: 'obsidian-pro-dark' }) },
                    { badge: 'Tema', title: 'Nebula Siber Teması', sub: 'Derin uzay yüzeyleri', kw: 'nebula siber tema', run: () => send('applyTheme', { id: 'nebula-dark' }) }
                ];
                for (const c of quickCmds) {
                    if (!qNorm) continue;
                    const sc = scoreSearchCandidate(normTr(c.title), normTr(c.sub), c.kw, qNorm, tokens, 0);
                    if (sc > 0) scored.push({ score: sc, ...c, icon: null, svgIcon: SVG.bolt });
                }
            }

            // H. Persistent Clipboard Vault matches when searching!
            if ((omniFilter === 'all' || omniFilter === 'file') && qNorm.length >= 2) {
                for (const c of (state.clipboard || [])) {
                    const hay = normTr((c.text || c.preview || '') + ' ' + (c.sourceApp || ''));
                    if (hay.includes(qNorm) || normTr('pano clipboard').includes(qNorm)) {
                        scored.push({
                            score: (c.pinned ? 540 : 420),
                            badge: c.pinned ? 'Pano ★' : 'Pano',
                            title: c.preview || 'Pano Öğesi',
                            sub: `${c.sourceApp || 'Pano'} · ${c.time || ''} • Kopyalamak için Enter`,
                            icon: c.thumb || null,
                            svgIcon: SVG.clip,
                            run: () => send('copyClipById', { id: c.id })
                        });
                    }
                }
            }

            scored.sort((a, b) => b.score - a.score);
            currentOmniItems = scored.slice(0, 9);

            if (rawQ && !rawQ.startsWith('>')) {
                currentOmniItems.push({
                    badge: 'Google',
                    title: `"${rawQ}" için Google'da Ara`,
                    sub: 'Varsayılan tarayıcıda aç',
                    icon: null,
                    svgIcon: SVG.search,
                    run: () => send('openUri', { uri: 'https://www.google.com/search?q=' + encodeURIComponent(rawQ) })
                });
            }

            if (omniIdx >= currentOmniItems.length) omniIdx = 0;

            listWrap.innerHTML = currentOmniItems.length === 0
                ? `<div style="padding:14px;text-align:center;font-size:12px;color:var(--text-2);">Bu filtrede eşleşen sonuç bulunamadı.</div>`
                : currentOmniItems.map((it, idx) => `
                    <div class="omni-row ${idx===omniIdx?'sel':''}" onclick="execOmni(${idx})">
                        ${it.icon ? `<img src="data:image/png;base64,${it.icon}"/>` : `<span style="width:22px;display:inline-flex;justify-content:center;color:var(--accent-1);">${it.svgIcon || SVG.search}</span>`}
                        <div style="flex:1;min-width:0;">
                            <div style="font-size:12.5px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(it.title)}</div>
                            <div style="font-size:11px;color:var(--text-2);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(it.sub)}</div>
                        </div>
                        <div class="omni-row-actions">
                            ${it.revealPath ? `<button class="micro-btn" onclick="event.stopPropagation();revealOmniItem(${idx});" title="Klasörde Göster">${SVG.folder}</button>` : ''}
                            ${it.canPin ? `<button class="micro-btn" onclick="event.stopPropagation();pinOmniItem(${idx});" title="Dock'a Sabitle">${SVG.pin}</button>` : ''}
                            <span class="pill-btn" style="height:22px;font-size:10.5px;">${escHtml(it.badge)}</span>
                        </div>
                    </div>
                `).join('');

            requestAnimationFrame(reportHitRects);
        }

        function pinAppByPath(idx) {
            const it = currentOmniItems[idx];
            if (!it) return;
            send('togglePin', { name: it.name, path: it.path });
        }

        function pinOmniItem(idx) {
            const it = currentOmniItems[idx];
            if (!it || !it.pinPath) return;
            send('togglePin', { name: it.pinName, path: it.pinPath });
        }

        function revealOmniItem(idx) {
            const it = currentOmniItems[idx];
            if (!it || !it.revealPath) return;
            closeOmni();
            send('launchPath', { path: 'reveal:' + it.revealPath });
        }

        function execOmni(idx) {
            const it = currentOmniItems[idx];
            closeOmni();
            if (it) it.run();
        }

        function renderTrayPopup() {
            const el = document.getElementById('trayPopup');
            if (!trayOpen || !state) {
                el.className = 'glass-shell flyout-panel';
                return;
            }
            const apps = state.trayApps || [];
            el.className = 'glass-shell flyout-panel open';
            el.innerHTML = `
                <div class="card-title" style="margin-bottom:0;">
                    <span class="card-title-l">${SVG.tray}<span>Sistem Tepsisi & Arka Plan Uygulamaları (${apps.length})</span></span>
                    <button class="micro-btn" onclick="toggleTrayPopup()">${SVG.close}</button>
                </div>
                ${apps.length === 0 ? `
                    <div style="font-size:12px;color:var(--text-2);padding:10px 4px;">
                        Şu an arka planda aktif tepsi uygulaması algılanmadı.
                    </div>
                ` : `
                    <div class="tray-grid">
                        ${apps.map((a, idx) => `
                            <div class="tray-item" onclick="send('openTrayApp',{index:${idx}});toggleTrayPopup();" title="${escHtml(a.name)}${a.subtitle ? ' — ' + escHtml(a.subtitle) : ''} (${a.memoryMb} MB)">
                                ${a.icon ? `<img src="data:image/png;base64,${a.icon}"/>` : `<span style="width:24px;display:inline-flex;justify-content:center;color:var(--accent-1);">${SVG.layers}</span>`}
                                <div style="flex:1;min-width:0;">
                                    <div style="font-size:12px;font-weight:700;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(a.name)}</div>
                                    <div style="font-size:10.5px;color:var(--text-2);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" class="num">${a.subtitle ? escHtml(a.subtitle) + ' · ' : ''}${a.memoryMb} MB</div>
                                </div>
                                ${a.canQuit ? `<button class="micro-btn danger" onclick="event.stopPropagation();send('quitTrayApp',{index:${idx}});" title="Uygulamayı Sonlandır">${SVG.close}</button>` : ''}
                            </div>
                        `).join('')}
                    </div>
                `}
                <div class="power-strip" style="padding-top:8px;">
                    <button class="pill-btn" onclick="send('peekNativeTray');toggleTrayPopup();" title="Sağ alt klasik Windows bildirim ikonlarına sağ tıklamak için 10 saniye görünür yapar">
                        ${SVG.desktop}<span>Klasik Windows Tepsisi (10 sn)</span>
                    </button>
                    <button class="pill-btn" onclick="send('launchPath',{path:'taskmgr.exe'});toggleTrayPopup();">${SVG.activity}<span>Görev Yöneticisi</span></button>
                </div>
            `;
        }

        function formatLocalDateKey(d) {
            return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        }

        function renderCalendarPopup() {
            const el = document.getElementById('calendarPopup');
            if (!calOpen || !state) {
                el.className = 'glass-shell flyout-panel';
                return;
            }
            el.className = 'glass-shell flyout-panel open';

            const now = new Date();
            const todayKey = formatLocalDateKey(now);
            if (!selectedDateKey) selectedDateKey = todayKey;

            const viewDate = new Date(now.getFullYear(), now.getMonth() + calMonthOffset, 1);
            const monthTitle = new Intl.DateTimeFormat('tr-TR', { month: 'long', year: 'numeric' }).format(viewDate);
            const fullTodayTitle = new Intl.DateTimeFormat('tr-TR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(now);

            const mondayOffset = (viewDate.getDay() + 6) % 7;
            const daysInMonth = new Date(viewDate.getFullYear(), viewDate.getMonth() + 1, 0).getDate();
            const prevMonthDays = new Date(viewDate.getFullYear(), viewDate.getMonth(), 0).getDate();

            const notesMap = state.agendaNotes || {};
            const cells = [];

            for (let i = mondayOffset - 1; i >= 0; i--) {
                cells.push(`<div class="cal-day muted num">${prevMonthDays - i}</div>`);
            }
            for (let d = 1; d <= daysInMonth; d++) {
                const key = formatLocalDateKey(new Date(viewDate.getFullYear(), viewDate.getMonth(), d));
                const isToday = key === todayKey;
                const isSel = key === selectedDateKey;
                const hasNote = Boolean(notesMap[key]);
                cells.push(`
                    <div class="cal-day num ${isToday?'today':''} ${isSel?'selected':''}" onclick="selectCalendarDate('${key}')">
                        <span>${d}</span>
                        ${hasNote ? `<span class="note-dot"></span>` : ''}
                    </div>
                `);
            }
            let nextDay = 1;
            while (cells.length % 7 !== 0) {
                cells.push(`<div class="cal-day muted num">${nextDay++}</div>`);
            }

            const curNote = notesMap[selectedDateKey] || '';
            el.innerHTML = `
                <div style="display:flex;align-items:center;justify-content:space-between;">
                    <div>
                        <div style="font-size:10.5px;color:var(--accent-1);font-weight:700;text-transform:uppercase;letter-spacing:0.04em;">${escHtml(fullTodayTitle)}</div>
                        <div style="font-size:15.5px;font-weight:800;text-transform:capitalize;margin-top:2px;">${escHtml(monthTitle)}</div>
                    </div>
                    <div style="display:flex;gap:4px;align-items:center;">
                        <button class="micro-btn" onclick="calMonthOffset--;renderCalendarPopup();reportHitRects();" title="Önceki Ay">${SVG.chevL}</button>
                        <button class="pill-btn" style="height:26px;padding:0 9px;font-size:11px;" onclick="calMonthOffset=0;selectedDateKey='${todayKey}';renderCalendarPopup();reportHitRects();">Bugün</button>
                        <button class="micro-btn" onclick="calMonthOffset++;renderCalendarPopup();reportHitRects();" title="Sonraki Ay">${SVG.chevR}</button>
                        <button class="micro-btn" onclick="toggleCalendarPopup()">${SVG.close}</button>
                    </div>
                </div>
                <div class="cal-weekdays">
                    <span>Pzt</span><span>Sal</span><span>Çar</span><span>Per</span><span>Cum</span><span>Cmt</span><span>Paz</span>
                </div>
                <div class="cal-days">${cells.join('')}</div>
                <div style="padding-top:9px;border-top:1px solid var(--border-subtle);display:flex;flex-direction:column;gap:6px;">
                    <div style="font-size:11px;font-weight:700;color:var(--text-2);display:flex;justify-content:space-between;">
                        <span class="num">${escHtml(selectedDateKey)} Ajanda Notu</span>
                        <span style="color:var(--accent-1);">Yerel Kayıt</span>
                    </div>
                    <div style="display:flex;gap:6px;">
                        <input id="agendaInput" type="text" value="${escHtml(curNote)}"
                               placeholder="Toplantı, hatırlatma veya hedef ekleyin..."
                               style="flex:1;background:var(--bg-inset);border:1px solid var(--border-subtle);border-radius:10px;padding:7px 10px;color:var(--text-1);font-size:12px;outline:none;user-select:text;"
                               onkeydown="if(event.key==='Enter'){saveSelectedAgenda();}" />
                        <button class="pill-btn accent" onclick="saveSelectedAgenda()">Kaydet</button>
                    </div>
                </div>
            `;
        }

        function selectCalendarDate(key) {
            selectedDateKey = key;
            renderCalendarPopup();
            requestAnimationFrame(() => {
                reportHitRects();
                const inp = document.getElementById('agendaInput');
                if (inp) inp.focus();
            });
        }

        function saveSelectedAgenda() {
            const inp = document.getElementById('agendaInput');
            if (!inp) return;
            send('saveAgendaNote', { date: selectedDateKey, note: inp.value });
        }

        function renderDropPopup() {
            const el = document.getElementById('dropPopup');
            if (!dropOpen || !state) {
                el.className = 'glass-shell flyout-panel';
                return;
            }
            const files = state.dropShelf || [];
            el.className = 'glass-shell flyout-panel open';
            el.innerHTML = `
                <div class="card-title" style="margin-bottom:0;">
                    <span class="card-title-l">${SVG.inbox}<span>Sürükle-Bırak Dosya Rafı (${files.length})</span></span>
                    <div style="display:flex;gap:5px;">
                        <button class="pill-btn" style="height:26px;" onclick="toggleDropPopup();send('pickAndShareQr');">${SVG.qr}<span>QR Gönder</span></button>
                        <button class="pill-btn" style="height:26px;" onclick="send('pickShelfFile')">${SVG.plus}<span>Dosya Ekle</span></button>
                        ${files.length > 0 ? `<button class="pill-btn" style="height:26px;" onclick="send('clearShelfFiles')">Temizle</button>` : ''}
                        <button class="micro-btn" onclick="toggleDropPopup()">${SVG.close}</button>
                    </div>
                </div>
                <div class="drop-zone-box ${isDraggingFile?'drag-active':''}" onclick="send('pickShelfFile')">
                    <div style="font-size:12px;font-weight:700;color:var(--accent-1);">Dosyayı Üst Damla Adaya Sürükleyin — 3 Akıllı Yuva Açılır</div>
                    <div style="font-size:11px;color:var(--text-2);margin-top:3px;">Süper Raf · QR ile Telefona Gönder · Hızlı Dönüştür / ZIP</div>
                </div>
                ${files.length > 0 ? `
                    <div style="max-height:200px;overflow-y:auto;">
                        ${files.map((f, idx) => `
                            <div class="shelf-file-row">
                                ${f.icon ? `<img src="data:image/png;base64,${f.icon}"/>` : SVG.folder}
                                <div style="flex:1;min-width:0;cursor:pointer;" onclick="send('openShelfFile',{index:${idx}});toggleDropPopup();" title="${escHtml(f.path)}">
                                    <div style="font-size:12px;font-weight:700;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(f.name)}</div>
                                    <div style="font-size:10.5px;color:var(--text-2);">${escHtml(f.sizeLabel)}</div>
                                </div>
                                <button class="micro-btn" onclick="toggleDropPopup();send('shareShelfFileQr',{index:${idx}})" title="QR ile Telefona Gönder">${SVG.qr}</button>
                                <button class="micro-btn" onclick="send('convertShelfFile',{index:${idx}})" title="Hızlı Dönüştür / ZIP">${SVG.zip}</button>
                                <button class="micro-btn" onclick="send('copyText',{text:state.dropShelf[${idx}].path})" title="Yolu Kopyala">${SVG.copy}</button>
                                <button class="micro-btn" onclick="send('revealShelfFile',{index:${idx}})" title="Klasörde Göster">${SVG.folder}</button>
                                <button class="micro-btn danger" onclick="send('removeShelfFile',{index:${idx}})" title="Kaldır">${SVG.close}</button>
                            </div>
                        `).join('')}
                    </div>
                ` : ''}
            `;
        }

        function renderNotifPopup() {
            const el = document.getElementById('notifPopup');
            if (!notifOpen || !state) {
                el.className = 'glass-shell flyout-panel';
                return;
            }
            const items = state.notifications || [];
            el.className = 'glass-shell flyout-panel open';
            el.innerHTML = `
                <div class="card-title" style="margin-bottom:0;">
                    <span class="card-title-l">${SVG.bell}<span>Canlı Bildirim Merkezi (${items.length})</span></span>
                    <div style="display:flex;gap:5px;align-items:center;">
                        <button class="pill-btn ${state.quietHours?'accent':''}" style="height:25px;font-size:10.5px;" onclick="send('toggleQuietHours')">${SVG.moon}<span>${state.quietHours ? 'Sessiz' : 'Odak'}</span></button>
                        ${items.length > 0 ? `<button class="pill-btn" style="height:25px;font-size:10.5px;" onclick="send('clearNotifications')">Temizle</button>` : ''}
                        <button class="micro-btn" onclick="toggleNotifPopup()">${SVG.close}</button>
                    </div>
                </div>
                ${items.length === 0 ? `
                    <div style="padding:18px 8px;text-align:center;color:var(--text-2);font-size:12px;">
                        Tüm bildirimler okundu — WhatsApp, Teams ve Windows bildirimleri buraya canlı düşer.
                    </div>
                ` : `
                    <div style="display:flex;flex-direction:column;gap:7px;max-height:280px;overflow-y:auto;">
                        ${items.map(n => `
                            <div class="notif-card">
                                <div style="flex:1;min-width:0;">
                                    <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;">
                                        <span class="notif-app-tag">${escHtml(n.app || 'Windows')}</span>
                                        ${n.time ? `<span class="num" style="font-size:10px;color:var(--text-2);">${escHtml(n.time)}</span>` : ''}
                                    </div>
                                    <div style="font-size:12.5px;font-weight:700;margin-top:2px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(n.title)}</div>
                                    ${n.body ? `<div style="font-size:11.5px;color:var(--text-2);margin-top:2px;line-height:1.35;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;">${escHtml(n.body)}</div>` : ''}
                                </div>
                                <button class="micro-btn" onclick="send('dismissNotification',{id:'${escHtml(n.id)}'})" title="Kapat">${SVG.close}</button>
                            </div>
                        `).join('')}
                    </div>
                `}
            `;
        }

        function selectAudioOutputIdx(idx) {
            const ep = (state?.audioOutputs || [])[idx];
            if (ep && ep.id) send('setAudioOutputDevice', { id: ep.id });
        }

        function confirmPowerAction(kind) {
            const btn = document.getElementById(kind === 'restart' ? 'pwRestart' : 'pwShutdown');
            if (armedPowerAction === kind) {
                armedPowerAction = null;
                clearTimeout(armedPowerTimer);
                closeOmni();
                send(kind === 'restart' ? 'restartPc' : 'shutdownPc');
                return;
            }
            armedPowerAction = kind;
            resetPowerButtonsUi();
            if (btn) {
                btn.classList.add('danger-arm');
                btn.innerHTML = (kind === 'restart' ? SVG.restart : SVG.power) + '<span>Onayla?</span>';
            }
            clearTimeout(armedPowerTimer);
            armedPowerTimer = setTimeout(() => {
                armedPowerAction = null;
                resetPowerButtonsUi();
            }, 3500);
        }

        function resetPowerButtonsUi() {
            const r = document.getElementById('pwRestart');
            const s = document.getElementById('pwShutdown');
            if (r) { r.classList.remove('danger-arm'); r.innerHTML = SVG.restart + '<span>Yeniden Başlat</span>'; }
            if (s) { s.classList.remove('danger-arm'); s.innerHTML = SVG.power + '<span>Kapat</span>'; }
        }

        function openOnboardingTour() {
            expanded = false;
            closeAllSubPopups();
            manualOnboardingOpen = true;
            onboardingDismissedLocal = false;
            renderAll();
            syncFocusDemand();
        }

        function finishOnboarding() {
            manualOnboardingOpen = false;
            onboardingDismissedLocal = true;
            send('completeOnboarding');
            renderAll();
            syncFocusDemand();
        }

        function renderOnboardingCard() {
            const el = document.getElementById('onboardingCard');
            if (!state || !isOnboardingVisible()) {
                el.className = 'glass-shell';
                return;
            }
            const curTheme = state.theme?.id || 'aurora-glass-light';
            const curDock = state.dockMode || 'AlwaysVisible';
            el.className = 'glass-shell open';
            el.innerHTML = `
                <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;">
                    <div style="display:flex;align-items:center;gap:10px;">
                        <div class="orb">✦</div>
                        <div>
                            <div style="font-size:15px;font-weight:800;letter-spacing:-0.015em;">Project Aurora'ya Hoş Geldin</div>
                            <div style="font-size:11.5px;color:var(--text-2);">5 saniyede görünümünü seç, masaüstünü anında hızlandır</div>
                        </div>
                    </div>
                    <button class="micro-btn" onclick="finishOnboarding()">${SVG.close}</button>
                </div>

                <div>
                    <div class="card-title" style="margin-bottom:6px;"><span>1. Tema Tercihin</span></div>
                    <div style="display:flex;gap:6px;flex-wrap:wrap;">
                        <button class="pill-btn ${curTheme==='aurora-glass-light'?'accent':''}" onclick="send('applyTheme',{id:'aurora-glass-light'})">Aurora Kristal Açık</button>
                        <button class="pill-btn ${curTheme==='obsidian-pro-dark'?'accent':''}" onclick="send('applyTheme',{id:'obsidian-pro-dark'})">Obsidian Pro Koyu</button>
                        <button class="pill-btn ${curTheme==='nebula-dark'?'accent':''}" onclick="send('applyTheme',{id:'nebula-dark'})">Nebula Siber</button>
                    </div>
                </div>

                <div>
                    <div class="card-title" style="margin-bottom:6px;"><span>2. Alt Dock Davranışı</span></div>
                    <div style="display:flex;gap:6px;flex-wrap:wrap;">
                        <button class="pill-btn ${curDock==='SmartDodge'?'accent':''}" onclick="send('setDockMode',{mode:'SmartDodge'})">Akıllı Kaçınma (Önerilen)</button>
                        <button class="pill-btn ${curDock==='AlwaysVisible'?'accent':''}" onclick="send('setDockMode',{mode:'AlwaysVisible'})">Her Zaman Sabit</button>
                        <button class="pill-btn ${curDock==='AutoHide'?'accent':''}" onclick="send('setDockMode',{mode:'AutoHide'})">Otomatik Gizle</button>
                    </div>
                </div>

                <div class="onb-tips">
                    <div class="onb-tip"><strong>Dairesel Halka & Tekerlek</strong><span>Çok pencereli simgeye gelince dairesel küre + 60 FPS önizleme açılır; fare tekerleği pencereleri döner.</span></div>
                    <div class="onb-tip"><strong>Evrensel Arama (${escHtml(state.omniHotkeyPreset || 'Alt+Space')})</strong><span>280+ Store & Win32 uygulama, yerel dosyalar, ayarlar, birim çevirici ve <code>&gt;</code> komut çalıştırıcı.</span></div>
                    <div class="onb-tip"><strong>Üst Damla Ada</strong><span>Üzerine gelince açılır; canlı Windows bildirimleri (🔔), sanal masaüstü (M1/2) ve ses çıkışı elinin altında.</span></div>
                    <div class="onb-tip"><strong>Sürükle-Bırak & Klasör Yığını</strong><span>Dock'a dosya sürükleyip bekleyince pencere öne gelir; klasör bırakınca yığın olarak sabitlenir.</span></div>
                </div>

                <div style="display:flex;justify-content:flex-end;align-items:center;gap:8px;padding-top:4px;">
                    <button class="pill-btn accent" style="height:34px;padding:0 18px;font-size:12.5px;" onclick="finishOnboarding()">
                        ${SVG.sparkles}<span>Aurora'yı Kullanmaya Başla</span>
                    </button>
                </div>
            `;
        }

        function routeAppAudioByIdx(pid, idxStr) {
            const idx = parseInt(idxStr, 10);
            const ep = (idx >= 0 && state?.audioOutputs) ? state.audioOutputs[idx] : null;
            send('setAppAudioOutput', { pid, deviceId: ep ? ep.id : '' });
        }

        function setClipFilter(f) {
            clipFilter = f || 'all';
            lastExpandedSig = '';
            renderAll();
        }

        function buildClipboardListHtml(clips) {
            const q = normTr(clipQuery || '');
            const filtered = (clips || []).filter(c => {
                if (clipFilter === 'pinned' && !c.pinned) return false;
                if (clipFilter === 'image' && c.kind !== 'image') return false;
                if (clipFilter === 'text' && c.kind !== 'text') return false;
                if (q) {
                    const hay = normTr((c.text || '') + ' ' + (c.preview || '') + ' ' + (c.sourceApp || ''));
                    if (!hay.includes(q)) return false;
                }
                return true;
            });
            if (filtered.length === 0) {
                return `<div style="font-size:11.5px;color:var(--text-2);padding:12px 4px;text-align:center;">${q ? 'Aramanızla eşleşen pano öğesi yok.' : 'Metin veya ekran görüntüsü kopyaladığınızda (Ctrl+C) burada kalıcı olarak saklanır.'}</div>`;
            }
            return filtered.slice(0, 40).map(c => {
                const rawTxt = (c.text || c.preview || '').trim();
                const isColor = c.kind === 'text' && (/^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(rawTxt) || /^rgba?\(\s*\d+\s*,\s*\d+\s*,\s*\d+/i.test(rawTxt));
                const isUrl = c.kind === 'text' && /^https?:\/\/\S+$/i.test(rawTxt);
                const leftVisual = c.kind === 'image' && c.thumb
                    ? `<img class="clip-thumb" src="data:image/png;base64,${c.thumb}"/>`
                    : (isColor
                        ? `<span class="clip-color-swatch" style="background:${escHtml(rawTxt)};" title="Renk Önizleme: ${escHtml(rawTxt)}"></span>`
                        : `<span style="width:28px;height:28px;border-radius:8px;background:rgba(56,189,248,0.12);color:var(--accent-1);flex-shrink:0;display:inline-flex;align-items:center;justify-content:center;">${isUrl ? SVG.globe : SVG.copy}</span>`);
                return `
                <div class="clip-card ${c.pinned ? 'pinned' : ''}" onclick="send('copyClipById',{id:${c.id}})" title="${state?.clipboardAutoPaste ? 'Tıklayınca önceki pencereye doğrudan yapıştırır' : 'Panoya kopyalamak için tıklayın'}">
                    ${leftVisual}
                    <div style="flex:1;min-width:0;">
                        <div class="clip-text-preview">${escHtml(c.preview)}</div>
                        <div class="clip-meta">
                            <span>${escHtml(c.sourceApp || 'Pano')}</span>
                            <span>·</span>
                            <span class="num">${escHtml(c.time || '')}</span>
                            ${isColor ? `<span style="color:var(--accent-1);font-weight:700;">· Renk</span>` : ''}
                            ${isUrl ? `<span style="color:var(--accent-2);font-weight:700;">· Bağlantı</span>` : ''}
                            ${c.pinned ? `<span style="color:var(--accent-1);font-weight:700;">· Sabitli</span>` : ''}
                        </div>
                    </div>
                    <div class="clip-actions">
                        ${isUrl ? `<button class="micro-btn" onclick="event.stopPropagation();send('openUri',{uri:'${escHtml(rawTxt)}'})" title="Tarayıcıda Aç">${SVG.extLink}</button>` : ''}
                        <button class="micro-btn ${c.pinned ? 'active' : ''}" onclick="event.stopPropagation();send('togglePinClip',{id:${c.id}})" title="${c.pinned ? 'Sabitlemeyi Kaldır' : 'Sabitle (Silinmez)'}">${SVG.pin}</button>
                        <button class="micro-btn danger" onclick="event.stopPropagation();send('deleteClip',{id:${c.id}})" title="Sil">${SVG.close}</button>
                    </div>
                </div>`;
            }).join('');
        }

        function cycleDockDensity() {
            const cur = state?.dockDensity || 'Standard';
            const next = cur === 'Standard' ? 'Compact' : (cur === 'Compact' ? 'Cozy' : 'Standard');
            send('setDockDensity', { density: next });
        }

        function cycleDockEdge() {
            const cur = state?.dockEdge || 'Bottom';
            const next = cur === 'Bottom' ? 'Left' : (cur === 'Left' ? 'Right' : 'Bottom');
            send('setDockEdge', { edge: next });
        }

        function snapPresetLabel(id) {
            if (id === 'studio3') return 'Üçlü Stüdyo';
            if (id === 'cols3') return '3 Sütun';
            if (id === 'grid2x2') return '2×2 Izgara';
            return 'Kodlama 65/35';
        }

        function cycleSnapZonePreset() {
            const order = ['code6535', 'studio3', 'cols3', 'grid2x2'];
            const cur = state?.snapZonePreset || 'code6535';
            const next = order[(order.indexOf(cur) + 1) % order.length];
            send('setSnapZonePreset', { preset: next });
        }

        document.addEventListener('DOMContentLoaded', () => {
            document.getElementById('pwDesktop').innerHTML = SVG.desktop + '<span>Masaüstü</span>';
            document.getElementById('pwOcr').innerHTML = SVG.scanText + '<span>Metin OCR</span>';
            document.getElementById('pwColor').innerHTML = SVG.pipette + '<span>Renk Seç</span>';
            document.getElementById('pwGif').innerHTML = SVG.record + '<span>GIF Kaydet</span>';
            document.getElementById('pwSnip').innerHTML = SVG.scissors + '<span>Alıntı</span>';
            document.getElementById('pwTask').innerHTML = SVG.activity + '<span>Görev Yön.</span>';
            document.getElementById('pwTrash').innerHTML = SVG.trash + '<span>Çöp</span>';
            document.getElementById('pwSettings').innerHTML = SVG.settings + '<span>Ayarlar</span>';
            document.getElementById('pwSleep').innerHTML = SVG.sleep + '<span>Uyku</span>';
            document.getElementById('pwLock').innerHTML = SVG.lock + '<span>Kilitle</span>';
            resetPowerButtonsUi();

            // Liquid Droplet Hover Wake & Grace Recede
            const wrap = document.getElementById('crownWrap');
            wrap.addEventListener('mouseenter', () => {
                clearTimeout(hoverLeaveTimer);
                if (!crownHovered) {
                    crownHovered = true;
                    renderAll();
                }
            });
            wrap.addEventListener('mouseleave', () => {
                clearTimeout(hoverLeaveTimer);
                hoverLeaveTimer = setTimeout(() => {
                    if (!isAnyPopupOpen() && !crownPinned && !isDraggingFile) {
                        crownHovered = false;
                        renderAll();
                    }
                }, 460);
            });

            const inp = document.getElementById('omniInput');
            inp.addEventListener('input', () => { omniIdx = 0; renderOmniList(inp.value); });
            inp.addEventListener('keydown', e => {
                if (e.key === 'Escape') { closeOmni(); }
                else if (e.key === 'Tab') {
                    e.preventDefault();
                    const order = ['all', 'app', 'file', 'setting', 'window'];
                    const next = order[(order.indexOf(omniFilter) + (e.shiftKey ? order.length - 1 : 1)) % order.length];
                    setOmniFilter(next);
                }
                else if (e.key === 'ArrowDown' && currentOmniItems.length) { e.preventDefault(); omniIdx = (omniIdx + 1) % currentOmniItems.length; renderOmniList(inp.value, true); }
                else if (e.key === 'ArrowUp' && currentOmniItems.length) { e.preventDefault(); omniIdx = (omniIdx - 1 + currentOmniItems.length) % currentOmniItems.length; renderOmniList(inp.value, true); }
                else if (e.key === 'Enter') {
                    e.preventDefault();
                    if (currentOmniItems[omniIdx]) execOmni(omniIdx);
                }
            });

            window.addEventListener('resize', () => requestAnimationFrame(reportHitRects));

            window.addEventListener('dragover', e => {
                e.preventDefault();
                const slot = e.target?.closest?.('[data-drop-zone]');
                const nextZone = slot ? slot.getAttribute('data-drop-zone') : dragDropZone;
                const zoneChanged = nextZone && nextZone !== dragDropZone;
                if (zoneChanged) dragDropZone = nextZone;
                if (!isDraggingFile) {
                    isDraggingFile = true;
                    dragDropZone = nextZone || 'shelf';
                    crownHovered = true;
                    renderAll();
                } else if (zoneChanged) {
                    document.querySelectorAll('.drop-target-slot').forEach(el => {
                        el.classList.toggle('active-zone', el.getAttribute('data-drop-zone') === dragDropZone);
                    });
                }
            });
            window.addEventListener('dragleave', e => {
                if (e.relatedTarget === null) {
                    isDraggingFile = false;
                    dragDropZone = 'shelf';
                    renderAll();
                }
            });
            window.addEventListener('drop', e => {
                e.preventDefault();
                const chosenZone = e.target?.closest?.('[data-drop-zone]')?.getAttribute('data-drop-zone') || dragDropZone || 'shelf';
                isDraggingFile = false;
                dragDropZone = 'shelf';
                if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0 && window.chrome.webview.postMessageWithAdditionalObjects) {
                    window.chrome.webview.postMessageWithAdditionalObjects({ action: 'filesDropped', zone: chosenZone }, Array.from(e.dataTransfer.files));
                    if (chosenZone === 'shelf') dropOpen = true;
                    crownHovered = true;
                }
                renderAll();
                syncFocusDemand();
            });
        });

        window.chrome.webview.addEventListener('message', e => {
            const msg = e.data;
            if (msg.type === 'gifRecState') {
                liveGifRec = { recording: Boolean(msg.recording), elapsed: Number(msg.elapsed || 0) };
                renderAll();
            } else if (msg.type === 'collapseDeckForPaste') {
                expanded = false;
                closeAllSubPopups();
                notifyMetricsVisible();
                if (!crownPinned) crownHovered = false;
                renderAll();
                syncFocusDemand();
            } else if (msg.type === 'catalog') {
                if (Array.isArray(msg.installedApps)) appCatalog = msg.installedApps;
                if (Array.isArray(msg.themes)) themesCatalog = msg.themes;
                if (msg.frecency && typeof msg.frecency === 'object') searchFrecency = msg.frecency;
                if (omniOpen) {
                    const inp = document.getElementById('omniInput');
                    renderOmniList(inp ? inp.value : '', true);
                }
            } else if (msg.type === 'frecency') {
                if (msg.frecency && typeof msg.frecency === 'object') searchFrecency = msg.frecency;
            } else if (msg.type === 'fileSearchResults') {
                if (msg.seq === fileSearchSeq && Array.isArray(msg.files)) {
                    latestFileHits = msg.files;
                    latestFileQuery = msg.query || '';
                    if (omniOpen) {
                        const inp = document.getElementById('omniInput');
                        renderOmniList(inp ? inp.value : '', true);
                    }
                }
            } else if (msg.type === 'clockTick') {
                if (state && msg.clock) {
                    state.clock = msg.clock;
                    const tEl = document.querySelector('#crown.droplet:not(.osd-active) .drop-time');
                    if (tEl) tEl.textContent = msg.clock;
                    const pEl = document.getElementById('crownClockPill');
                    if (pEl) pEl.textContent = `${msg.clock} • ${state.shortDate || ''}`;
                }
            } else if (msg.type === 'state') {
                state = msg.payload;
                if (typeof state.crownPinned === 'boolean') crownPinned = state.crownPinned;
                const activeEl = document.activeElement;
                const editingText = activeEl && (activeEl.id === 'agendaInput' || activeEl.id === 'scratchArea' || activeEl.id === 'clipSearchInput');
                if (!editingText && !omniOpen) {
                    renderAll();
                }
            } else if (msg.type === 'syncLayout') {
                reportHitRects();
            } else if (msg.type === 'toggleOmni') {
                if (omniOpen) closeOmni(); else openOmni();
            } else if (msg.type === 'toggleTray') {
                toggleTrayPopup();
            } else if (msg.type === 'toggleCalendar') {
                toggleCalendarPopup();
            } else if (msg.type === 'toggleDropShelf') {
                toggleDropPopup();
            } else if (msg.type === 'setDropletPreview') {
                crownHovered = Boolean(msg.hovered);
                renderAll();
            } else if (msg.type === 'toggleDeck') {
                activeTab = msg.tab || 'control';
                toggleCrown(!expanded);
            } else if (msg.type === 'outsideDeactivate') {
                if (isAnyPopupOpen()) {
                    expanded = false;
                    closeAllSubPopups();
                    notifyMetricsVisible();
                    if (!crownPinned) crownHovered = false;
                    renderAll();
                }
            } else if (msg.type === 'osdHud') {
                activeOsd = {
                    kind: msg.kind || 'volume',
                    label: msg.label || 'Ses',
                    valueText: msg.valueText || '',
                    percent: (typeof msg.percent === 'number') ? msg.percent : null,
                    active: Boolean(msg.active)
                };
                renderAll();
                clearTimeout(osdHideTimer);
                osdHideTimer = setTimeout(() => {
                    activeOsd = null;
                    renderAll();
                }, Math.max(700, msg.durationMs || 1450));
            } else if (msg.type === 'toast') {
                const t = document.getElementById('toast');
                t.innerHTML = `<span style="color:var(--accent-1);font-size:15px;font-weight:800;">✦</span><div><strong>${escHtml(msg.title)}</strong><div style="font-size:11.5px;color:var(--text-2);">${escHtml(msg.body)}</div></div>`;
                t.className = 'glass-shell show';
                requestAnimationFrame(reportHitRects);
                clearTimeout(toastTimer);
                toastTimer = setTimeout(() => { t.className = 'glass-shell'; requestAnimationFrame(reportHitRects); }, 3800);
            }
        });
        </script>
        </body>
        </html>
        """;

    public static string BuildCrystalDockHtml() => $$$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="UTF-8">
        <style>
        {{{SharedDesignCss}}}
        :root {
            --radial-disc-bg: radial-gradient(circle at 50% 30%, rgba(22, 34, 62, 0.95), rgba(8, 13, 26, 0.98));
            --radial-orb-bg: rgba(255, 255, 255, 0.075);
            --radial-orb-border: rgba(255, 255, 255, 0.16);
            --radial-orb-hover: rgba(56, 189, 248, 0.22);
            --radial-orbit-bg: rgba(11, 18, 34, 0.96);
            --radial-caption-bg: rgba(9, 14, 27, 0.96);
            --radial-ring-stroke: rgba(148, 163, 184, 0.34);
            --radial-shadow: 0 22px 52px -10px rgba(0, 0, 0, 0.78), 0 6px 20px -4px rgba(0, 0, 0, 0.45);
            --acc-bg: rgba(255, 255, 255, 0.045);
            --acc-open-bg: rgba(56, 189, 248, 0.10);
        }
        .dock-stage {
            width: 100%; height: 100%;
            display: flex; flex-direction: column;
            justify-content: flex-end; align-items: center;
            padding-bottom: 7px;
            pointer-events: none;
            position: relative;
        }

        /* ========== RADIAL WINDOW ORB HUB + ORBITAL FEATURE RING ========== */
        #radialHub {
            position: fixed;
            bottom: 64px;
            left: 50%;
            width: 296px;
            padding: 4px 6px 14px 6px;
            display: none;
            flex-direction: column;
            align-items: center;
            pointer-events: auto;
            z-index: 30;
            transform-origin: bottom center;
        }
        #radialHub.open { display: flex; }
        #radialHub.animate-in { animation: radialBloom 0.21s var(--spring); }
        @keyframes radialBloom {
            from { opacity: 0; transform: translateY(14px) scale(0.86); }
            to { opacity: 1; transform: translateY(0) scale(1); }
        }

        .radial-caption {
            max-width: 286px;
            min-height: 29px;
            padding: 4px 12px;
            margin-bottom: 5px;
            border-radius: 999px;
            background: var(--radial-caption-bg);
            border: 1px solid var(--border-rim);
            box-shadow: 0 10px 26px -6px rgba(0,0,0,0.42), inset 0 1px 0 rgba(255,255,255,0.18);
            display: inline-flex; align-items: center; gap: 7px;
            font-size: 11.5px; font-weight: 700; color: var(--text-1);
            letter-spacing: -0.01em;
            transition: border-color 0.15s ease;
        }
        .radial-caption.danger { border-color: var(--danger); }
        .radial-cap-dot {
            width: 6.5px; height: 6.5px; border-radius: 50%;
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            box-shadow: 0 0 8px var(--accent-1);
            flex-shrink: 0;
        }
        .radial-caption.danger .radial-cap-dot {
            background: var(--danger);
            box-shadow: 0 0 8px var(--danger);
        }
        .radial-cap-text {
            overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
            max-width: 214px;
        }
        .radial-cap-badge {
            font-size: 10px; font-weight: 800;
            color: var(--accent-1);
            padding: 1px 6px; border-radius: 99px;
            background: rgba(56,189,248,0.14);
            flex-shrink: 0;
        }

        .radial-dial {
            position: relative;
            width: 284px;
            height: 248px;
            flex-shrink: 0;
        }
        .radial-svg {
            position: absolute; inset: 0;
            width: 284px; height: 248px;
            pointer-events: none;
            overflow: visible;
        }
        .radial-disc {
            position: absolute;
            width: 172px; height: 172px;
            left: 56px; top: 34px;
            border-radius: 50%;
            background: var(--radial-disc-bg);
            border: 1px solid var(--border-rim);
            box-shadow:
                var(--radial-shadow),
                inset 0 1px 0 rgba(255, 255, 255, 0.24),
                inset 0 0 28px rgba(56, 189, 248, 0.07);
            pointer-events: auto;
        }

        /* Circular Window Orbs inside the glass ring */
        .win-orb {
            position: absolute;
            border-radius: 50%;
            transform: translate(-50%, -50%);
            background: var(--radial-orb-bg);
            border: 1.5px solid var(--radial-orb-border);
            display: flex; flex-direction: column;
            align-items: center; justify-content: center;
            gap: 2px;
            padding: 4px;
            cursor: pointer;
            transition: transform 0.18s var(--spring), background 0.15s, border-color 0.15s, box-shadow 0.18s;
            box-shadow: 0 4px 12px -4px rgba(0, 0, 0, 0.32);
            z-index: 2;
        }
        .win-orb:hover, .win-orb.targeted {
            transform: translate(-50%, -50%) scale(1.11);
            background: var(--radial-orb-hover);
            border-color: var(--accent-1);
            box-shadow: 0 0 0 2px rgba(56, 189, 248, 0.26), 0 10px 22px -6px rgba(56, 189, 248, 0.45);
            z-index: 4;
        }
        .win-orb.active-win:not(.targeted) {
            border-color: rgba(56, 189, 248, 0.65);
            box-shadow: 0 0 0 1px rgba(56, 189, 248, 0.25);
        }
        .win-orb img {
            width: 16px; height: 16px; border-radius: 4px;
            pointer-events: none; flex-shrink: 0;
        }
        .win-orb.orb-lg img { width: 22px; height: 22px; border-radius: 5.5px; }
        .win-orb.orb-sm img { width: 13px; height: 13px; border-radius: 3.5px; }
        .win-orb-lbl {
            font-size: 9px; font-weight: 700; line-height: 1.1;
            letter-spacing: -0.02em;
            color: var(--text-1);
            text-align: center;
            max-width: 90%;
            overflow: hidden;
            text-overflow: ellipsis;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            word-break: normal;
            overflow-wrap: normal;
            pointer-events: none;
        }
        .win-orb.orb-lg .win-orb-lbl { font-size: 10px; }
        .win-orb.orb-sm .win-orb-lbl { font-size: 8.5px; -webkit-line-clamp: 1; }
        .win-orb-idx {
            position: absolute; top: -3px; right: -2px;
            min-width: 16px; height: 16px; padding: 0 3px;
            border-radius: 99px;
            background: var(--radial-orbit-bg);
            border: 1px solid var(--border-rim);
            color: var(--text-2);
            font-size: 9px; font-weight: 800;
            display: flex; align-items: center; justify-content: center;
            pointer-events: none;
            transition: all 0.15s ease;
        }
        .win-orb.targeted .win-orb-idx,
        .win-orb.active-win .win-orb-idx {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
            border-color: transparent;
        }

        /* Outer Orbital Feature Buttons around the ring perimeter */
        .orbit-btn {
            position: absolute;
            width: 34px; height: 34px;
            border-radius: 50%;
            transform: translate(-50%, -50%);
            background: var(--radial-orbit-bg);
            border: 1px solid var(--border-rim);
            color: var(--text-2);
            display: flex; align-items: center; justify-content: center;
            cursor: pointer;
            box-shadow: 0 8px 20px -6px rgba(0, 0, 0, 0.50), inset 0 1px 0 rgba(255,255,255,0.16);
            transition: transform 0.16s var(--spring), background 0.14s, color 0.14s, border-color 0.14s, box-shadow 0.14s;
            z-index: 3;
        }
        .orbit-btn:hover {
            transform: translate(-50%, -50%) scale(1.16);
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
            border-color: transparent;
            box-shadow: 0 10px 24px -4px rgba(56, 189, 248, 0.55);
            z-index: 5;
        }
        .orbit-btn.active {
            background: rgba(56, 189, 248, 0.22);
            color: var(--accent-1);
            border-color: var(--accent-1);
        }
        .orbit-btn.danger:hover {
            background: linear-gradient(135deg, #FB7185, #E11D48);
            color: #FFFFFF;
            border-color: transparent;
            box-shadow: 0 10px 24px -4px rgba(251, 113, 133, 0.58);
        }
        .orbit-btn:active { transform: translate(-50%, -50%) scale(0.95); }

        .radial-bottom-pill {
            position: absolute;
            left: 50%; top: 222px;
            transform: translateX(-50%);
            height: 24px; padding: 0 10px;
            border-radius: 999px;
            background: var(--radial-orbit-bg);
            border: 1px solid var(--border-rim);
            color: var(--text-2);
            font-family: var(--font); font-size: 10.5px; font-weight: 700;
            display: inline-flex; align-items: center; gap: 5px;
            cursor: pointer;
            box-shadow: 0 8px 18px -6px rgba(0, 0, 0, 0.44);
            transition: all 0.15s var(--ease);
            white-space: nowrap;
            z-index: 4;
        }
        .radial-bottom-pill:hover {
            color: var(--text-1);
            border-color: var(--accent-1);
            transform: translateX(-50%) translateY(-1px);
        }
        .radial-bottom-pill.pinned {
            color: var(--accent-1);
            border-color: rgba(56,189,248,0.55);
        }
        .radial-stem-pip {
            position: absolute;
            bottom: 3px;
            width: 24px; height: 3.5px;
            border-radius: 99px;
            transform: translateX(-50%);
            background: linear-gradient(90deg, transparent, var(--accent-1), var(--accent-2), transparent);
            box-shadow: 0 0 10px var(--accent-1);
            pointer-events: none;
        }

        /* ========== CRYSTAL DOCK & INLINE ACCORDION ========== */
        #dockBar {
            --ds: 1;
            --item-sz: calc(42px * var(--ds));
            --img-sz: calc(26px * var(--ds));
            pointer-events: auto;
            position: relative;
            display: flex; align-items: center; gap: calc(8px * var(--ds));
            max-width: calc(100vw - 20px);
            width: auto;
            height: auto;
            min-height: calc(56px * var(--ds));
            padding: calc(7px * var(--ds)) calc(12px * var(--ds));
            border-radius: calc(22px * var(--ds));
            transition: opacity 0.2s, border-radius 0.2s var(--ease);
            z-index: 20;
        }
        #dockBar.tucked {
            height: 7px; min-height: 7px; width: 224px; min-width: 0; padding: 0; border-radius: 99px;
            background: linear-gradient(90deg, transparent 0%, var(--accent-1) 28%, var(--accent-2) 72%, transparent 100%);
            border: none;
            box-shadow: 0 0 16px rgba(56, 189, 248, 0.72), 0 2px 8px rgba(0, 0, 0, 0.35);
            cursor: pointer; opacity: 0.92;
        }
        .dock-pin-btn {
            position: relative;
        }
        .dock-pin-btn.smart-dodge {
            background: linear-gradient(135deg, rgba(129,140,248,0.24), rgba(56,189,248,0.20));
            color: var(--accent-2);
            border-color: var(--accent-2);
        }
        .dock-pin-btn .hover-label {
            top: -34px;
        }
        .dock-pin-btn:hover .hover-label {
            opacity: 1;
        }

        /* Attention Pulse & Live Progress Arc on Dock Icons */
        @keyframes attnPulse {
            0%, 100% { box-shadow: 0 0 0 1.5px rgba(251,191,36,0.75), 0 0 16px -2px rgba(251,191,36,0.55); border-color: var(--warning); }
            50% { box-shadow: 0 0 0 3px rgba(251,191,36,0.95), 0 0 24px 0px rgba(251,191,36,0.78); border-color: var(--warning); }
        }
        .dock-item.attention {
            animation: attnPulse 1.65s ease-in-out infinite;
        }
        .attn-pip {
            position: absolute; top: -3px; left: -3px;
            width: 9px; height: 9px; border-radius: 50%;
            background: var(--warning);
            box-shadow: 0 0 8px var(--warning);
            border: 1.5px solid var(--bg-glass-2);
            pointer-events: none;
        }
        .dock-item.drag-target {
            border-color: var(--accent-1) !important;
            background: rgba(56, 189, 248, 0.28) !important;
            box-shadow: 0 0 0 2.5px rgba(56, 189, 248, 0.55), 0 10px 24px -4px rgba(56, 189, 248, 0.55) !important;
            transform: translateY(-6px) scale(1.12) !important;
        }
        .dock-prog-svg {
            position: absolute; inset: -2px;
            width: calc(100% + 4px); height: calc(100% + 4px);
            pointer-events: none;
        }
        .prog-pill {
            position: absolute; bottom: -5px; right: -5px;
            padding: 0 4px; height: 14px; border-radius: 99px;
            background: var(--success); color: #04101F;
            font-size: 8.5px; font-weight: 800;
            display: inline-flex; align-items: center; justify-content: center;
            box-shadow: 0 2px 6px rgba(0,0,0,0.35);
            pointer-events: none;
        }

        /* Dock Folder Stacks ("Klasör Yığınları") */
        .folder-chip {
            position: relative;
            width: calc(var(--item-sz) * 0.91); height: calc(var(--item-sz) * 0.91); flex-shrink: 0;
            border-radius: calc(11px * var(--ds));
            display: inline-flex; align-items: center; justify-content: center;
            background: var(--bg-card);
            border: 1px solid var(--border-subtle);
            color: var(--accent-1);
            cursor: pointer;
            transform-origin: bottom center;
            transition: background 0.14s, border-color 0.14s, box-shadow 0.14s;
        }
        .folder-chip:hover, .folder-chip.open {
            background: rgba(56,189,248,0.16);
            border-color: var(--accent-1);
            box-shadow: 0 6px 16px -4px rgba(56,189,248,0.38);
        }
        .folder-chip:hover:not(.open) .hover-label { opacity: 1; }
        #folderStackCard {
            position: fixed;
            bottom: 66px;
            left: 50%;
            width: 288px;
            max-height: 318px;
            border-radius: 20px;
            padding: 11px;
            display: none;
            flex-direction: column;
            gap: 7px;
            pointer-events: auto;
            z-index: 32;
            transform-origin: bottom center;
            animation: radialBloom 0.18s var(--spring);
        }
        #folderStackCard.open { display: flex; }
        .fstack-list {
            display: flex; flex-direction: column; gap: 4px;
            max-height: 242px; overflow-y: auto;
        }
        .fstack-row {
            display: flex; align-items: center; gap: 8px;
            padding: 6px 8px; border-radius: 11px;
            background: var(--bg-card); border: 1px solid transparent;
            cursor: pointer; transition: background 0.13s, border-color 0.13s;
        }
        .fstack-row:hover {
            background: var(--bg-card-hover); border-color: var(--border-rim);
        }
        .fstack-row img { width: 20px; height: 20px; border-radius: 5px; flex-shrink: 0; }
        .radial-bottom-row {
            position: absolute;
            left: 50%; top: 220px;
            transform: translateX(-50%);
            display: inline-flex; align-items: center; gap: 5px;
            z-index: 4;
        }
        .radial-bottom-row .radial-bottom-pill {
            position: static; transform: none;
        }
        .radial-bottom-row .radial-bottom-pill:hover {
            transform: translateY(-1px);
        }
        .radial-bottom-row .radial-bottom-pill.danger:hover {
            border-color: var(--danger); color: var(--danger);
        }
        .search-pill {
            position: relative;
            height: calc(var(--item-sz) * 0.95); padding: 0 calc(13px * var(--ds)); border-radius: calc(13px * var(--ds));
            display: inline-flex; align-items: center; gap: calc(7px * var(--ds));
            background: linear-gradient(135deg, rgba(56,189,248,0.20), rgba(129,140,248,0.16));
            border: 1px solid rgba(56,189,248,0.40);
            color: var(--text-1); font-size: calc(12.5px * var(--ds)); font-weight: 700;
            cursor: pointer; transform-origin: bottom center; flex-shrink: 0;
            transition: all 0.16s var(--ease);
        }
        .search-pill:hover {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); border-color: transparent;
            transform: translateY(-1.5px);
        }
        .search-pill:hover .hover-label { opacity: 1; }
        .dock-item {
            position: relative;
            width: var(--item-sz); height: var(--item-sz); flex-shrink: 0;
            border-radius: calc(12px * var(--ds));
            display: flex; align-items: center; justify-content: center;
            cursor: pointer;
            transform-origin: bottom center;
            will-change: transform;
            background: var(--bg-card);
            border: 1px solid var(--border-subtle);
            transition: background 0.15s, border-color 0.15s, box-shadow 0.15s;
        }
        /* Invisible hit-bridge underneath lifted icons so clicking never slips during hover elevation */
        .dock-item::before {
            content: '';
            position: absolute;
            inset: -3px -3px -10px -3px;
            pointer-events: auto;
        }
        .dock-item:hover {
            background: var(--bg-card-hover);
            border-color: rgba(255, 255, 255, 0.24);
            box-shadow: 0 8px 20px -6px rgba(0, 0, 0, 0.55), 0 0 0 1px rgba(56, 189, 248, 0.22);
        }
        .dock-item.active {
            background: linear-gradient(145deg, rgba(56,189,248,0.22), rgba(129,140,248,0.18));
            border-color: var(--accent-1);
            box-shadow: 0 6px 18px -4px rgba(56,189,248,0.42);
        }
        .dock-item.radial-anchored {
            border-color: var(--accent-1);
            box-shadow: 0 0 0 1.5px rgba(56,189,248,0.38);
        }
        .dock-item img { width: var(--img-sz); height: var(--img-sz); border-radius: calc(6.5px * var(--ds)); pointer-events: none; }
        .dock-item .fallback-badge {
            width: var(--img-sz); height: var(--img-sz); border-radius: calc(7px * var(--ds));
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); font-weight: 800; font-size: calc(12.5px * var(--ds));
            display: flex; align-items: center; justify-content: center;
        }
        .dock-dot {
            position: absolute; bottom: -5px; left: 50%; transform: translateX(-50%);
            height: 3.5px; border-radius: 99px;
            background: var(--text-2);
            pointer-events: none;
        }
        .dock-item.active .dock-dot {
            width: 16px;
            background: linear-gradient(90deg, var(--accent-1), var(--accent-2));
        }
        .dock-item.running:not(.active) .dock-dot { width: 5px; }
        .count-pill {
            position: absolute; top: -4px; right: -4px;
            min-width: 16px; height: 16px; padding: 0 4px;
            border-radius: 99px;
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
            font-size: 9.5px; font-weight: 800;
            display: flex; align-items: center; justify-content: center;
            box-shadow: 0 2px 6px rgba(0,0,0,0.32);
            pointer-events: none;
        }
        .hover-label {
            position: absolute; top: -34px; left: 50%; transform: translateX(-50%);
            padding: 4px 10px; border-radius: 9px;
            background: rgba(8, 12, 24, 0.96);
            border: 1px solid rgba(255, 255, 255, 0.16);
            color: #F8FAFC; font-size: 11px; font-weight: 600;
            white-space: nowrap; opacity: 0; pointer-events: none;
            transition: opacity 0.13s ease, transform 0.14s var(--ease);
            box-shadow: 0 10px 24px -6px rgba(0,0,0,0.52);
            z-index: 95;
        }
        .dock-item:hover:not(.radial-anchored) .hover-label { opacity: 1; }
        .dock-v-btn { position: relative; }
        .dock-v-btn:hover .hover-label { opacity: 1; }
        .dock-v-badge {
            display: none;
            position: absolute; top: -4px; right: -4px;
            min-width: 15px; height: 15px; padding: 0 3.5px;
            border-radius: 99px;
            background: rgba(15, 23, 42, 0.94);
            border: 1px solid var(--border-rim);
            color: var(--text-1);
            font-size: 8.5px; font-weight: 800;
            align-items: center; justify-content: center;
            pointer-events: none;
            box-shadow: 0 2px 6px rgba(0,0,0,0.38);
        }
        .dock-sep {
            width: 1px; height: 26px;
            background: linear-gradient(180deg, transparent, var(--border-rim), transparent);
            margin: 0 2px; flex-shrink: 0;
        }

        /* Dock Density Variants (Compact for 13-14" laptops, Cozy for 4K/Touch) */
        #dockBar.density-compact {
            --item-sz: calc(35px * var(--ds));
            --img-sz: calc(21px * var(--ds));
            padding: calc(5px * var(--ds)) calc(10px * var(--ds));
            gap: calc(6px * var(--ds));
            border-radius: calc(17px * var(--ds));
        }
        #dockBar.density-compact .acc-bucket { height: calc(37px * var(--ds)); border-radius: calc(11px * var(--ds)); }
        #dockBar.density-compact .acc-head { height: calc(32px * var(--ds)); padding: 0 calc(7px * var(--ds)); font-size: calc(11px * var(--ds)); }
        #dockBar.density-compact .dock-sep { height: calc(22px * var(--ds)); }

        #dockBar.density-cozy {
            --item-sz: calc(48px * var(--ds));
            --img-sz: calc(30px * var(--ds));
            padding: calc(9px * var(--ds)) calc(15px * var(--ds));
            gap: calc(9px * var(--ds));
            border-radius: calc(24px * var(--ds));
        }
        #dockBar.density-cozy .acc-bucket { height: calc(50px * var(--ds)); border-radius: calc(16px * var(--ds)); }
        #dockBar.density-cozy .acc-head { height: calc(44px * var(--ds)); font-size: calc(12.5px * var(--ds)); }

        /* Inline Accordion Category Stack for Smart Tidy ("Derli") */
        .acc-bucket {
            display: inline-flex; align-items: center;
            height: var(--item-sz); border-radius: calc(12px * var(--ds));
            background: var(--acc-bg);
            border: 1px solid var(--border-rim);
            padding: 0 2px;
            flex-shrink: 0;
            transition: background 0.18s ease, border-color 0.18s ease, box-shadow 0.18s ease;
        }
        .acc-bucket.has-active {
            border-color: rgba(56, 189, 248, 0.55);
        }
        .acc-bucket.open {
            background: var(--acc-open-bg);
            border-color: var(--accent-1);
            padding: 0 calc(5px * var(--ds)) 0 calc(3px * var(--ds));
            box-shadow: 0 6px 20px -6px rgba(56, 189, 248, 0.35);
        }
        .acc-head {
            height: calc(var(--item-sz) - 4px); padding: 0 calc(8px * var(--ds)); border-radius: calc(10px * var(--ds));
            display: inline-flex; align-items: center; gap: calc(6px * var(--ds));
            cursor: pointer; font-size: calc(11.5px * var(--ds)); font-weight: 700;
            color: var(--text-1);
            transition: background 0.15s ease, transform 0.15s var(--ease);
        }
        .acc-head:hover { background: var(--bg-card-hover); transform: translateY(-1px); }
        .stack-icons { display: flex; align-items: center; }
        .stack-icons img {
            width: calc(18px * var(--ds)); height: calc(18px * var(--ds)); border-radius: calc(5px * var(--ds));
            margin-left: calc(-5px * var(--ds)); border: 1.5px solid var(--bg-glass-2);
            background: var(--bg-glass-2);
        }
        .stack-icons img:first-child { margin-left: 0; }
        .acc-count {
            min-width: calc(17px * var(--ds)); height: calc(17px * var(--ds)); padding: 0 calc(4.5px * var(--ds));
            border-radius: 99px;
            background: rgba(56, 189, 248, 0.16);
            color: var(--accent-1);
            font-size: calc(10px * var(--ds)); font-weight: 800;
            display: inline-flex; align-items: center; justify-content: center;
        }
        .acc-drawer {
            display: none;
            align-items: center;
            gap: calc(5px * var(--ds));
            padding-left: calc(6px * var(--ds));
            margin-left: calc(4px * var(--ds));
            border-left: 1px solid var(--border-subtle);
        }
        .acc-bucket.open .acc-drawer {
            display: inline-flex;
            animation: accFanIn 0.20s var(--spring);
        }
        .acc-bucket.open .acc-drawer .dock-item {
            width: calc(var(--item-sz) * 0.86); height: calc(var(--item-sz) * 0.86); border-radius: calc(10px * var(--ds));
        }
        .acc-bucket.open .acc-drawer .dock-item img { width: calc(var(--img-sz) * 0.84); height: calc(var(--img-sz) * 0.84); }
        .acc-bucket.open .acc-drawer .dock-item .dock-dot { bottom: -3px; height: 3px; }
        @keyframes accFanIn {
            from { opacity: 0; transform: translateX(-8px) scale(0.92); }
            to { opacity: 1; transform: translateX(0) scale(1); }
        }

        /* ========== VERTICAL DOCK MODES (ARCHITECTURAL GLASS RAIL — LEFT & RIGHT) ========== */
        body.edge-left .dock-stage {
            flex-direction: row; justify-content: flex-start; align-items: center;
            padding: 10px 0 10px 7px;
        }
        body.edge-right .dock-stage {
            flex-direction: row; justify-content: flex-end; align-items: center;
            padding: 10px 7px 10px 0;
        }
        body.edge-left #dockBar,
        body.edge-right #dockBar {
            flex-direction: column;
            width: auto;
            height: auto;
            min-height: 0;
            min-width: calc(56px * var(--ds));
            padding: calc(9px * var(--ds)) calc(7px * var(--ds));
            gap: calc(7px * var(--ds));
            max-width: none;
            max-height: calc(100vh - 20px);
            border-radius: calc(20px * var(--ds));
        }
        body.edge-left #dockBar {
            box-shadow:
                14px 0 38px -10px rgba(0, 0, 0, 0.72),
                0 14px 32px -12px rgba(0, 0, 0, 0.52),
                inset 1.5px 0 0 rgba(255, 255, 255, 0.24),
                inset 0 1px 0 rgba(255, 255, 255, 0.14);
        }
        body.edge-right #dockBar {
            box-shadow:
                -14px 0 38px -10px rgba(0, 0, 0, 0.72),
                0 14px 32px -12px rgba(0, 0, 0, 0.52),
                inset -1.5px 0 0 rgba(255, 255, 255, 0.24),
                inset 0 1px 0 rgba(255, 255, 255, 0.14);
        }
        body.edge-left #dockBar.density-compact,
        body.edge-right #dockBar.density-compact {
            min-width: calc(48px * var(--ds));
            padding: calc(7px * var(--ds)) calc(6px * var(--ds));
            gap: calc(5.5px * var(--ds));
            border-radius: calc(17px * var(--ds));
        }
        body.edge-left #dockBar.density-cozy,
        body.edge-right #dockBar.density-cozy {
            min-width: calc(64px * var(--ds));
            padding: calc(11px * var(--ds)) calc(8px * var(--ds));
            gap: calc(8.5px * var(--ds));
            border-radius: calc(22px * var(--ds));
        }
        body.edge-left #dockBar.tucked,
        body.edge-right #dockBar.tucked {
            width: 6px; min-width: 6px; height: 116px; min-height: 116px; padding: 0;
            border-radius: 99px;
            background: linear-gradient(180deg, transparent 0%, var(--accent-1) 28%, var(--accent-2) 72%, transparent 100%);
            box-shadow: 0 0 16px rgba(56, 189, 248, 0.72), 0 2px 10px rgba(0, 0, 0, 0.42);
        }
        body.edge-left .dock-sep,
        body.edge-right .dock-sep {
            width: calc(24px * var(--ds)); height: 1px;
            background: linear-gradient(90deg, transparent, var(--border-rim), transparent);
            margin: calc(2px * var(--ds)) 0;
        }
        /* Unified Squircle Grid: Search, Apps, Category Stacks, Folder Stacks & Utilities share exact same width & corner rhythm */
        body.edge-left .search-pill,
        body.edge-right .search-pill {
            width: var(--item-sz); height: var(--item-sz);
            border-radius: calc(12px * var(--ds));
            padding: 0; justify-content: center;
            background: linear-gradient(145deg, rgba(56,189,248,0.18), rgba(129,140,248,0.13));
            border: 1px solid rgba(56,189,248,0.34);
        }
        body.edge-left .search-pill:hover,
        body.edge-right .search-pill:hover {
            transform: none;
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); border-color: transparent;
            box-shadow: 0 6px 18px -4px rgba(56,189,248,0.48);
        }
        body.edge-left .folder-chip,
        body.edge-right .folder-chip {
            width: var(--item-sz); height: var(--item-sz);
            border-radius: calc(12px * var(--ds));
        }
        body.edge-left .dock-item,
        body.edge-left .folder-chip,
        body.edge-left .acc-bucket {
            transform-origin: center left;
        }
        body.edge-right .dock-item,
        body.edge-right .folder-chip,
        body.edge-right .acc-bucket {
            transform-origin: center right;
        }
        body.edge-left .dock-item::before { inset: -3px -10px -3px -6px; }
        body.edge-right .dock-item::before { inset: -3px -6px -3px -10px; }
        body.edge-left .dock-dot {
            left: -5px; right: auto; bottom: auto; top: 50%; transform: translateY(-50%);
            width: 3px; height: 5px;
        }
        body.edge-left .dock-item.active .dock-dot,
        body.edge-left .acc-bucket.has-active > .acc-head .dock-dot { width: 3px; height: 16px; background: linear-gradient(180deg, var(--accent-1), var(--accent-2)); }
        body.edge-left .dock-item.running:not(.active) .dock-dot { width: 3px; height: 5px; }
        body.edge-right .dock-dot {
            right: -5px; left: auto; bottom: auto; top: 50%; transform: translateY(-50%);
            width: 3px; height: 5px;
        }
        body.edge-right .dock-item.active .dock-dot,
        body.edge-right .acc-bucket.has-active > .acc-head .dock-dot { width: 3px; height: 16px; background: linear-gradient(180deg, var(--accent-1), var(--accent-2)); }
        body.edge-right .dock-item.running:not(.active) .dock-dot { width: 3px; height: 5px; }
        body.edge-left .hover-label,
        body.edge-left .dock-pin-btn .hover-label,
        body.edge-left .dock-v-btn .hover-label,
        body.edge-left .search-pill .hover-label,
        body.edge-left .acc-head .hover-label {
            left: calc(100% + 12px); right: auto; top: 50%; bottom: auto;
            transform: translateY(-50%) translateX(-4px);
        }
        body.edge-left .dock-item:hover:not(.radial-anchored) .hover-label,
        body.edge-left .folder-chip:hover:not(.open) .hover-label,
        body.edge-left .dock-pin-btn:hover .hover-label,
        body.edge-left .dock-v-btn:hover .hover-label,
        body.edge-left .search-pill:hover .hover-label,
        body.edge-left .acc-bucket:not(.open) .acc-head:hover .hover-label {
            opacity: 1;
            transform: translateY(-50%) translateX(0);
        }
        body.edge-right .hover-label,
        body.edge-right .dock-pin-btn .hover-label,
        body.edge-right .dock-v-btn .hover-label,
        body.edge-right .search-pill .hover-label,
        body.edge-right .acc-head .hover-label {
            right: calc(100% + 12px); left: auto; top: 50%; bottom: auto;
            transform: translateY(-50%) translateX(4px);
        }
        body.edge-right .dock-item:hover:not(.radial-anchored) .hover-label,
        body.edge-right .folder-chip:hover:not(.open) .hover-label,
        body.edge-right .dock-pin-btn:hover .hover-label,
        body.edge-right .dock-v-btn:hover .hover-label,
        body.edge-right .search-pill:hover .hover-label,
        body.edge-right .acc-bucket:not(.open) .acc-head:hover .hover-label {
            opacity: 1;
            transform: translateY(-50%) translateX(0);
        }
        /* Vertical Multi-App Category Stacks: Clean iOS/macOS Folder-Style 2x2 Mosaic Squircle! */
        body.edge-left .acc-bucket,
        body.edge-right .acc-bucket {
            flex-direction: column;
            width: var(--item-sz); height: auto !important;
            padding: 0;
            border-radius: calc(12px * var(--ds));
            background: var(--bg-card);
            border: 1px solid var(--border-subtle);
            overflow: visible;
        }
        body.edge-left .acc-bucket.open,
        body.edge-right .acc-bucket.open {
            background: var(--acc-open-bg);
            border-color: var(--accent-1);
            padding: 2px 0 4px 0;
            border-radius: calc(14px * var(--ds));
        }
        body.edge-left .acc-head,
        body.edge-right .acc-head {
            position: relative;
            width: calc(var(--item-sz) - 2px); height: calc(var(--item-sz) - 2px) !important;
            padding: 0; border-radius: calc(11px * var(--ds));
            flex-direction: column; justify-content: center; align-items: center; gap: 0;
        }
        body.edge-left .acc-head:hover,
        body.edge-right .acc-head:hover {
            transform: none;
            background: var(--bg-card-hover);
        }
        .v-mosaic {
            width: calc(var(--item-sz) * 0.72);
            height: calc(var(--item-sz) * 0.72);
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            grid-template-rows: repeat(2, 1fr);
            gap: 2px;
            align-items: center; justify-items: center;
            padding: 1px;
        }
        .v-mosaic img {
            width: 100%; height: 100%;
            object-fit: contain;
            border-radius: calc(4px * var(--ds));
            pointer-events: none;
        }
        .v-mosaic.pair {
            grid-template-rows: 1fr;
            align-content: center;
            height: auto;
        }
        .v-mosaic.pair img {
            width: calc(var(--item-sz) * 0.35);
            height: calc(var(--item-sz) * 0.35);
        }
        body.edge-left .acc-drawer,
        body.edge-right .acc-drawer {
            flex-direction: column; gap: 5px;
            padding: 5px 0 1px 0; margin: 3px 0 0 0;
            border-left: none; border-top: 1px solid var(--border-subtle);
            width: 100%; align-items: center;
        }
        body.edge-left .acc-bucket.open .acc-drawer .dock-item,
        body.edge-right .acc-bucket.open .acc-drawer .dock-item {
            width: calc(var(--item-sz) * 0.84); height: calc(var(--item-sz) * 0.84);
            border-radius: calc(10px * var(--ds));
        }
        body.edge-left .acc-bucket.open .acc-drawer .dock-item img,
        body.edge-right .acc-bucket.open .acc-drawer .dock-item img {
            width: calc(var(--img-sz) * 0.84); height: calc(var(--img-sz) * 0.84);
        }
        /* Unified Vertical Utility Squircles (Tray / Smart Tidy / Pin) — Zero mismatched pills! */
        body.edge-left .dock-v-btn,
        body.edge-right .dock-v-btn,
        body.edge-left .dock-pin-btn,
        body.edge-right .dock-pin-btn {
            width: var(--item-sz);
            height: calc(var(--item-sz) * 0.84);
            border-radius: calc(11px * var(--ds));
            padding: 0;
            justify-content: center;
            background: var(--bg-card);
            border: 1px solid var(--border-subtle);
            color: var(--text-2);
            box-shadow: none;
        }
        body.edge-left .dock-v-btn:hover,
        body.edge-right .dock-v-btn:hover,
        body.edge-left .dock-pin-btn:hover,
        body.edge-right .dock-pin-btn:hover {
            transform: none;
            background: var(--bg-card-hover);
            border-color: var(--border-rim);
            color: var(--text-1);
        }
        body.edge-left .dock-v-btn.accent,
        body.edge-right .dock-v-btn.accent,
        body.edge-left .dock-pin-btn.active,
        body.edge-right .dock-pin-btn.active {
            background: linear-gradient(145deg, rgba(56,189,248,0.22), rgba(129,140,248,0.18));
            border-color: rgba(56,189,248,0.65);
            color: var(--accent-1);
            box-shadow: 0 4px 14px -4px rgba(56,189,248,0.38);
        }
        body.edge-left .dock-pin-btn.smart-dodge,
        body.edge-right .dock-pin-btn.smart-dodge {
            background: linear-gradient(145deg, rgba(129,140,248,0.24), rgba(56,189,248,0.16));
            border-color: rgba(129,140,248,0.65);
            color: var(--accent-2);
        }
        body.edge-left .dock-v-badge,
        body.edge-right .dock-v-badge {
            display: inline-flex;
        }
        body.edge-left .radial-stem-pip,
        body.edge-right .radial-stem-pip { display: none; }
        </style>
        </head>
        <body>
        <div class="dock-stage">
            <div id="radialHub"></div>
            <div id="folderStackCard" class="glass-shell"></div>
            <div id="dockBar" class="glass-shell"></div>
        </div>

        <script>
        {{{SharedSvgJs}}}

        let state = null;
        let radialGroupKey = null;
        let radialLastOpenedKey = null;
        let radialSelectedHwnd = null;
        let radialAnchorX = null;
        let radialAnchorY = null;
        let radialAnchorCatId = null;
        let openFolderPath = null;
        let openFolderAnchorX = null;
        let openFolderAnchorY = null;
        let folderStackCache = {};
        let folderCloseTimer = null;
        let pointerInFolder = false;
        let liveDodgedOverride = null;
        let isDraggingFileOverDock = false;
        let dragFocusTimer = null;
        let expandedAccordionCat = null;
        let manualTucked = false;
        let autoHideRevealed = false;
        let autoHideTimer = null;
        let hoverRadialTimer = null;
        let radialCloseTimer = null;
        let accOpenTimer = null;
        let accCloseTimer = null;
        let orbTargetTimer = null;
        let dwmPreviewTimer = null;
        let dwmPreviewActiveHwnd = null;
        let mouseX = null;
        let mouseY = null;
        let pointerInRadial = false;
        let dragPinState = null;
        let liveScaleOverride = null;
        let lastRenderedEdge = null;

        function getDockEdge() { return state?.dockEdge || 'Bottom'; }
        function isVerticalEdge() { const e = getDockEdge(); return e === 'Left' || e === 'Right'; }

        function applyDockGeometryAndOffset() {
            const dock = document.getElementById('dockBar');
            if (!dock) return;
            const edge = getDockEdge();
            const isVert = isVerticalEdge();
            const tuckedNow = isDockTuckedNow();

            const rawScale = liveScaleOverride !== null ? liveScaleOverride : (state?.dockScalePercent ?? 100);
            let ds = Math.max(0.75, Math.min(1.35, rawScale / 100.0));
            dock.style.setProperty('--ds', ds.toFixed(3));

            if (!tuckedNow && isVert) {
                const maxH = Math.max(240, window.innerHeight - 24);
                const actualH = dock.scrollHeight;
                if (actualH > maxH && actualH > 0) {
                    const fitDs = Math.max(0.68, ds * ((maxH - 6) / actualH));
                    dock.style.setProperty('--ds', fitDs.toFixed(3));
                }
            }

            const offsetPct = Math.max(-100, Math.min(100, state?.dockOffsetPercent ?? 0));
            if (offsetPct === 0) {
                dock.style.left = '';
                dock.style.top = '';
            } else if (isVert) {
                const rect = dock.getBoundingClientRect();
                const maxTravel = Math.max(0, (window.innerHeight - rect.height) / 2 - 14);
                const dy = Math.round((offsetPct / 100.0) * maxTravel);
                dock.style.left = '';
                dock.style.top = `${dy}px`;
            } else {
                const rect = dock.getBoundingClientRect();
                const maxTravel = Math.max(0, (window.innerWidth - rect.width) / 2 - 14);
                const dx = Math.round((offsetPct / 100.0) * maxTravel);
                dock.style.top = '';
                dock.style.left = `${dx}px`;
            }

            dock.querySelectorAll('.mag-target').forEach(el => {
                el._baseW = null;
                el._baseH = null;
            });
        }

        function send(action, payload = {}) {
            window.chrome.webview.postMessage({ action, ...payload });
        }

        function escHtml(s) {
            return String(s || '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
        }

        function hexToRgba(hex, alpha) {
            const h = (hex || '#121A2F').replace('#', '');
            const bigint = parseInt(h.length === 8 ? h.slice(2) : h, 16);
            const r = (bigint >> 16) & 255, g = (bigint >> 8) & 255, b = bigint & 255;
            return `rgba(${r},${g},${b},${alpha})`;
        }

        function applyThemeVars(t) {
            if (!t) return;
            const root = document.documentElement.style;
            const isLight = t.colorMode === 'Light';
            root.setProperty('--bg-glass-1', isLight ? 'rgba(248,250,252,0.97)' : hexToRgba(t.colors.surface1, 0.965));
            root.setProperty('--bg-glass-2', isLight ? 'rgba(235,241,250,0.988)' : hexToRgba(t.colors.surface2, 0.985));
            root.setProperty('--bg-card', isLight ? 'rgba(15,23,42,0.048)' : 'rgba(255,255,255,0.05)');
            root.setProperty('--bg-card-hover', isLight ? 'rgba(15,23,42,0.095)' : 'rgba(255,255,255,0.105)');
            root.setProperty('--border-rim', isLight ? 'rgba(15,23,42,0.16)' : 'rgba(255,255,255,0.16)');
            root.setProperty('--border-subtle', isLight ? 'rgba(15,23,42,0.085)' : 'rgba(255,255,255,0.085)');
            root.setProperty('--text-1', t.colors.textPrimary);
            root.setProperty('--text-2', t.colors.textSecondary);
            root.setProperty('--accent-1', t.colors.accentPrimary);
            root.setProperty('--accent-2', t.colors.accentSecondary);
            root.setProperty('--accent-ink', isLight ? '#FFFFFF' : '#04101F');

            root.setProperty('--radial-disc-bg', isLight
                ? 'radial-gradient(circle at 50% 25%, rgba(255,255,255,0.985), rgba(232,240,251,0.985))'
                : 'radial-gradient(circle at 50% 30%, rgba(20,31,56,0.96), rgba(8,13,26,0.985))');
            root.setProperty('--radial-orb-bg', isLight ? 'rgba(15,23,42,0.05)' : 'rgba(255,255,255,0.075)');
            root.setProperty('--radial-orb-border', isLight ? 'rgba(15,23,42,0.14)' : 'rgba(255,255,255,0.16)');
            root.setProperty('--radial-orb-hover', isLight ? 'rgba(2,132,199,0.15)' : 'rgba(56,189,248,0.22)');
            root.setProperty('--radial-orbit-bg', isLight ? 'rgba(255,255,255,0.98)' : 'rgba(11,18,34,0.96)');
            root.setProperty('--radial-caption-bg', isLight ? 'rgba(255,255,255,0.985)' : 'rgba(9,14,27,0.965)');
            root.setProperty('--radial-ring-stroke', isLight ? 'rgba(15,23,42,0.22)' : 'rgba(148,163,184,0.34)');
            root.setProperty('--radial-shadow', isLight
                ? '0 22px 46px -12px rgba(15,23,42,0.32), 0 6px 18px -4px rgba(15,23,42,0.16)'
                : '0 22px 52px -10px rgba(0,0,0,0.80), 0 6px 20px -4px rgba(0,0,0,0.48)');
            root.setProperty('--acc-bg', isLight ? 'rgba(15,23,42,0.045)' : 'rgba(255,255,255,0.045)');
            root.setProperty('--acc-open-bg', isLight ? 'rgba(2,132,199,0.09)' : 'rgba(56,189,248,0.11)');
        }

        function isDockTuckedNow() {
            if (radialGroupKey || openFolderPath || autoHideRevealed || isDraggingFileOverDock)
                return false;
            if (manualTucked)
                return true;
            const mode = state?.dockMode || (state?.dockAutoHide ? 'AutoHide' : 'AlwaysVisible');
            if (mode === 'AutoHide') return true;
            if (mode === 'SmartDodge') {
                const dodged = (liveDodgedOverride !== null) ? liveDodgedOverride : Boolean(state?.dockDodged);
                return dodged;
            }
            return false;
        }

        function reportHitRects() {
            const rects = [];
            const dock = document.getElementById('dockBar');
            const hub = document.getElementById('radialHub');
            const fcard = document.getElementById('folderStackCard');
            const edge = getDockEdge();
            const isVert = isVerticalEdge();
            let dockRect = null;

            if (dock && getComputedStyle(dock).display !== 'none') {
                const r = dock.getBoundingClientRect();
                if (r.width > 0 && r.height > 0) {
                    dockRect = r;
                    const tuckedNow = isDockTuckedNow();
                    if (isVert) {
                        if (tuckedNow) {
                            const padY = 48;
                            const rx = edge === 'Left' ? 0 : Math.max(0, r.left - 24);
                            const rw = edge === 'Left' ? (r.right + 24) : (window.innerWidth - rx);
                            rects.push({ x: rx, y: Math.max(0, r.top - padY), w: Math.max(32, rw), h: r.height + padY * 2 });
                        } else {
                            const padLeft = edge === 'Right' ? 38 : 14;
                            const padRight = edge === 'Left' ? 38 : 14;
                            const padY = 18;
                            rects.push({
                                x: Math.max(0, r.left - padLeft),
                                y: Math.max(0, r.top - padY),
                                w: r.width + padLeft + padRight,
                                h: r.height + padY * 2
                            });
                        }
                    } else {
                        const extraTop = tuckedNow ? 22 : 36;
                        const padX = tuckedNow ? 80 : 12;
                        const padB = tuckedNow ? 16 : 12;
                        rects.push({ x: r.left - padX, y: r.top - extraTop, w: r.width + padX * 2, h: r.height + extraTop + padB });
                    }
                }
            }

            for (const pop of [hub, fcard]) {
                if (pop && getComputedStyle(pop).display !== 'none') {
                    const hr = pop.getBoundingClientRect();
                    if (hr.width > 0 && hr.height > 0) {
                        if (isVert && dockRect) {
                            const leftX = edge === 'Left'
                                ? Math.min(hr.left - 8, dockRect.right - 4)
                                : (hr.left - 8);
                            const rightX = edge === 'Right'
                                ? Math.max(hr.right + 8, dockRect.left + 4)
                                : (hr.right + 8);
                            rects.push({
                                x: Math.max(0, leftX),
                                y: Math.max(0, hr.top - 8),
                                w: Math.max(hr.width + 16, rightX - leftX),
                                h: hr.height + 16
                            });
                        } else {
                            const bridgeBottom = dockRect ? Math.max(hr.bottom + 8, dockRect.top + 8) : (hr.bottom + 14);
                            rects.push({
                                x: hr.left - 8,
                                y: hr.top - 8,
                                w: hr.width + 16,
                                h: Math.max(hr.height + 16, bridgeBottom - hr.top + 8)
                            });
                        }
                    }
                }
            }

            window.chrome.webview.postMessage({
                type: 'hitRects',
                vw: window.innerWidth,
                vh: window.innerHeight,
                rects
            });
        }

        function classifyCategory(proc) {
            const p = (proc || '').toLowerCase();
            if (/edge|chrome|firefox|brave|opera|arc/.test(p)) return { id: 'cat-web', name: 'Web' };
            if (/code|devenv|antigravity|powershell|pwsh|windowsterminal|cmd|git|rider|idea|cursor/.test(p)) return { id: 'cat-dev', name: 'Geliştirme' };
            if (/spotify|music|vlc|discord|whatsapp|telegram|teams|slack/.test(p)) return { id: 'cat-media', name: 'İletişim' };
            return { id: 'cat-sys', name: 'Uygulama' };
        }

        function cleanWindowTitle(raw, appName) {
            let t = String(raw || '').trim();
            if (!t) return appName || 'Pencere';
            const suffixes = [
                ' - Opera', ' - Brave', ' - Google Chrome', ' - Microsoft Edge',
                ' — Mozilla Firefox', ' - Mozilla Firefox', ' - Visual Studio Code',
                ' - Microsoft Teams', ' - Dosya Gezgini', ' - File Explorer',
                ' - Notepad', ' - Not Defteri', ' - Windows PowerShell'
            ];
            if (appName) suffixes.unshift(' - ' + appName, ' — ' + appName);
            for (const sfx of suffixes) {
                if (t.length > sfx.length + 2 && t.toLowerCase().endsWith(sfx.toLowerCase())) {
                    t = t.slice(0, t.length - sfx.length).trim();
                    break;
                }
            }
            return t || appName || 'Pencere';
        }

        function shortOrbLabel(cleaned) {
            let s = String(cleaned || '').replace(/^\(\d+\)\s*/, '').trim();
            if (!s) return 'Pencere';
            if (s.includes('\\') && s.length > 14) {
                const segs = s.split('\\').filter(Boolean);
                if (segs.length) s = segs[segs.length - 1];
            }
            if (/^[a-z0-9.-]+\.[a-z]{2,}(\/|$)/i.test(s)) {
                s = s.split('/')[0].replace(/^www\./i, '').split('.')[0];
            }
            const words = s.split(/\s+/);
            if (words.length === 1 && s.length > 9) {
                return s.slice(0, 8) + '…';
            }
            return s.length > 16 ? s.slice(0, 15) + '…' : s;
        }

        function getSmartTidyBuckets(runningUnpinned) {
            const buckets = {};
            for (const g of runningUnpinned) {
                const cat = classifyCategory(g.windows[0]?.processName || g.displayName);
                if (!buckets[cat.id]) buckets[cat.id] = { ...cat, groups: [], windows: [], icons: [], hasActive: false };
                buckets[cat.id].groups.push(g);
                buckets[cat.id].windows.push(...g.windows);
                if (g.hasActive) buckets[cat.id].hasActive = true;
                if (g.icon && buckets[cat.id].icons.length < 4) buckets[cat.id].icons.push(g.icon);
            }
            return Object.values(buckets);
        }

        function renderDock() {
            if (!state) return;
            applyThemeVars(state.theme);

            const edge = getDockEdge();
            const isVert = isVerticalEdge();
            const edgeChanged = (lastRenderedEdge !== edge);
            lastRenderedEdge = edge;
            document.body.classList.toggle('edge-left', edge === 'Left');
            document.body.classList.toggle('edge-right', edge === 'Right');

            const dock = document.getElementById('dockBar');
            if (isDockTuckedNow()) {
                closeRadialHub(false);
                dock.className = 'glass-shell tucked';
                dock.innerHTML = '';
                applyDockGeometryAndOffset();
                dock.onmouseenter = () => { clearTimeout(autoHideTimer); manualTucked = false; autoHideRevealed = true; renderDock(); };
                dock.onmouseleave = null;
                dock.onclick = () => { clearTimeout(autoHideTimer); manualTucked = false; autoHideRevealed = true; renderDock(); };
                reportHitRects();
                requestAnimationFrame(reportHitRects);
                if (edgeChanged) { setTimeout(reportHitRects, 140); setTimeout(reportHitRects, 320); }
                return;
            }
            dock.onmouseenter = () => { clearTimeout(autoHideTimer); clearTimeout(radialCloseTimer); autoHideRevealed = true; };
            dock.onmouseleave = () => {
                mouseX = null;
                mouseY = null;
                applyParabolicWave();
                scheduleDockAutoHide(440);
            };
            dock.onclick = null;
            const densCls = state.dockDensity === 'Compact' ? ' density-compact' : (state.dockDensity === 'Cozy' ? ' density-cozy' : '');
            dock.className = 'glass-shell' + densCls;

            const groups = state.groups || [];
            const pinned = groups.filter(g => g.isPinned);
            const runningUnpinned = groups.filter(g => !g.isPinned && g.windows.length > 0);
            const compactMode = isVert || (window.innerWidth < 1050);

            let html = `
                <div class="search-pill" onclick="send('openOmni')">
                    ${SVG.search}
                    ${compactMode ? '' : '<span>Ara</span>'}
                    ${isVert ? '<span class="hover-label">Evrensel Arama</span>' : ''}
                </div>
                <div class="dock-sep"></div>
            `;

            for (let i = 0; i < pinned.length; i++) {
                html += buildDockIconHtml(pinned[i], i, null);
            }

            if (runningUnpinned.length > 0) {
                html += `<div class="dock-sep"></div>`;
                if (state.smartTidy) {
                    const buckets = getSmartTidyBuckets(runningUnpinned);
                    if (expandedAccordionCat && !buckets.some(b => b.id === expandedAccordionCat)) {
                        expandedAccordionCat = null;
                    }
                    for (const b of buckets) {
                        if (isVert && b.groups.length === 1) {
                            html += buildDockIconHtml(b.groups[0], -1, null);
                            continue;
                        }
                        const isOpen = (expandedAccordionCat === b.id) || (radialAnchorCatId === b.id && Boolean(radialGroupKey));
                        const vMosaicHtml = b.icons.length
                            ? `<div class="v-mosaic ${b.icons.length <= 2 ? 'pair' : ''}">${b.icons.slice(0, 4).map(ic => `<img src="data:image/png;base64,${ic}"/>`).join('')}</div>`
                            : SVG.layers;
                        const horizHeadHtml = `
                            <div class="stack-icons">
                                ${b.icons.length ? b.icons.slice(0, 3).map(ic => `<img src="data:image/png;base64,${ic}"/>`).join('') : SVG.layers}
                            </div>
                            ${compactMode ? '' : `<span>${escHtml(b.name)}</span>`}
                            <span class="acc-count num">${b.windows.length}</span>
                        `;
                        const vertHeadHtml = `
                            ${vMosaicHtml}
                            <span class="count-pill num">${b.windows.length}</span>
                            <span class="dock-dot"></span>
                            <span class="hover-label">${escHtml(b.name)} (${b.windows.length})</span>
                        `;
                        html += `
                            <div class="acc-bucket ${isOpen ? 'open' : ''} ${b.hasActive ? 'has-active' : ''}"
                                 data-cat-id="${b.id}"
                                 onmouseenter="scheduleAccordionOpen('${b.id}')"
                                 onmouseleave="scheduleAccordionClose('${b.id}')">
                                <div class="acc-head"
                                     onclick="handleCategoryClick('${b.id}')"
                                     onwheel="handleCategoryWheel(event,'${b.id}')">
                                    ${isVert ? vertHeadHtml : horizHeadHtml}
                                </div>
                                <div class="acc-drawer">
                                    ${b.groups.map(g => buildDockIconHtml(g, -1, b.id)).join('')}
                                </div>
                            </div>
                        `;
                    }
                } else {
                    expandedAccordionCat = null;
                    for (const g of runningUnpinned) {
                        html += buildDockIconHtml(g, -1, null);
                    }
                }
            }

            const dockFolders = state.dockFolders || [];
            if (dockFolders.length > 0 && (!compactMode || isVert)) {
                html += `<div class="dock-sep"></div>`;
                dockFolders.forEach((f, idx) => {
                    const isOpen = (openFolderPath && openFolderPath.toLowerCase() === f.path.toLowerCase());
                    html += `
                        <div class="folder-chip mag-target ${isOpen ? 'open' : ''}" data-folder-idx="${idx}"
                             onclick="toggleFolderStackByIdx(this, ${idx})"
                             oncontextmenu="event.preventDefault();openDockFolderNative(${idx});">
                            ${SVG.folder}
                            <span class="hover-label">${escHtml(f.name)}</span>
                        </div>
                    `;
                });
            }

            const trayCount = state.trayCount ?? (state.trayApps || []).length;
            const dockMode = state.dockMode || (state.dockAutoHide ? 'AutoHide' : 'AlwaysVisible');
            const pinCls = dockMode === 'AlwaysVisible' ? 'active' : (dockMode === 'SmartDodge' ? 'smart-dodge' : '');
            const pinLabel = dockMode === 'AlwaysVisible'
                ? 'Dock: Sabit (Tıkla → Akıllı Kaçınma)'
                : (dockMode === 'SmartDodge'
                    ? 'Dock: Akıllı Kaçınma (Tıkla → Oto-Gizle)'
                    : 'Dock: Oto-Gizle (Tıkla → Sabit)');
            html += `
                <div class="dock-sep"></div>
                <button class="pill-btn dock-v-btn num" onclick="send('openTray')" title="Sistem Tepsisi (${trayCount})">
                    ${SVG.tray}${isVert ? '' : `<span>${trayCount}</span>`}
                    ${isVert && trayCount > 0 ? `<span class="dock-v-badge num">${trayCount}</span>` : ''}
                    ${isVert ? `<span class="hover-label">Sistem Tepsisi (${trayCount})</span>` : ''}
                </button>
                <button class="pill-btn dock-v-btn ${state.smartTidy?'accent':''}" onclick="send('toggleSmartTidy')" title="Akıllı Derle">
                    ${SVG.sparkles}${compactMode ? '' : `<span>${state.smartTidy ? 'Derli' : 'Derle'}</span>`}
                    ${isVert ? `<span class="hover-label">Akıllı Derle: ${state.smartTidy ? 'Açık' : 'Kapalı'}</span>` : ''}
                </button>
                <button class="micro-btn dock-pin-btn ${pinCls}" onclick="toggleDockPin()">
                    ${SVG.pin}
                    <span class="hover-label">${pinLabel}</span>
                </button>
            `;

            dock.innerHTML = html;
            applyDockGeometryAndOffset();

            if (radialGroupKey) {
                const anchorEl = dock.querySelector(`[data-group-key="${CSS.escape(radialGroupKey)}"]`);
                if (anchorEl) {
                    const ar = anchorEl.getBoundingClientRect();
                    if (ar.width > 0) {
                        radialAnchorX = ar.left + ar.width / 2;
                        radialAnchorY = ar.top + ar.height / 2;
                    }
                }
            }
            renderRadialHub(false);
            renderFolderStackCard();
            reportHitRects();
            requestAnimationFrame(reportHitRects);
            if (edgeChanged) { setTimeout(reportHitRects, 140); setTimeout(reportHitRects, 320); }
        }

        function toggleDockPin() {
            manualTucked = false;
            autoHideRevealed = true;
            closeRadialHub(false);
            closeFolderStack(false);
            send('cycleDockVisibilityMode');
            scheduleDockAutoHide(900);
        }

        function scheduleDockAutoHide(delayMs = 440) {
            clearTimeout(autoHideTimer);
            autoHideTimer = setTimeout(() => {
                const mode = state?.dockMode || (state?.dockAutoHide ? 'AutoHide' : 'AlwaysVisible');
                const shouldHide = (mode === 'AutoHide') ||
                    (mode === 'SmartDodge' && ((liveDodgedOverride !== null) ? liveDodgedOverride : Boolean(state?.dockDodged)));
                const pointerOutside = isVerticalEdge() ? (mouseY === null) : (mouseX === null);
                if (shouldHide && !pointerInRadial && !radialGroupKey && !pointerInFolder && !openFolderPath && !isDraggingFileOverDock && pointerOutside) {
                    autoHideRevealed = false;
                    renderDock();
                }
            }, delayMs);
        }

        function buildDockIconHtml(g, pinIndex, catId) {
            const isAnchored = (radialGroupKey === g.key);
            const hasAttn = Boolean(g.hasAttention);
            const prog = Number(g.progress || 0);
            const hasProg = prog > 0 && prog < 100;
            const cls = `dock-item mag-target ${g.hasActive ? 'active' : (g.windows.length > 0 ? 'running' : '')} ${isAnchored ? 'radial-anchored' : ''} ${hasAttn ? 'attention' : ''}`;
            const safeKey = escHtml(g.key);
            const safeCat = catId ? `'${escHtml(catId)}'` : 'null';
            const monogram = escHtml((g.displayName || 'A').slice(0, 1).toUpperCase());
            const pinAttrs = pinIndex >= 0
                ? `data-pin-id="${safeKey}" data-pin-idx="${pinIndex}" onpointerdown="startPinDrag(event,'${safeKey}',${pinIndex})"`
                : '';
            const singleSub = g.windows.length === 1
                ? cleanWindowTitle(g.windows[0].title, g.displayName)
                : '';
            const labelText = g.windows.length > 1
                ? `${g.displayName} (${g.windows.length})`
                : (singleSub && singleSub.toLowerCase() !== g.displayName.toLowerCase() ? `${g.displayName} · ${shortOrbLabel(singleSub)}` : g.displayName);

            const progOffset = hasProg ? (152 * (1 - prog / 100)).toFixed(1) : '152';
            const progSvg = hasProg ? `
                <svg class="dock-prog-svg" viewBox="0 0 46 46">
                    <rect x="2" y="2" width="42" height="42" rx="12" fill="none" stroke="rgba(52,211,153,0.25)" stroke-width="2.2"/>
                    <rect x="2" y="2" width="42" height="42" rx="12" fill="none" stroke="var(--success)" stroke-width="2.4"
                          stroke-dasharray="152" stroke-dashoffset="${progOffset}" stroke-linecap="round"/>
                </svg>
                <span class="prog-pill num">${prog}%</span>
            ` : '';

            return `
                <div class="${cls}" data-group-key="${safeKey}" ${pinAttrs}
                     onmouseenter="onDockItemEnter(this,'${safeKey}',${g.windows.length},${safeCat})"
                     onmouseleave="onDockItemLeave('${safeKey}')"
                     ondragover="onDockAppDragOver(event,this,'${safeKey}')"
                     ondragleave="onDockAppDragLeave(this)"
                     ondrop="onDockAppDrop(event,this,'${safeKey}')"
                     onwheel="handleGroupWheel(event,'${safeKey}')"
                     onauxclick="if(event.button===1 && ${g.windows.length}>0){send('closeWindow',{hwnd:${g.windows[0]?.hwnd || 0}});}"
                     onclick="handleGroupClick(event,'${safeKey}')"
                     oncontextmenu="event.preventDefault();openRadialForElement(this,'${safeKey}',${safeCat},true);">
                    ${progSvg}
                    ${hasAttn ? `<span class="attn-pip"></span>` : ''}
                    <span class="hover-label">${escHtml(labelText)}</span>
                    ${g.icon ? `<img src="data:image/png;base64,${g.icon}"/>` : `<div class="fallback-badge">${monogram}</div>`}
                    ${g.windows.length > 1 ? `<span class="count-pill num">${g.windows.length}</span>` : ''}
                    ${g.windows.length > 0 ? `<span class="dock-dot"></span>` : ''}
                </div>
            `;
        }

        function onDockAppDragOver(e, el, key) {
            e.preventDefault();
            e.stopPropagation();
            if (!el.classList.contains('drag-target')) {
                document.querySelectorAll('.dock-item.drag-target').forEach(x => x.classList.remove('drag-target'));
                el.classList.add('drag-target');
                clearTimeout(dragFocusTimer);
                dragFocusTimer = setTimeout(() => {
                    send('focusGroupForDrop', { key });
                }, 420);
            }
        }

        function onDockAppDragLeave(el) {
            el.classList.remove('drag-target');
            clearTimeout(dragFocusTimer);
        }

        function onDockAppDrop(e, el, key) {
            e.preventDefault();
            e.stopPropagation();
            el.classList.remove('drag-target');
            clearTimeout(dragFocusTimer);
            isDraggingFileOverDock = false;
            if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0 && window.chrome.webview.postMessageWithAdditionalObjects) {
                window.chrome.webview.postMessageWithAdditionalObjects({ action: 'dropOnDockApp', key }, Array.from(e.dataTransfer.files));
            }
        }

        function toggleFolderStackByIdx(el, idx) {
            const f = (state?.dockFolders || [])[idx];
            if (!f) return;
            closeRadialHub(false);
            if (openFolderPath && openFolderPath.toLowerCase() === f.path.toLowerCase()) {
                closeFolderStack(true);
                return;
            }
            const r = el.getBoundingClientRect();
            openFolderAnchorX = r.left + r.width / 2;
            openFolderAnchorY = r.top + r.height / 2;
            openFolderPath = f.path;
            send('requestFolderStack', { path: f.path });
            renderDock();
        }

        function openDockFolderNative(idx) {
            const f = (state?.dockFolders || [])[idx];
            if (f) send('launchPath', { path: f.path });
        }

        function closeFolderStack(updateRects = true) {
            clearTimeout(folderCloseTimer);
            openFolderPath = null;
            pointerInFolder = false;
            const card = document.getElementById('folderStackCard');
            if (card) {
                card.className = 'glass-shell';
                card.innerHTML = '';
            }
            document.querySelectorAll('.folder-chip.open').forEach(x => x.classList.remove('open'));
            if (updateRects) requestAnimationFrame(reportHitRects);
        }

        function renderFolderStackCard() {
            const card = document.getElementById('folderStackCard');
            if (!openFolderPath || !state) {
                card.className = 'glass-shell';
                return;
            }
            const folderInfo = (state.dockFolders || []).find(f => f.path.toLowerCase() === openFolderPath.toLowerCase());
            const folderName = folderInfo?.name || 'Klasör';
            const data = folderStackCache[openFolderPath.toLowerCase()];
            const items = data?.items || [];

            const cardW = 288;
            const edge = getDockEdge();
            const dockR = document.getElementById('dockBar')?.getBoundingClientRect();
            if (isVerticalEdge() && dockR) {
                const rawY = openFolderAnchorY ?? (window.innerHeight / 2);
                const clampedTop = Math.max(10, Math.min(window.innerHeight - 320, rawY - 150));
                card.style.bottom = 'auto';
                card.style.top = `${clampedTop.toFixed(1)}px`;
                const leftPos = edge === 'Left'
                    ? (dockR.right + 12)
                    : Math.max(8, dockR.left - cardW - 12);
                card.style.left = `${leftPos.toFixed(1)}px`;
            } else {
                const rawX = openFolderAnchorX ?? (window.innerWidth / 2);
                const clampedLeft = Math.max(10, Math.min(window.innerWidth - cardW - 10, rawX - cardW / 2));
                card.style.top = 'auto';
                card.style.bottom = '68px';
                card.style.left = `${clampedLeft.toFixed(1)}px`;
            }
            card.className = 'glass-shell open';

            card.innerHTML = `
                <div style="display:flex;align-items:center;justify-content:space-between;gap:6px;">
                    <span style="display:inline-flex;align-items:center;gap:6px;font-size:12.5px;font-weight:800;color:var(--text-1);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">
                        ${SVG.folder}<span>${escHtml(folderName)}</span>
                    </span>
                    <div style="display:flex;gap:4px;flex-shrink:0;">
                        <button class="micro-btn" onclick="openCurrentFolderStack()" title="Gezginde Aç">${SVG.desktop}</button>
                        <button class="micro-btn" onclick="send('pickDockFolder');closeFolderStack(true);" title="Yeni Klasör Ekle">${SVG.plus}</button>
                        <button class="micro-btn danger" onclick="removeCurrentFolderStack()" title="Dock'tan Kaldır">${SVG.trash}</button>
                        <button class="micro-btn" onclick="closeFolderStack(true)">${SVG.close}</button>
                    </div>
                </div>
                ${!data ? `<div style="padding:14px;text-align:center;font-size:11.5px;color:var(--text-2);">Yükleniyor…</div>` :
                  items.length === 0 ? `<div style="padding:14px;text-align:center;font-size:11.5px;color:var(--text-2);">Bu klasör şu an boş.</div>` : `
                    <div class="fstack-list">
                        ${items.map((it, idx) => `
                            <div class="fstack-row" onclick="openFolderStackItem(${idx})">
                                ${it.icon ? `<img src="data:image/png;base64,${it.icon}"/>` : `<span style="width:20px;display:inline-flex;justify-content:center;color:var(--accent-1);">${it.isDir ? SVG.folder : SVG.note}</span>`}
                                <div style="flex:1;min-width:0;">
                                    <div style="font-size:12px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(it.name)}</div>
                                    <div style="font-size:10px;color:var(--text-2);" class="num">${escHtml(it.size)}${it.mod ? ' · ' + escHtml(it.mod) : ''}</div>
                                </div>
                                <button class="micro-btn" style="width:22px;height:22px;" onclick="event.stopPropagation();revealFolderStackItem(${idx});" title="Klasörde Göster">${SVG.folder}</button>
                            </div>
                        `).join('')}
                    </div>
                `}
            `;
        }

        function openCurrentFolderStack() {
            if (!openFolderPath) return;
            const p = openFolderPath;
            closeFolderStack(true);
            send('launchPath', { path: p });
        }

        function removeCurrentFolderStack() {
            if (!openFolderPath) return;
            const p = openFolderPath;
            closeFolderStack(true);
            send('removeDockFolder', { path: p });
        }

        function openFolderStackItem(idx) {
            if (!openFolderPath) return;
            const items = folderStackCache[openFolderPath.toLowerCase()]?.items || [];
            const it = items[idx];
            if (!it) return;
            closeFolderStack(true);
            send('launchPath', { path: it.path });
        }

        function revealFolderStackItem(idx) {
            if (!openFolderPath) return;
            const items = folderStackCache[openFolderPath.toLowerCase()]?.items || [];
            const it = items[idx];
            if (!it) return;
            closeFolderStack(true);
            send('launchPath', { path: 'reveal:' + it.path });
        }

        function scheduleAccordionOpen(catId) {
            clearTimeout(accCloseTimer);
            clearTimeout(accOpenTimer);
            if (expandedAccordionCat === catId) return;
            const delay = expandedAccordionCat ? 95 : 175;
            accOpenTimer = setTimeout(() => openAccordionCat(catId), delay);
        }

        function openAccordionCat(catId) {
            clearTimeout(accOpenTimer);
            clearTimeout(accCloseTimer);
            clearTimeout(radialCloseTimer);
            if (expandedAccordionCat !== catId) {
                expandedAccordionCat = catId;
                renderDock();
                setTimeout(reportHitRects, 210);
            }
        }

        function scheduleAccordionClose(catId) {
            clearTimeout(accOpenTimer);
            clearTimeout(accCloseTimer);
            accCloseTimer = setTimeout(() => {
                if (pointerInRadial || (radialGroupKey && radialAnchorCatId === catId)) return;
                if (expandedAccordionCat === catId) {
                    expandedAccordionCat = null;
                    renderDock();
                }
            }, 340);
        }

        function handleCategoryClick(catId) {
            const runningUnpinned = (state?.groups || []).filter(g => !g.isPinned && g.windows.length > 0);
            const b = getSmartTidyBuckets(runningUnpinned).find(x => x.id === catId);
            if (!b || b.windows.length === 0) return;
            send('cycleCategoryWindows', { hwnds: b.windows.map(w => w.hwnd), delta: 1, fromWheel: false });
        }

        function handleCategoryWheel(e, catId) {
            e.preventDefault();
            e.stopPropagation();
            const runningUnpinned = (state?.groups || []).filter(g => !g.isPinned && g.windows.length > 0);
            const b = getSmartTidyBuckets(runningUnpinned).find(x => x.id === catId);
            if (!b || b.windows.length === 0) return;
            send('cycleCategoryWindows', { hwnds: b.windows.map(w => w.hwnd), delta: e.deltaY > 0 ? 1 : -1, fromWheel: true });
        }

        function onDockItemEnter(el, key, winCount, catId) {
            clearTimeout(hoverRadialTimer);
            if (catId) clearTimeout(accCloseTimer);
            if (radialGroupKey === key) {
                clearTimeout(radialCloseTimer);
                return;
            }
            if (winCount > 1 && !dragPinState) {
                const delay = radialGroupKey ? 90 : 145;
                hoverRadialTimer = setTimeout(() => {
                    if (!dragPinState) openRadialForElement(el, key, catId, false);
                }, delay);
            } else if (radialGroupKey && !pointerInRadial) {
                scheduleRadialClose(220);
            }
        }

        function onDockItemLeave(key) {
            clearTimeout(hoverRadialTimer);
            if (radialGroupKey === key) {
                scheduleRadialClose(320);
            }
        }

        function openRadialForElement(el, key, catId, isToggleIfSame) {
            clearTimeout(hoverRadialTimer);
            clearTimeout(radialCloseTimer);
            closeFolderStack(false);
            if (isToggleIfSame && radialGroupKey === key) {
                closeRadialHub(true);
                return;
            }
            const r = el.getBoundingClientRect();
            radialAnchorX = r.left + r.width / 2;
            radialAnchorY = r.top + r.height / 2;
            radialGroupKey = key;
            radialAnchorCatId = catId || null;

            const g = (state?.groups || []).find(x => x.key === key);
            if (g && g.windows.length > 0) {
                const act = g.windows.find(w => w.isActive) || g.windows[0];
                radialSelectedHwnd = act.hwnd;
            } else {
                radialSelectedHwnd = null;
            }

            syncDockAnchorClasses();
            renderRadialHub(true);
            requestAnimationFrame(reportHitRects);
        }

        function syncDockAnchorClasses() {
            document.querySelectorAll('.dock-item.radial-anchored').forEach(x => x.classList.remove('radial-anchored'));
            if (radialGroupKey) {
                const el = document.querySelector(`.dock-item[data-group-key="${CSS.escape(radialGroupKey)}"]`);
                if (el) el.classList.add('radial-anchored');
            }
        }

        function scheduleRadialClose(delayMs = 320) {
            clearTimeout(radialCloseTimer);
            radialCloseTimer = setTimeout(() => {
                if (!pointerInRadial) {
                    closeRadialHub(true);
                    const pointerOutside = isVerticalEdge() ? (mouseY === null) : (mouseX === null);
                    if (expandedAccordionCat && pointerOutside) {
                        expandedAccordionCat = null;
                        renderDock();
                    }
                }
            }, delayMs);
        }

        function hideDwmPreviewNow() {
            clearTimeout(dwmPreviewTimer);
            if (dwmPreviewActiveHwnd !== null) {
                dwmPreviewActiveHwnd = null;
                send('hideDwmPreview');
            }
        }

        function scheduleDwmPreview(hwnd, immediate = false) {
            clearTimeout(dwmPreviewTimer);
            if (!hwnd || state?.dwmLivePreview === false || !radialGroupKey) {
                hideDwmPreviewNow();
                return;
            }
            const delay = (immediate || dwmPreviewActiveHwnd !== null) ? 45 : 220;
            dwmPreviewTimer = setTimeout(() => {
                if (!radialGroupKey || !pointerInRadial) return;
                const hub = document.getElementById('radialHub');
                if (!hub) return;
                const r = hub.getBoundingClientRect();
                if (r.width <= 0 || r.height <= 0) return;
                dwmPreviewActiveHwnd = hwnd;
                send('showDwmPreview', {
                    hwnd,
                    hubLeft: r.left,
                    hubTop: r.top,
                    hubWidth: r.width,
                    hubHeight: r.height,
                    vw: window.innerWidth,
                    vh: window.innerHeight
                });
            }, delay);
        }

        function closeRadialHub(updateHitRects = true) {
            clearTimeout(hoverRadialTimer);
            clearTimeout(radialCloseTimer);
            clearTimeout(orbTargetTimer);
            hideDwmPreviewNow();
            radialGroupKey = null;
            radialLastOpenedKey = null;
            radialSelectedHwnd = null;
            radialAnchorCatId = null;
            pointerInRadial = false;
            const hub = document.getElementById('radialHub');
            if (hub) {
                hub.className = '';
                hub.innerHTML = '';
            }
            syncDockAnchorClasses();
            if (updateHitRects) requestAnimationFrame(reportHitRects);
        }

        function handleGroupClick(e, key) {
            clearTimeout(hoverRadialTimer);
            if (window.__suppressNextDockClick && Date.now() < window.__suppressNextDockClick) return;
            const g = (state?.groups || []).find(x => x.key === key);
            if (!g) return;
            closeRadialHub(false);
            closeFolderStack(false);
            if (e && e.shiftKey) {
                send('launchNewInstance', { key });
                return;
            }
            if (g.windows.length === 0) {
                send('invokeGroup', { key });
            } else {
                send('cycleGroupWindow', { key, delta: 1, fromWheel: false });
            }
        }

        function handleGroupWheel(e, key) {
            const g = (state?.groups || []).find(x => x.key === key);
            if (!g || g.windows.length === 0) return;
            e.preventDefault();
            e.stopPropagation();
            const delta = e.deltaY > 0 ? 1 : -1;
            send('cycleGroupWindow', { key, delta, fromWheel: true });
            if (radialGroupKey === key && g.windows.length > 1) {
                const idx = Math.max(0, g.windows.findIndex(w => w.hwnd === radialSelectedHwnd));
                const next = g.windows[(idx + (delta > 0 ? 1 : -1) + g.windows.length) % g.windows.length];
                if (next) setTargetedOrbImmediate(next.hwnd);
            }
        }

        function computeOrbLayout(count, cx, cy) {
            if (count <= 1) return [{ x: cx, y: cy, size: 72, cls: 'orb-lg' }];
            if (count === 2) return [
                { x: cx - 41, y: cy, size: 60, cls: '' },
                { x: cx + 41, y: cy, size: 60, cls: '' }
            ];
            const radius = count === 3 ? 46 : (count === 4 ? 49 : (count === 5 ? 52 : 54));
            const size = count === 3 ? 56 : (count === 4 ? 50 : (count === 5 ? 46 : 43));
            const cls = count >= 5 ? 'orb-sm' : '';
            const startDeg = count === 4 ? -45 : 0;
            const coords = [];
            for (let i = 0; i < count; i++) {
                const rad = ((startDeg + (360 / count) * i) * Math.PI) / 180;
                coords.push({
                    x: cx + radius * Math.sin(rad),
                    y: cy - radius * Math.cos(rad),
                    size,
                    cls
                });
            }
            return coords;
        }

        function polar(cx, cy, r, deg) {
            const rad = (deg * Math.PI) / 180;
            return { x: cx + r * Math.sin(rad), y: cy - r * Math.cos(rad) };
        }

        function renderRadialHub(forceAnimate) {
            const hub = document.getElementById('radialHub');
            if (!radialGroupKey || !state) {
                hub.className = '';
                return;
            }

            const g = (state.groups || []).find(x => x.key === radialGroupKey);
            if (!g) {
                closeRadialHub(true);
                return;
            }

            const wins = (g.windows || []).slice(0, 6);
            if (wins.length > 0 && !wins.some(w => w.hwnd === radialSelectedHwnd)) {
                const activeW = wins.find(w => w.isActive) || wins[0];
                radialSelectedHwnd = activeW.hwnd;
            }
            const targetWin = wins.find(w => w.hwnd === radialSelectedHwnd) || wins[0] || null;
            const targetIdx = targetWin ? wins.findIndex(w => w.hwnd === targetWin.hwnd) : -1;

            const hubW = 296;
            const hubH = 304;
            const edge = getDockEdge();
            const dockR = document.getElementById('dockBar')?.getBoundingClientRect();
            let stemX = hubW / 2;
            if (isVerticalEdge() && dockR) {
                const rawY = radialAnchorY ?? (window.innerHeight / 2);
                const clampedTop = Math.max(8, Math.min(window.innerHeight - hubH - 8, rawY - hubH / 2));
                const leftPos = edge === 'Left'
                    ? Math.min(window.innerWidth - hubW - 8, dockR.right + 12)
                    : Math.max(8, dockR.left - hubW - 12);
                hub.style.bottom = 'auto';
                hub.style.top = `${clampedTop.toFixed(1)}px`;
                hub.style.left = `${leftPos.toFixed(1)}px`;
            } else {
                const rawAnchorX = (radialAnchorX ?? (window.innerWidth / 2));
                const clampedLeft = Math.max(10, Math.min(window.innerWidth - hubW - 10, rawAnchorX - hubW / 2));
                stemX = Math.max(26, Math.min(hubW - 26, rawAnchorX - clampedLeft));
                hub.style.top = 'auto';
                hub.style.bottom = '64px';
                hub.style.left = `${clampedLeft.toFixed(1)}px`;
            }

            const shouldAnimate = forceAnimate || (radialLastOpenedKey !== radialGroupKey);
            radialLastOpenedKey = radialGroupKey;
            hub.className = shouldAnimate ? 'open animate-in' : 'open';

            const cx = 142, cy = 120;
            const orbCoords = computeOrbLayout(wins.length, cx, cy);

            const capText = targetWin
                ? cleanWindowTitle(targetWin.title, g.displayName)
                : `${g.displayName} • Başlat`;
            const capBadge = wins.length > 0 ? `${targetIdx + 1}/${g.windows.length}` : 'Sabitli';

            // SVG orbital track + precision spokes stopping cleanly at each orb rim (zero bleed behind icons!)
            let svgLines = `<circle cx="${cx}" cy="${cy}" r="110" fill="none" stroke="var(--radial-ring-stroke)" stroke-width="1.15" stroke-dasharray="3 5" opacity="${wins.length > 0 ? '0.85' : '0.35'}"/>`;
            if (wins.length >= 2) {
                wins.forEach((w, idx) => {
                    const p = orbCoords[idx];
                    const dx = p.x - cx, dy = p.y - cy;
                    const dist = Math.hypot(dx, dy) || 1;
                    const ux = dx / dist, uy = dy / dist;
                    const startR = 6;
                    const endR = Math.max(startR + 2, dist - (p.size / 2) - 2);
                    const x1 = cx + ux * startR, y1 = cy + uy * startR;
                    const x2 = cx + ux * endR, y2 = cy + uy * endR;
                    const isSel = w.hwnd === radialSelectedHwnd;
                    svgLines += `<line data-spoke-hwnd="${w.hwnd}" x1="${x1.toFixed(1)}" y1="${y1.toFixed(1)}" x2="${x2.toFixed(1)}" y2="${y2.toFixed(1)}" stroke="${isSel ? 'var(--accent-1)' : 'var(--radial-ring-stroke)'}" stroke-width="${isSel ? '2.2' : '1.2'}" stroke-linecap="round" opacity="${isSel ? '0.95' : '0.50'}"/>`;
                });
                svgLines += `<circle cx="${cx}" cy="${cy}" r="4.5" fill="var(--accent-1)" opacity="0.92"/>`;
            }

            // Inner circular window orbs
            let orbsHtml = '';
            if (wins.length === 0) {
                const monogram = escHtml((g.displayName || 'A').slice(0, 1).toUpperCase());
                orbsHtml = `
                    <div class="win-orb orb-lg targeted" style="left:${cx}px;top:${cy}px;width:78px;height:78px;"
                         onclick="launchRadialGroup()">
                        ${g.icon ? `<img src="data:image/png;base64,${g.icon}"/>` : `<span style="font-weight:800;color:var(--accent-1);">${monogram}</span>`}
                        <span class="win-orb-lbl">Başlat</span>
                    </div>
                `;
            } else {
                orbsHtml = wins.map((w, idx) => {
                    const p = orbCoords[idx];
                    const clean = cleanWindowTitle(w.title, g.displayName);
                    const lbl = shortOrbLabel(clean);
                    const isSel = (w.hwnd === radialSelectedHwnd);
                    const cls = `win-orb ${p.cls} ${isSel ? 'targeted' : ''} ${w.isActive ? 'active-win' : ''}`;
                    return `
                        <div class="${cls}" data-orb-hwnd="${w.hwnd}"
                             style="left:${p.x.toFixed(1)}px;top:${p.y.toFixed(1)}px;width:${p.size}px;height:${p.size}px;"
                             onmouseenter="scheduleTargetOrb(${w.hwnd})"
                             onclick="send('focusWindow',{hwnd:${w.hwnd}});closeRadialHub(true);"
                             onauxclick="if(event.button===1){event.stopPropagation();send('closeWindow',{hwnd:${w.hwnd}});}">
                            ${g.icon ? `<img src="data:image/png;base64,${g.icon}"/>` : ''}
                            <span class="win-orb-lbl">${escHtml(lbl)}</span>
                            ${wins.length > 1 ? `<span class="win-orb-idx num">${idx + 1}</span>` : ''}
                        </div>
                    `;
                }).join('');
            }

            // Outer Orbital Feature Icons around the ring perimeter!
            let orbitHtml = '';
            if (wins.length > 0 && targetWin) {
                const features = [
                    { id: 'center', deg: -54, icon: SVG.snapC, label: 'Zen Ortala %75', act: 'arrange', mode: 'center', cls: '' },
                    { id: 'left',   deg: -88, icon: SVG.snapL, label: 'Sola Yasla %50', act: 'arrange', mode: 'left',   cls: '' },
                    { id: 'min',    deg: -122, icon: SVG.min,  label: 'Küçült',         act: 'min',     mode: '',       cls: '' },
                    { id: 'top',    deg: 54,  icon: SVG.pin,   label: 'Her Zaman Üstte', act: 'top',    mode: '',       cls: targetWin.isTopMost ? 'active' : '' },
                    { id: 'right',  deg: 88,  icon: SVG.snapR, label: 'Sağa Yasla %50', act: 'arrange', mode: 'right',  cls: '' },
                    { id: 'close',  deg: 122, icon: SVG.close, label: 'Pencereyi Kapat', act: 'close',  mode: '',       cls: 'danger' }
                ];
                orbitHtml = features.map(f => {
                    const pt = polar(cx, cy, 110, f.deg);
                    return `
                        <button class="orbit-btn ${f.cls}" data-orbit-id="${f.id}"
                                style="left:${pt.x.toFixed(1)}px;top:${pt.y.toFixed(1)}px;"
                                onmouseenter="previewOrbitCaption('${escHtml(f.label)}', ${f.id === 'close'})"
                                onmouseleave="restoreOrbCaption()"
                                onclick="execOrbitAction('${f.act}','${f.mode}')">
                            ${f.icon}
                        </button>
                    `;
                }).join('');
            }

            const overflowTxt = g.windows.length > 6 ? ` · +${g.windows.length - 6}` : '';
            hub.innerHTML = `
                <div id="radialCaption" class="radial-caption">
                    <span class="radial-cap-dot"></span>
                    <span id="radialCaptionText" class="radial-cap-text">${escHtml(capText)}</span>
                    <span id="radialCaptionBadge" class="radial-cap-badge num">${escHtml(capBadge + overflowTxt)}</span>
                </div>
                <div class="radial-dial">
                    <div class="radial-disc"></div>
                    <svg class="radial-svg" viewBox="0 0 284 248">${svgLines}</svg>
                    ${orbsHtml}
                    ${orbitHtml}
                    <div class="radial-bottom-row">
                        <button class="radial-bottom-pill ${g.isPinned ? 'pinned' : ''}"
                                onmouseenter="previewPinCaption()"
                                onmouseleave="restoreOrbCaption()"
                                onclick="togglePinRadial()">
                            ${SVG.pin}<span>${g.isPinned ? 'Sabitli' : 'Sabitle'}</span>
                        </button>
                        <button class="radial-bottom-pill"
                                onmouseenter="previewOrbitCaption('Yeni Pencere Aç (Shift+Tık)', false)"
                                onmouseleave="restoreOrbCaption()"
                                onclick="launchNewRadialInstance()">
                            ${SVG.plus}<span>Yeni</span>
                        </button>
                        ${wins.length > 1 ? `
                        <button class="radial-bottom-pill danger"
                                onmouseenter="previewOrbitCaption('Tüm Pencereleri Kapat', true)"
                                onmouseleave="restoreOrbCaption()"
                                onclick="closeAllRadialWindows()">
                            ${SVG.close}
                        </button>` : ''}
                    </div>
                </div>
                <div class="radial-stem-pip" style="left:${stemX.toFixed(1)}px;"></div>
            `;
        }

        function launchNewRadialInstance() {
            if (!radialGroupKey) return;
            const k = radialGroupKey;
            closeRadialHub(true);
            send('launchNewInstance', { key: k });
        }

        function closeAllRadialWindows() {
            if (!radialGroupKey) return;
            const k = radialGroupKey;
            closeRadialHub(true);
            send('closeAllGroupWindows', { key: k });
        }

        function launchRadialGroup() {
            if (!radialGroupKey) return;
            const k = radialGroupKey;
            closeRadialHub(true);
            send('invokeGroup', { key: k });
        }

        function previewPinCaption() {
            const g = (state?.groups || []).find(x => x.key === radialGroupKey);
            if (!g) return;
            previewOrbitCaption(g.isPinned ? 'Dock Sabitini Kaldır' : 'Dock Sabitle', false);
        }

        function togglePinRadial() {
            const g = (state?.groups || []).find(x => x.key === radialGroupKey);
            if (!g) return;
            send('togglePin', { name: g.displayName, path: g.executablePath });
            closeRadialHub(true);
        }

        function scheduleTargetOrb(hwnd) {
            clearTimeout(orbTargetTimer);
            if (radialSelectedHwnd === hwnd) {
                restoreOrbCaption();
                scheduleDwmPreview(hwnd, false);
                return;
            }
            orbTargetTimer = setTimeout(() => {
                setTargetedOrbImmediate(hwnd);
                scheduleDwmPreview(hwnd, false);
            }, 55);
        }

        function setTargetedOrbImmediate(hwnd) {
            radialSelectedHwnd = hwnd;
            document.querySelectorAll('.win-orb').forEach(el => {
                const h = Number(el.getAttribute('data-orb-hwnd'));
                el.classList.toggle('targeted', h === hwnd);
            });
            document.querySelectorAll('[data-spoke-hwnd]').forEach(line => {
                const h = Number(line.getAttribute('data-spoke-hwnd'));
                const sel = (h === hwnd);
                line.setAttribute('stroke', sel ? 'var(--accent-1)' : 'var(--radial-ring-stroke)');
                line.setAttribute('stroke-width', sel ? '2' : '1.1');
                line.setAttribute('opacity', sel ? '0.92' : '0.45');
            });

            const g = (state?.groups || []).find(x => x.key === radialGroupKey);
            const w = g?.windows?.find(x => x.hwnd === hwnd);
            const topBtn = document.querySelector('[data-orbit-id="top"]');
            if (topBtn && w) topBtn.classList.toggle('active', Boolean(w.isTopMost));
            restoreOrbCaption();
        }

        function previewOrbitCaption(actionLabel, isDanger) {
            const cap = document.getElementById('radialCaption');
            const txt = document.getElementById('radialCaptionText');
            if (!cap || !txt) return;
            cap.classList.toggle('danger', Boolean(isDanger));
            const g = (state?.groups || []).find(x => x.key === radialGroupKey);
            const w = g?.windows?.find(x => x.hwnd === radialSelectedHwnd);
            const shortWin = w ? shortOrbLabel(cleanWindowTitle(w.title, g.displayName)) : (g?.displayName || '');
            txt.textContent = shortWin ? `${actionLabel} · ${shortWin}` : actionLabel;
        }

        function restoreOrbCaption() {
            const cap = document.getElementById('radialCaption');
            const txt = document.getElementById('radialCaptionText');
            const badge = document.getElementById('radialCaptionBadge');
            if (!cap || !txt) return;
            cap.classList.remove('danger');
            const g = (state?.groups || []).find(x => x.key === radialGroupKey);
            if (!g) return;
            const wins = g.windows || [];
            const idx = wins.findIndex(x => x.hwnd === radialSelectedHwnd);
            const w = idx >= 0 ? wins[idx] : wins[0];
            txt.textContent = w ? cleanWindowTitle(w.title, g.displayName) : `${g.displayName} • Başlat`;
            if (badge && wins.length > 0) {
                badge.textContent = `${Math.max(1, idx + 1)}/${wins.length}`;
            }
        }

        function execOrbitAction(act, mode) {
            const g = (state?.groups || []).find(x => x.key === radialGroupKey);
            const hwnd = radialSelectedHwnd || g?.windows?.[0]?.hwnd;
            if (!hwnd) return;
            if (act === 'arrange') {
                send('arrangeWindow', { hwnd, mode });
                closeRadialHub(true);
            } else if (act === 'min') {
                send('minimizeWindow', { hwnd });
                closeRadialHub(true);
            } else if (act === 'top') {
                send('toggleTopMost', { hwnd });
            } else if (act === 'close') {
                send('closeWindow', { hwnd });
                if ((g?.windows?.length || 0) <= 1) closeRadialHub(true);
            }
        }

        function startPinDrag(e, pinId, startIdx) {
            if (e.button !== 0 || e.altKey) return;
            dragPinState = { pinId, startIdx, targetIdx: startIdx, startX: e.clientX, startY: e.clientY, moved: false };
        }

        document.addEventListener('pointermove', e => {
            if (!dragPinState) return;
            const isVert = isVerticalEdge();
            const delta = isVert ? Math.abs(e.clientY - dragPinState.startY) : Math.abs(e.clientX - dragPinState.startX);
            if (delta > 12) {
                dragPinState.moved = true;
                clearTimeout(hoverRadialTimer);
                closeRadialHub(false);
                const pinEls = Array.from(document.querySelectorAll('[data-pin-idx]'));
                let bestIdx = dragPinState.startIdx;
                let bestDist = Infinity;
                pinEls.forEach(el => {
                    el.style.outline = 'none';
                    const r = el.getBoundingClientRect();
                    const d = isVert
                        ? Math.abs(e.clientY - (r.top + r.height / 2))
                        : Math.abs(e.clientX - (r.left + r.width / 2));
                    if (d < bestDist) {
                        bestDist = d;
                        bestIdx = parseInt(el.getAttribute('data-pin-idx'), 10);
                    }
                });
                dragPinState.targetIdx = bestIdx;
                const targetEl = pinEls.find(el => parseInt(el.getAttribute('data-pin-idx'), 10) === bestIdx);
                if (targetEl) targetEl.style.outline = '2px dashed var(--accent-1)';
            }
        });

        document.addEventListener('pointerup', () => {
            if (!dragPinState) return;
            const st = dragPinState;
            dragPinState = null;
            document.querySelectorAll('[data-pin-idx]').forEach(el => el.style.outline = 'none');
            if (st.moved && st.targetIdx !== st.startIdx) {
                window.__suppressNextDockClick = Date.now() + 220;
                send('reorderPin', { id: st.pinId, targetIndex: st.targetIdx });
            }
        });

        window.addEventListener('DOMContentLoaded', () => {
            const hub = document.getElementById('radialHub');
            hub.addEventListener('mouseenter', () => {
                pointerInRadial = true;
                clearTimeout(radialCloseTimer);
                clearTimeout(accCloseTimer);
                clearTimeout(autoHideTimer);
            });
            hub.addEventListener('mouseleave', () => {
                pointerInRadial = false;
                hideDwmPreviewNow();
                scheduleRadialClose(300);
            });
            hub.addEventListener('wheel', e => {
                if (!radialGroupKey) return;
                handleGroupWheel(e, radialGroupKey);
            }, { passive: false });

            const fcard = document.getElementById('folderStackCard');
            fcard.addEventListener('mouseenter', () => {
                pointerInFolder = true;
                clearTimeout(folderCloseTimer);
                clearTimeout(autoHideTimer);
            });
            fcard.addEventListener('mouseleave', () => {
                pointerInFolder = false;
                folderCloseTimer = setTimeout(() => {
                    if (!pointerInFolder) closeFolderStack(true);
                }, 380);
            });

            window.addEventListener('dragover', e => {
                e.preventDefault();
                if (!isDraggingFileOverDock) {
                    isDraggingFileOverDock = true;
                    autoHideRevealed = true;
                    clearTimeout(autoHideTimer);
                    renderDock();
                }
            });
            window.addEventListener('dragleave', e => {
                if (e.relatedTarget === null) {
                    isDraggingFileOverDock = false;
                    clearTimeout(dragFocusTimer);
                    document.querySelectorAll('.dock-item.drag-target').forEach(x => x.classList.remove('drag-target'));
                    scheduleDockAutoHide(440);
                }
            });
            window.addEventListener('drop', e => {
                e.preventDefault();
                isDraggingFileOverDock = false;
                clearTimeout(dragFocusTimer);
                document.querySelectorAll('.dock-item.drag-target').forEach(x => x.classList.remove('drag-target'));
                if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0 && window.chrome.webview.postMessageWithAdditionalObjects) {
                    window.chrome.webview.postMessageWithAdditionalObjects({ action: 'dropFolderToStack' }, Array.from(e.dataTransfer.files));
                }
            });
        });

        function isPointInTuckedWakeZone(cx, cy) {
            const dock = document.getElementById('dockBar');
            if (!dock) return false;
            const r = dock.getBoundingClientRect();
            const edge = getDockEdge();
            if (edge === 'Left') {
                return cx <= r.right + 28 && cy >= r.top - 52 && cy <= r.bottom + 52;
            }
            if (edge === 'Right') {
                return cx >= r.left - 28 && cy >= r.top - 52 && cy <= r.bottom + 52;
            }
            return cy >= r.top - 26 && cx >= r.left - 84 && cx <= r.right + 84;
        }

        function wakeTuckedDockIfHit(cx, cy) {
            if (!isDockTuckedNow()) return false;
            if (!isPointInTuckedWakeZone(cx, cy)) return false;
            clearTimeout(autoHideTimer);
            manualTucked = false;
            autoHideRevealed = true;
            renderDock();
            return true;
        }

        document.addEventListener('mousedown', e => {
            wakeTuckedDockIfHit(e.clientX, e.clientY);
        });

        document.addEventListener('mousemove', e => {
            const dock = document.getElementById('dockBar');
            if (!dock) return;
            if (isDockTuckedNow()) {
                wakeTuckedDockIfHit(e.clientX, e.clientY);
                return;
            }
            const r = dock.getBoundingClientRect();
            const isVert = isVerticalEdge();
            const insideDockZone = isVert
                ? (e.clientX >= r.left - 34 && e.clientX <= r.right + 34 && e.clientY >= r.top - 20 && e.clientY <= r.bottom + 20)
                : (e.clientY >= r.top - 36 && e.clientY <= r.bottom + 14 && e.clientX >= r.left - 20 && e.clientX <= r.right + 20);
            if (insideDockZone) {
                mouseX = e.clientX;
                mouseY = e.clientY;
                clearTimeout(autoHideTimer);
            } else {
                mouseX = null;
                mouseY = null;
            }
            applyParabolicWave();
        });

        document.addEventListener('mouseleave', () => {
            mouseX = null;
            mouseY = null;
            pointerInRadial = false;
            applyParabolicWave();
            clearTimeout(hoverRadialTimer);
            clearTimeout(accOpenTimer);
            if (radialGroupKey) scheduleRadialClose(320);
            if (expandedAccordionCat) scheduleAccordionClose(expandedAccordionCat);
            scheduleDockAutoHide(440);
        });

        window.addEventListener('resize', () => {
            applyDockGeometryAndOffset();
            reportHitRects();
            requestAnimationFrame(reportHitRects);
        });

        if (typeof ResizeObserver !== 'undefined') {
            const dockResizeObs = new ResizeObserver(() => {
                applyDockGeometryAndOffset();
                reportHitRects();
            });
            dockResizeObs.observe(document.documentElement);
            const dockElForObs = document.getElementById('dockBar');
            if (dockElForObs) dockResizeObs.observe(dockElForObs);
        }

        let waveRafId = null;

        function applyParabolicWave() {
            if (waveRafId === null) {
                waveRafId = requestAnimationFrame(stepParabolicWave);
            }
        }

        function stepParabolicWave() {
            waveRafId = null;
            const magStrength = ((state?.dockMagnification ?? 48) / 100.0);
            const items = document.querySelectorAll('.mag-target');
            const edge = getDockEdge();
            const isVert = isVerticalEdge();
            const active = ((isVert ? mouseY !== null : mouseX !== null) && magStrength > 0.02 && !pointerInRadial && !pointerInFolder);

            let needsNextFrame = false;
            const targets = [];

            // Zero-Drift Fitts's Law Hover Wave:
            // NEVER mutates marginLeft/Right/Top/Bottom, so every icon stays 100% locked at its exact click coordinate (0px target drift!).
            // Uses a crisp GPU-composited perpendicular elevation + tightly bounded scale that fits cleanly inside the static icon gap.
            const focusRadius = 92;
            items.forEach(el => {
                const baseDim = isVert
                    ? (el._baseH || (el._baseH = el.offsetHeight || 42))
                    : (el._baseW || (el._baseW = el.offsetWidth || 42));
                const cur = typeof el._magCur === 'number' ? el._magCur : 1.0;
                let target = 1.0;

                if (active) {
                    const rect = el.getBoundingClientRect();
                    const center = isVert ? (rect.top + rect.height / 2) : (rect.left + rect.width / 2);
                    const dist = Math.abs((isVert ? mouseY : mouseX) - center);
                    if (dist < focusRadius) {
                        const t = 1 - (dist / focusRadius);
                        const influence = t * t * (3 - 2 * t);
                        const inAcc = Boolean(el.closest('.acc-drawer'));
                        const maxAdd = inAcc
                            ? Math.min(0.11, magStrength * 0.16)
                            : Math.min(0.16, magStrength * 0.22);
                        target = 1.0 + (maxAdd * influence);
                    }
                }
                targets.push({ el, baseDim, cur, target });
            });

            for (const it of targets) {
                let next = it.cur + (it.target - it.cur) * 0.34;
                if (Math.abs(next - it.target) < 0.0015) next = it.target;
                else needsNextFrame = true;

                it.el._magCur = next;
                const extra = Math.max(0, next - 1.0);
                if (extra < 0.001) {
                    it.el.style.transform = '';
                    it.el.style.zIndex = '';
                } else {
                    const lift = Math.min(6.5, extra * it.baseDim * 0.92);
                    if (isVert) {
                        const dir = edge === 'Right' ? -1 : 1;
                        it.el.style.transform = `translateX(${(dir * lift).toFixed(1)}px) scale(${next.toFixed(3)})`;
                    } else {
                        it.el.style.transform = `translateY(-${lift.toFixed(1)}px) scale(${next.toFixed(3)})`;
                    }
                    it.el.style.zIndex = String(Math.round(next * 100));
                }
            }

            if (needsNextFrame) {
                waveRafId = requestAnimationFrame(stepParabolicWave);
            }
        }

        window.chrome.webview.addEventListener('message', e => {
            const msg = e.data;
            if (msg.type === 'state') {
                state = msg.payload;
                liveDodgedOverride = null;
                liveScaleOverride = null;
                renderDock();
            } else if (msg.type === 'dockLivePreview') {
                liveScaleOverride = typeof msg.scalePercent === 'number' ? msg.scalePercent : null;
                applyDockGeometryAndOffset();
                reportHitRects();
                requestAnimationFrame(reportHitRects);
            } else if (msg.type === 'syncLayout') {
                applyDockGeometryAndOffset();
                reportHitRects();
            } else if (msg.type === 'cursorWake') {
                clearTimeout(autoHideTimer);
                if (isDockTuckedNow()) {
                    manualTucked = false;
                    autoHideRevealed = true;
                    renderDock();
                }
            } else if (msg.type === 'cursorLeft') {
                mouseX = null;
                mouseY = null;
                applyParabolicWave();
                if (!pointerInRadial && !radialGroupKey && !pointerInFolder && !openFolderPath) {
                    scheduleDockAutoHide(420);
                }
            } else if (msg.type === 'dodgeState') {
                liveDodgedOverride = Boolean(msg.dodged);
                if (!liveDodgedOverride) autoHideRevealed = false;
                renderDock();
            } else if (msg.type === 'folderStackItems') {
                if (msg.path) {
                    folderStackCache[msg.path.toLowerCase()] = { name: msg.name, items: msg.items || [] };
                    if (openFolderPath && openFolderPath.toLowerCase() === msg.path.toLowerCase()) {
                        renderFolderStackCard();
                        requestAnimationFrame(reportHitRects);
                    }
                }
            } else if (msg.type === 'openRadialPreview') {
                if (!state || !state.groups) return;
                const sorted = [...state.groups].sort((a, b) => (b.windows?.length || 0) - (a.windows?.length || 0));
                let pick = sorted.find(g => (g.windows?.length || 0) > 1);
                if (!pick && state.groups.length >= 3) {
                    const baseIcon = state.groups[2]?.icon || state.groups[0]?.icon || null;
                    const demoGroup = {
                        key: 'preview-opera',
                        displayName: 'Opera',
                        executablePath: 'opera.exe',
                        isPinned: false,
                        hasActive: true,
                        icon: baseIcon,
                        windows: [
                            { hwnd: 101, title: '(4) Anasayfa / X - Opera', processName: 'opera', isActive: false, isTopMost: false },
                            { hwnd: 102, title: 'Teknosa Timpani - Opera', processName: 'opera', isActive: true, isTopMost: false },
                            { hwnd: 103, title: 'pckasasrv3.teknosa.com - Opera', processName: 'opera', isActive: false, isTopMost: true }
                        ]
                    };
                    state = { ...state, smartTidy: false, groups: [...state.groups, demoGroup] };
                    renderDock();
                    pick = demoGroup;
                }
                if (pick) {
                    const el = document.querySelector(`.dock-item[data-group-key="${CSS.escape(pick.key)}"]`);
                    if (el) openRadialForElement(el, pick.key, null, false);
                }
            } else if (msg.type === 'openAccordionPreview') {
                if (!state) return;
                closeRadialHub(false);
                const realGroups = (state.groups || []).filter(g => !g.key.startsWith('preview-'));
                const unpinnedReal = realGroups.filter(g => !g.isPinned && g.windows.length > 0);
                if (unpinnedReal.length === 0 && realGroups.length >= 3) {
                    const ic1 = realGroups[0]?.icon || null;
                    const ic2 = realGroups[1]?.icon || null;
                    const ic3 = realGroups[2]?.icon || null;
                    state = {
                        ...state,
                        smartTidy: true,
                        groups: [
                            ...realGroups,
                            { key: 'preview-brave', displayName: 'Brave', executablePath: 'brave.exe', isPinned: false, hasActive: false, icon: ic2, windows: [{ hwnd: 201, title: 'GitHub - Brave', processName: 'brave', isActive: false, isTopMost: false }] },
                            { key: 'preview-opera', displayName: 'Opera', executablePath: 'opera.exe', isPinned: false, hasActive: true, icon: ic3, windows: [{ hwnd: 202, title: 'Teknosa Timpani - Opera', processName: 'opera', isActive: true, isTopMost: false }, { hwnd: 203, title: 'Anasayfa / X - Opera', processName: 'opera', isActive: false, isTopMost: false }, { hwnd: 204, title: 'pckasasrv3.teknosa.com - Opera', processName: 'opera', isActive: false, isTopMost: false }] },
                            { key: 'preview-teams', displayName: 'Teams', executablePath: 'teams.exe', isPinned: false, hasActive: false, icon: ic1, windows: [{ hwnd: 205, title: 'Ekip Sohbeti - Microsoft Teams', processName: 'teams', isActive: false, isTopMost: false }] }
                        ]
                    };
                } else {
                    state = { ...state, smartTidy: true, groups: realGroups };
                }
                const buckets = getSmartTidyBuckets((state.groups || []).filter(g => !g.isPinned && g.windows.length > 0));
                if (buckets.length > 0) {
                    expandedAccordionCat = buckets[0].id;
                }
                renderDock();
            } else if (msg.type === 'outsideDeactivate') {
                if (radialGroupKey) closeRadialHub(true);
                if (openFolderPath) closeFolderStack(true);
                if (expandedAccordionCat) {
                    expandedAccordionCat = null;
                    renderDock();
                }
            }
        });
        </script>
        </body>
        </html>
        """;
}
