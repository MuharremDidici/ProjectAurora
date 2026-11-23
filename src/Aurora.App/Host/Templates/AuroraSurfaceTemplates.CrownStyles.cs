namespace Aurora.App.Host;

public static partial class AuroraSurfaceTemplates
{
    private const string CrownCss = """"
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
            background: var(--bg-inset); color: var(--text-2);
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
            background: var(--bg-inset); color: var(--accent-1); flex-shrink: 0;
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
            background: var(--bg-card-hover); color: var(--text-1);
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
        .cal-day.today .note-dot { background: var(--accent-ink); }

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
            border: 1.5px solid var(--border-rim);
            box-shadow: inset 0 1px 0 rgba(255,255,255,0.22), 0 3px 10px rgba(0,0,0,0.25);
        }

        /* v2.2 Hardware & Productivity Visual Polish */
        .priv-dot {
            width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0;
            display: inline-block;
        }
        .priv-dot.cam { background: var(--success); box-shadow: 0 0 8px var(--success); }
        .priv-dot.mic { background: var(--warning); box-shadow: 0 0 8px var(--warning); }
        .rec-dot {
            width: 8px; height: 8px; border-radius: 50%;
            background: var(--danger); box-shadow: 0 0 10px var(--danger);
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
            display: grid; grid-template-columns: repeat(auto-fit, minmax(168px, 1fr)); gap: 6px;
            margin-top: 7px; padding-top: 7px; border-top: 1px solid var(--border-subtle);
        }
        .bt-batt-chip {
            display: flex; align-items: center; gap: 6px; min-width: 0;
            padding: 5px 8px; border-radius: 10px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            font-size: 10.5px; font-weight: 700;
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
        """";
}
