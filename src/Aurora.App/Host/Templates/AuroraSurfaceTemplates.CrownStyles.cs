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
            border-radius: 0 0 16px 16px;
            padding: 0 15px;
            display: inline-flex; align-items: center; gap: 8px;
            cursor: pointer;
            box-shadow:
                0 10px 26px -6px rgba(0, 0, 0, 0.68),
                0 2px 10px rgba(var(--a1-rgb), 0.16),
                inset 0 -1px 0 rgba(var(--a1-rgb), 0.34);
        }
        #crown.droplet::after {
            content: '';
            position: absolute; bottom: 0; left: 20%; right: 20%; height: 2px;
            border-radius: 99px;
            background: linear-gradient(90deg, transparent, var(--accent-1), var(--accent-2), transparent);
            opacity: 0.68;
        }

        /* 2026 ARCHITECTURAL CROWN SILHOUETTES (Unmistakably Distinct Top Form-Factors) */
        body.style-liquid-glass #crown.droplet {
            height: 26px;
            border-radius: 0 0 21px 21px;
            padding: 0 17px;
        }
        body.style-bento-silk #crown.droplet {
            margin-top: 7px;
            height: 27px;
            border-top: 1px solid var(--border-rim);
            border-radius: 999px;
            padding: 0 15px;
            box-shadow: 0 8px 22px -6px rgba(20, 22, 26, 0.20), 0 1px 3px rgba(20, 22, 26, 0.08), inset 0 1px 0 rgba(255,255,255,0.92);
        }
        body.style-bento-silk #crown.droplet::after { display: none; }
        body.style-bento-silk .drop-dot { box-shadow: none; width: 6.5px; height: 6.5px; }

        body.style-linear-precision #crown.droplet {
            height: 22px;
            border-radius: 0 0 6px 6px;
            padding: 0 13px 0 15px;
            border-left: 2.5px solid var(--accent-1);
            box-shadow: 0 8px 18px -8px rgba(0, 0, 0, 0.72);
        }
        body.style-linear-precision #crown.droplet::after { display: none; }
        body.style-linear-precision .drop-dot { width: 5px; height: 5px; border-radius: 1px; box-shadow: none; }

        body.style-mono-matrix #crown.droplet {
            margin-top: 6px;
            height: 26px;
            border-top: 1.5px solid rgba(255, 255, 255, 0.22);
            border-radius: 999px;
            padding: 0 14px;
        }
        body.style-mono-matrix #crown.droplet::after { display: none; }
        body.style-mono-matrix .drop-dot {
            width: 7px; height: 7px;
            box-shadow: 0 0 0 2px rgba(var(--a1-rgb), 0.28), 0 0 10px var(--accent-1);
        }

        body.style-spatial-vision #crown.droplet {
            margin-top: 9px;
            height: 29px;
            border-top: 1.5px solid rgba(255, 255, 255, 0.34);
            border-radius: 999px;
            padding: 0 18px;
        }
        body.style-spatial-vision #crown.droplet::after { display: none; }

        body.style-cyber-prism #crown.droplet {
            height: 26px;
            border-radius: 0;
            padding: 0 23px;
            border-top: none;
            border-bottom: 2px solid var(--accent-1);
            clip-path: polygon(0 0, 100% 0, calc(100% - 11px) 100%, 11px 100%);
            box-shadow:
                0 12px 28px -6px rgba(0, 0, 0, 0.88),
                0 0 18px -4px rgba(var(--a1-rgb), 0.42);
        }
        body.style-cyber-prism #crown.droplet::after {
            left: 12px; right: 12px; height: 2px; opacity: 0.96;
            border-radius: 0;
        }
        body.style-cyber-prism .drop-dot { width: 6px; height: 6px; border-radius: 0; transform: rotate(45deg); }
        body.style-cyber-prism #crown.pill {
            border-radius: 3px;
            clip-path: polygon(9px 0, calc(100% - 9px) 0, 100% 9px, 100% calc(100% - 9px), calc(100% - 9px) 100%, 9px 100%, 0 calc(100% - 9px), 0 9px);
            padding: 0 14px;
        }

        /* LIVE HARDWARE OSD MORPH INSIDE LIQUID DROPLET */
        #crown.droplet.osd-active {
            height: 36px;
            min-width: 242px;
            padding: 0 15px;
            border-radius: 0 0 20px 20px;
            gap: 9px;
            box-shadow:
                0 14px 36px -8px rgba(0, 0, 0, 0.74),
                0 0 18px rgba(var(--a1-rgb), 0.28),
                inset 0 -1px 0 rgba(var(--a1-rgb), 0.55);
        }
        body.style-bento-silk #crown.droplet.osd-active,
        body.style-spatial-vision #crown.droplet.osd-active,
        body.style-mono-matrix #crown.droplet.osd-active {
            border-radius: 999px;
        }
        #crown.droplet.osd-active.osd-brightness {
            box-shadow:
                0 14px 36px -8px rgba(0, 0, 0, 0.74),
                0 0 18px rgba(245, 158, 11, 0.28),
                inset 0 -1px 0 rgba(251, 191, 36, 0.65);
        }
        #crown.droplet.osd-active.osd-mute,
        #crown.droplet.osd-active.osd-off {
            box-shadow:
                0 14px 36px -8px rgba(0, 0, 0, 0.74),
                0 0 18px rgba(251, 113, 133, 0.26),
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
            background: var(--accent-tint); color: var(--accent-1);
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
            width: 6px; height: 6px; border-radius: 50%;
            background: var(--accent-1);
            box-shadow: 0 0 8px var(--accent-glow);
        }
        .drop-time {
            font-size: 11.5px; font-weight: 700; letter-spacing: 0.02em;
            color: var(--text-1);
        }
        .drop-pill-tag {
            font-size: 10px; font-weight: 700; color: var(--accent-1);
            padding: 1px 6px; border-radius: 99px;
            background: var(--accent-soft);
        }

        /* HOVER-EXPANDED ACTION PILL */
        #crown.pill {
            margin-top: 7px;
            height: 42px;
            border-radius: var(--radius-btn);
            padding: 0 8px;
            display: inline-flex; align-items: center; gap: 5px;
        }
        #crown.pill .pill-btn {
            height: 28px; padding: 0 8.5px; font-size: 11.5px; flex-shrink: 0;
        }
        #crown.pill .micro-btn {
            width: 28px; height: 28px; flex-shrink: 0;
        }

        /* FULL BENTO COMMAND DECK */
        #crown.expanded {
            margin-top: 8px;
            width: min(900px, calc(100vw - 24px));
            height: min(528px, calc(100vh - 34px));
            border-radius: var(--radius-shell);
            padding: 14px 16px;
            display: flex; flex-direction: column; gap: 10px;
        }
        #crown.drag-over {
            margin-top: 7px;
            height: 48px;
            border-radius: 999px;
            padding: 0 22px;
            border-color: var(--accent-1) !important;
            box-shadow: 0 0 0 2px var(--accent-1), 0 18px 48px var(--accent-glow) !important;
        }

        .orb {
            width: 27px; height: 27px; border-radius: 50%;
            background: conic-gradient(from 180deg, var(--accent-1), var(--accent-2), var(--accent-1));
            display: flex; align-items: center; justify-content: center;
            color: var(--accent-ink); font-weight: 900; font-size: 12.5px;
            cursor: pointer; flex-shrink: 0;
            box-shadow: 0 0 14px var(--accent-glow);
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
            padding: 4px; border-radius: var(--radius-btn); border: 1px solid var(--border-subtle);
            overflow-x: auto;
        }
        .tab {
            display: inline-flex; align-items: center; gap: 6px;
            padding: 6px 12px; border-radius: var(--radius-btn); font-size: 11.5px; font-weight: 600;
            color: var(--text-2); cursor: pointer; border: none; background: transparent;
            white-space: nowrap; transition: all 0.16s var(--ease); font-family: var(--font);
        }
        .tab:hover { color: var(--text-1); }
        .tab.active {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
        }
        .deck-body { flex: 1; min-height: 0; overflow-y: auto; overflow-x: hidden; padding-right: 3px; display: flex; flex-direction: column; }
        .bento-grid { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(0, 1.10fr); gap: 11px; flex: 1; min-height: 0; }
        .bento-col { display: flex; flex-direction: column; gap: 9px; min-height: 0; }
        .card {
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            border-radius: var(--radius-card); padding: 12px;
        }
        .card-title {
            font-size: 10.5px; font-weight: 700; text-transform: uppercase;
            letter-spacing: 0.06em; color: var(--text-2); margin-bottom: 8px;
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
            padding: 10px 11px; border-radius: calc(var(--radius-card) - 2px); border: 1px solid var(--border-subtle);
            background: var(--bg-card); cursor: pointer;
            display: flex; align-items: center; justify-content: space-between; gap: 8px;
            transition: all 0.15s var(--ease);
        }
        .tile:hover { background: var(--bg-card-hover); border-color: var(--border-rim); }
        .tile.on {
            background: linear-gradient(135deg, rgba(var(--a1-rgb),0.20), rgba(var(--a2-rgb),0.14));
            border-color: var(--accent-1);
        }
        .tile .t-row { display: flex; align-items: center; gap: 8px; min-width: 0; }
        .tile .t-icon {
            width: 28px; height: 28px; border-radius: 8px;
            background: var(--bg-inset); color: var(--text-2);
            display: flex; align-items: center; justify-content: center; flex-shrink: 0;
        }
        .tile.on .t-icon { background: var(--accent-tint); color: var(--accent-1); }
        .tile .t-name { font-size: 12px; font-weight: 700; }
        .tile .t-sub { font-size: 10.5px; color: var(--text-2); margin-top: 1px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .slider-row { display: flex; align-items: center; gap: 9px; margin-bottom: 8px; }
        .slider-row input[type=range] {
            flex: 1; accent-color: var(--accent-1); height: 5px; cursor: pointer;
        }
        .media-hero {
            display: flex; align-items: center; gap: 13px;
            background: linear-gradient(135deg, rgba(var(--a1-rgb),0.12), rgba(var(--a2-rgb),0.08));
            border: 1px solid rgba(var(--a1-rgb),0.28); border-radius: var(--radius-card); padding: 12px;
        }
        .media-art {
            width: 54px; height: 54px; border-radius: 12px; object-fit: cover;
            background: var(--bg-inset); color: var(--accent-1); flex-shrink: 0;
            display: flex; align-items: center; justify-content: center;
        }
        textarea.scratchpad {
            width: 100%; height: 86px; resize: none;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            border-radius: 10px; padding: 9px 10px; color: var(--text-1);
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
        .clip-item:hover { border-color: var(--accent-1); background: var(--accent-soft); }

        /* ========== 2026 STUDIO: ARCHITECTURAL THEME GALLERY & LIVE COLOR CUSTOMIZER ========== */
        .theme-gallery {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 6px;
        }
        .theme-card {
            position: relative;
            padding: 7px 8px;
            border-radius: calc(var(--radius-card) - 3px);
            border: 1px solid var(--border-subtle);
            background: var(--bg-card);
            cursor: pointer;
            display: flex;
            flex-direction: column;
            gap: 5px;
            transition: border-color 0.15s var(--ease), background 0.15s var(--ease), transform 0.14s var(--ease);
        }
        .theme-card:hover {
            background: var(--bg-card-hover);
            border-color: var(--border-rim);
            transform: translateY(-1px);
        }
        .theme-card.active {
            border-color: var(--accent-1);
            background: var(--accent-soft);
            box-shadow: 0 0 0 1px var(--accent-ring);
        }
        .theme-mini {
            height: 36px;
            border-radius: 8px;
            position: relative;
            overflow: hidden;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: space-between;
            padding: 0 6px 4px 6px;
            border: 1px solid rgba(255,255,255,0.12);
        }
        .theme-mini.tm-liquid-glass {
            border-radius: 10px;
            box-shadow: inset 0 1px 0 rgba(255,255,255,0.28);
        }
        .theme-mini.tm-bento-silk {
            border-radius: 9px;
            border-color: rgba(20,22,26,0.14);
            padding-top: 3px;
        }
        .theme-mini.tm-linear-precision {
            border-radius: 4px;
            border-color: rgba(255,255,255,0.16);
        }
        .theme-mini.tm-mono-matrix {
            border-radius: 8px;
            padding-top: 3px;
            border: 1px dashed rgba(255,255,255,0.24);
        }
        .theme-mini.tm-spatial-vision {
            border-radius: 13px;
            padding-top: 3px;
            border: 1.5px solid rgba(255,255,255,0.28);
            box-shadow: inset 0 1.5px 0 rgba(255,255,255,0.34);
        }
        .theme-mini.tm-cyber-prism {
            border-radius: 2px;
            border-top: 1.5px solid;
        }
        .theme-mini-crown {
            width: 30px; height: 5px;
            border-radius: 0 0 6px 6px;
            opacity: 0.96;
        }
        .theme-mini.tm-bento-silk .theme-mini-crown {
            width: 28px; height: 5px; border-radius: 99px;
            box-shadow: 0 2px 4px rgba(20,22,26,0.16);
        }
        .theme-mini.tm-linear-precision .theme-mini-crown {
            width: 34px; height: 4px; border-radius: 0 0 2px 2px;
        }
        .theme-mini.tm-mono-matrix .theme-mini-crown {
            width: 28px; height: 5px; border-radius: 99px;
            border: 1px solid rgba(255,255,255,0.24);
        }
        .theme-mini.tm-spatial-vision .theme-mini-crown {
            width: 32px; height: 5px; border-radius: 99px;
            border: 1px solid rgba(255,255,255,0.45);
        }
        .theme-mini.tm-cyber-prism .theme-mini-crown {
            width: 36px; height: 5px; border-radius: 0;
            clip-path: polygon(0 0, 100% 0, calc(100% - 4px) 100%, 4px 100%);
        }
        .theme-mini-dock {
            width: 76%; height: 11px;
            border-radius: 5px;
            display: flex; align-items: center; justify-content: center; gap: 4px;
            border: 1px solid rgba(255,255,255,0.16);
        }
        .theme-mini.tm-bento-silk .theme-mini-dock {
            width: 76%; border-radius: 6px; border-color: rgba(20,22,26,0.12);
            box-shadow: 0 2px 5px rgba(20,22,26,0.14);
        }
        .theme-mini.tm-linear-precision .theme-mini-dock {
            border-radius: 2px; width: 88%; border-color: rgba(255,255,255,0.18);
        }
        .theme-mini.tm-mono-matrix .theme-mini-dock {
            border-radius: 99px; width: 82%; border: 1px solid rgba(255,255,255,0.24);
        }
        .theme-mini.tm-spatial-vision .theme-mini-dock {
            border-radius: 99px; width: 80%; border: 1px solid rgba(255,255,255,0.42);
        }
        .theme-mini.tm-cyber-prism .theme-mini-dock {
            border-radius: 1px; width: 92%; border-top: 1.5px solid;
        }
        .theme-mini-dot { width: 4.5px; height: 4.5px; border-radius: 50%; }
        .theme-mini.tm-linear-precision .theme-mini-dot { width: 6px; height: 3px; border-radius: 1px; }
        .theme-mini.tm-cyber-prism .theme-mini-dot { width: 6px; height: 3px; border-radius: 0; }
        .theme-mini.tm-mono-matrix .theme-mini-dot { width: 5px; height: 5px; border: 1px solid rgba(255,255,255,0.55); }
        .theme-mini.tm-spatial-vision .theme-mini-dot { width: 5.5px; height: 5.5px; box-shadow: 0 0 0 1px rgba(255,255,255,0.45); }
        .theme-card-meta {
            display: flex; align-items: center; justify-content: space-between; gap: 4px; min-width: 0;
        }
        .theme-card-name {
            font-size: 11px; font-weight: 700; color: var(--text-1);
            white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
        }
        .theme-card-tag {
            font-size: 9px; font-weight: 700; color: var(--text-2);
            padding: 1px 5px; border-radius: 99px;
            background: var(--bg-inset);
            white-space: nowrap; flex-shrink: 0;
        }
        .theme-card.active .theme-card-tag {
            color: var(--accent-1);
            background: var(--accent-soft);
        }

        .color-studio {
            margin-top: 8px;
            padding: 8px 9px;
            border-radius: calc(var(--radius-card) - 3px);
            background: var(--bg-inset);
            border: 1px solid var(--border-subtle);
            display: flex;
            flex-direction: column;
            gap: 7px;
        }
        .swatch-row {
            display: flex; align-items: center; gap: 5px; flex-wrap: wrap;
        }
        .swatch-btn {
            width: 18px; height: 18px; border-radius: 50%;
            border: 1.5px solid rgba(255,255,255,0.28);
            cursor: pointer; padding: 0; flex-shrink: 0;
            transition: transform 0.14s var(--spring), border-color 0.14s;
        }
        .swatch-btn:hover { transform: scale(1.18); border-color: var(--text-1); }
        .color-pickers-grid {
            display: grid;
            grid-template-columns: repeat(5, minmax(0, 1fr));
            gap: 5px;
        }
        .cp-slot {
            display: flex; flex-direction: column; align-items: center; gap: 3px;
            padding: 4px 3px; border-radius: 8px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            cursor: pointer; transition: border-color 0.14s;
        }
        .cp-slot:hover { border-color: var(--accent-ring); }
        .cp-slot input[type=color] {
            width: 24px; height: 18px; padding: 0; border: none;
            background: transparent; cursor: pointer; border-radius: 4px;
        }
        .cp-slot input[type=color]::-webkit-color-swatch-wrapper { padding: 0; }
        .cp-slot input[type=color]::-webkit-color-swatch {
            border: 1px solid rgba(255,255,255,0.28); border-radius: 4px;
        }
        .cp-lbl { font-size: 9px; font-weight: 700; color: var(--text-2); white-space: nowrap; }
        .wcag-pill {
            font-size: 9.5px; font-weight: 800; padding: 1px 6px; border-radius: 99px;
            background: rgba(52,211,153,0.14); color: var(--success);
            border: 1px solid rgba(52,211,153,0.32);
        }
        .wcag-pill.warn {
            background: rgba(251,191,36,0.14); color: var(--warning);
            border-color: rgba(251,191,36,0.34);
        }

        /* Flyouts: Tray, Calendar, Dropzone, Omnibar */
        .flyout-panel {
            pointer-events: auto;
            margin-top: 8px; border-radius: var(--radius-shell); padding: 15px;
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
            border-radius: var(--radius-shell); padding: 15px;
            display: none; flex-direction: column; gap: 11px;
            animation: flyIn 0.18s var(--spring);
        }
        #omnibar.open { display: flex; }

        .omni-input-wrap {
            display: flex; align-items: center; gap: 10px;
            background: var(--bg-inset); border: 1.5px solid var(--accent-1);
            border-radius: 12px; padding: 9px 13px;
            box-shadow: 0 0 20px var(--accent-soft);
        }
        .omni-input-wrap input {
            flex: 1; background: transparent; border: none; outline: none;
            color: var(--text-1); font-size: 14.5px; font-family: var(--font);
            user-select: text;
        }
        .calc-banner {
            padding: 10px 14px; border-radius: 12px;
            background: linear-gradient(135deg, rgba(52,211,153,0.16), var(--accent-soft));
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
            padding: 8px 10px; border-radius: 11px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            cursor: pointer; transition: all 0.14s var(--ease);
        }
        .app-card:hover {
            background: var(--accent-soft);
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
            padding: 8px 11px; border-radius: 11px; cursor: pointer;
            background: var(--bg-card); border: 1px solid transparent;
        }
        .omni-row.sel, .omni-row:hover {
            background: var(--accent-soft); border-color: var(--accent-1);
        }
        .omni-row img { width: 22px; height: 22px; border-radius: 6px; }

        .tray-grid {
            display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 8px;
            max-height: 270px; overflow-y: auto; padding-right: 4px;
        }
        .tray-item {
            display: flex; align-items: center; gap: 9px;
            padding: 9px 10px; border-radius: 12px;
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
        .cal-day.selected:not(.today) { border-color: var(--accent-1); background: var(--accent-soft); }
        .cal-day .note-dot {
            position: absolute; bottom: 3px; width: 4.5px; height: 4.5px;
            border-radius: 50%; background: var(--accent-1);
        }
        .cal-day.today .note-dot { background: var(--accent-ink); }

        .drop-zone-box {
            border: 1.5px dashed var(--accent-1);
            border-radius: 14px; padding: 14px;
            text-align: center; background: rgba(var(--a1-rgb), 0.06);
            cursor: pointer; transition: all 0.15s var(--ease);
        }
        .drop-zone-box:hover, .drop-zone-box.drag-active { background: var(--accent-soft); }
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
            margin-top: 8px; padding: 10px 15px; border-radius: var(--radius-card);
            display: none; align-items: center; gap: 10px; max-width: 460px;
            font-size: 12px;
            transition: border-color 0.16s, transform 0.16s;
        }
        #toast.show { display: flex; }
        #toast.clickable { cursor: pointer; }
        #toast.clickable:hover { border-color: var(--accent-1); transform: translateY(-1px); }
        .toast-open-pill {
            font-size: 10px; font-weight: 800; padding: 3px 8px; border-radius: 99px;
            background: var(--accent-soft); color: var(--accent-1);
            border: 1px solid var(--accent-ring); flex-shrink: 0; white-space: nowrap;
        }
        .notif-row-click {
            font-size: 11.5px; padding: 6px 7px; border-radius: 9px;
            border-bottom: 1px solid var(--border-subtle);
            cursor: pointer; transition: background 0.14s;
        }
        .notif-row-click:hover { background: var(--bg-card-hover); }
        .omni-filters {
            display: flex; align-items: center; gap: 5px; flex-wrap: wrap;
        }
        .omni-chip {
            height: 24px; padding: 0 10px; border-radius: var(--radius-btn);
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
            padding: 9px 11px; border-radius: 12px;
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            cursor: pointer;
            transition: background 0.14s, border-color 0.14s;
        }
        .notif-card:hover { background: var(--bg-card-hover); border-color: var(--accent-1); }
        .notif-app-tag {
            font-size: 10px; font-weight: 800; letter-spacing: 0.03em;
            color: var(--accent-1); text-transform: uppercase;
        }
        #onboardingCard {
            pointer-events: auto;
            margin-top: 12px;
            width: min(790px, calc(100vw - 24px));
            max-height: 550px;
            border-radius: var(--radius-shell);
            padding: 16px 19px 15px;
            display: none;
            flex-direction: column;
            gap: 11px;
            animation: popDown 0.22s var(--spring);
        }
        #onboardingCard.open { display: flex; }
        .tour-nav {
            display: grid;
            grid-template-columns: repeat(6, minmax(0, 1fr));
            gap: 5px;
            padding: 4px;
            border-radius: 12px;
            background: var(--bg-inset);
            border: 1px solid var(--border-subtle);
        }
        .tour-nav-btn {
            height: 29px; padding: 0 7px; border-radius: 9px;
            border: 1px solid transparent; background: transparent;
            color: var(--text-2); font-size: 10.5px; font-weight: 700;
            display: inline-flex; align-items: center; justify-content: center; gap: 5px;
            cursor: pointer; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
            transition: background 0.16s, color 0.16s, border-color 0.16s;
        }
        .tour-nav-btn:hover { background: var(--bg-card-hover); color: var(--text-1); }
        .tour-nav-btn.active {
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
            box-shadow: 0 4px 12px -4px var(--accent-glow);
        }
        .tour-nav-num {
            width: 15px; height: 15px; border-radius: 99px;
            background: var(--bg-card); color: inherit;
            display: inline-flex; align-items: center; justify-content: center;
            font-size: 9.5px; font-weight: 800; flex-shrink: 0;
        }
        .tour-nav-btn.active .tour-nav-num { background: rgba(255,255,255,0.22); }
        .tour-hero {
            display: flex; align-items: center; justify-content: space-between; gap: 12px;
            padding: 9px 13px; border-radius: var(--radius-card);
            background: linear-gradient(135deg, rgba(var(--a1-rgb),0.11), rgba(var(--a2-rgb),0.08));
            border: 1px solid rgba(var(--a1-rgb),0.26);
        }
        .tour-hero-title { font-size: 13px; font-weight: 800; color: var(--text-1); letter-spacing: -0.01em; }
        .tour-hero-sub { font-size: 11px; color: var(--text-2); margin-top: 2px; line-height: 1.35; }
        .tour-body {
            display: flex; flex-direction: column; gap: 9px;
            overflow-y: auto; max-height: 335px; padding-right: 2px;
        }
        .tour-grid {
            display: grid; grid-template-columns: 1fr 1fr; gap: 8px;
        }
        .tour-card {
            padding: 9px 11px; border-radius: calc(var(--radius-card) - 2px);
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            display: flex; align-items: flex-start; gap: 9px;
            transition: border-color 0.16s, background 0.16s;
        }
        .tour-card:hover { background: var(--bg-card-hover); border-color: var(--border-rim); }
        .tour-ic {
            width: 29px; height: 29px; border-radius: 9px; flex-shrink: 0;
            background: var(--accent-soft);
            border: 1px solid rgba(var(--a1-rgb), 0.25);
            color: var(--accent-1);
            display: inline-flex; align-items: center; justify-content: center;
        }
        .tour-card-txt { min-width: 0; flex: 1; }
        .tour-card-head {
            display: flex; align-items: center; gap: 6px; flex-wrap: wrap; margin-bottom: 3px;
        }
        .tour-card-head strong { font-size: 11.5px; font-weight: 800; color: var(--text-1); }
        .tour-tag {
            font-size: 9px; font-weight: 800; letter-spacing: 0.04em; text-transform: uppercase;
            padding: 1px 6px; border-radius: 99px;
            background: var(--accent-soft); color: var(--accent-1);
            border: 1px solid rgba(var(--a1-rgb), 0.28);
        }
        .tour-tag.ok { background: rgba(16, 185, 129, 0.14); color: var(--success); border-color: rgba(16, 185, 129, 0.30); }
        .tour-tag.warn { background: rgba(245, 158, 11, 0.15); color: var(--warning); border-color: rgba(245, 158, 11, 0.32); }
        .tour-tag.hot { background: rgba(244, 63, 94, 0.15); color: var(--danger); border-color: rgba(244, 63, 94, 0.32); }
        .tour-card p {
            margin: 0; font-size: 10.7px; color: var(--text-2); line-height: 1.38;
        }
        .tour-setup-box {
            padding: 10px 12px; border-radius: var(--radius-card);
            background: var(--bg-card); border: 1px solid var(--border-subtle);
            display: flex; flex-direction: column; gap: 8px;
        }
        .tour-setup-row {
            display: flex; align-items: center; justify-content: space-between; gap: 10px; flex-wrap: wrap;
        }
        .tour-setup-lbl { font-size: 11px; font-weight: 700; color: var(--text-1); min-width: 118px; }
        .tour-kbd-strip {
            display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 7px;
        }
        .tour-kbd-item {
            padding: 7px 9px; border-radius: 10px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            display: flex; align-items: center; justify-content: space-between; gap: 6px;
            font-size: 10.5px; color: var(--text-2);
        }
        .tour-kbd {
            font-family: var(--font-mono);
            font-size: 10px; font-weight: 800; color: var(--text-1);
            padding: 2px 6px; border-radius: 6px;
            background: var(--bg-card-hover); border: 1px solid var(--border-rim);
            white-space: nowrap;
        }
        .tour-footer {
            display: flex; align-items: center; justify-content: space-between; gap: 10px;
            padding-top: 8px; border-top: 1px solid var(--border-subtle);
        }
        .tour-prog-track {
            width: 84px; height: 5px; border-radius: 99px;
            background: var(--bg-inset); overflow: hidden;
        }
        .tour-prog-fill {
            height: 100%; border-radius: 99px;
            background: linear-gradient(90deg, var(--accent-1), var(--accent-2));
            transition: width 0.22s var(--ease);
        }
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
        .clip-search:focus { border-color: var(--accent-1); box-shadow: 0 0 0 2px var(--accent-soft); }
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
            padding: 8px 10px; border-radius: 11px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            cursor: pointer; flex-shrink: 0;
            transition: border-color 0.14s, background 0.14s, transform 0.12s;
        }
        .clip-card:hover { border-color: var(--accent-1); background: var(--bg-card-hover); }
        .clip-card.pinned {
            border-color: var(--accent-ring);
            background: linear-gradient(135deg, rgba(var(--a1-rgb),0.09), var(--bg-inset));
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
            box-shadow: 0 8px 20px -4px var(--accent-glow);
        }
        #qrShareCard {
            pointer-events: auto;
            margin-top: 8px; width: min(360px, calc(100vw - 24px));
            border-radius: var(--radius-shell); padding: 14px;
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
            padding: 9px 11px; border-radius: calc(var(--radius-card) - 2px); border: 1px solid var(--border-subtle);
            background: var(--bg-card); display: flex; flex-direction: column; gap: 5px;
        }
        .spark-tile.accent-tile {
            background: linear-gradient(145deg, rgba(var(--a1-rgb),0.12), rgba(var(--a2-rgb),0.08));
            border-color: rgba(var(--a1-rgb),0.34);
        }
        .spark-svg { width: 100%; height: 28px; display: block; overflow: visible; }
        .proc-row {
            display: flex; align-items: center; justify-content: space-between; gap: 8px;
            padding: 5px 8px; border-radius: 10px;
            background: var(--bg-inset); border: 1px solid var(--border-subtle);
            font-size: 11.5px;
        }

        /* ========== 2026 ARCHITECTURAL COMMAND DECK STYLES (Unmistakable Visual Paradigms) ========== */

        /* 1. LIQUID GLASS 26 — Refractive optical glass lenses with luminous inner top highlights */
        body.style-liquid-glass .card {
            background:
                linear-gradient(180deg, rgba(255,255,255,0.085) 0%, rgba(255,255,255,0.02) 45%, transparent 100%),
                var(--bg-card);
            border: 1px solid rgba(255,255,255,0.14);
            box-shadow: inset 0 1px 0 rgba(255,255,255,0.22);
        }
        body.style-liquid-glass .tab.active {
            box-shadow: 0 4px 16px -4px var(--accent-glow), inset 0 1px 0 rgba(255,255,255,0.34);
        }
        body.style-liquid-glass .tile.on {
            box-shadow: inset 0 1px 0 rgba(255,255,255,0.22), 0 6px 18px -8px var(--accent-glow);
        }

        /* 2. BENTO SILK — Tactile Elevated Porcelain Bento Cards, Recessed Track & White Paper Tabs (Zero Neon!) */
        body.style-bento-silk.mode-light .card {
            background: rgba(255, 255, 255, 0.88);
            border: 1px solid rgba(20, 22, 26, 0.09);
            box-shadow: 0 3px 10px -3px rgba(20, 22, 26, 0.08), 0 1px 2px rgba(20, 22, 26, 0.05);
        }
        body.style-bento-silk.mode-light .tabs {
            background: rgba(20, 22, 26, 0.065);
            border: 1px solid rgba(20, 22, 26, 0.08);
            border-radius: 13px;
            padding: 3px;
        }
        body.style-bento-silk.mode-light .tab {
            border-radius: 10px;
        }
        body.style-bento-silk.mode-light .tab.active {
            background: #FFFFFF;
            color: var(--text-1);
            box-shadow: 0 2px 6px rgba(20, 22, 26, 0.12), 0 0 0 1px rgba(20, 22, 26, 0.06);
        }
        body.style-bento-silk.mode-light .tile {
            background: rgba(247, 245, 240, 0.85);
            border: 1px solid rgba(20, 22, 26, 0.09);
            border-bottom-width: 2px;
        }
        body.style-bento-silk.mode-light .tile:hover {
            background: #FFFFFF;
            border-color: rgba(20, 22, 26, 0.16);
        }
        body.style-bento-silk.mode-light .tile.on {
            background: rgba(var(--a1-rgb), 0.10);
            border-color: var(--accent-1);
        }
        body.style-bento-silk.mode-light .tile.on .t-icon {
            background: var(--accent-1);
            color: var(--accent-ink);
        }
        body.style-bento-silk.mode-light .media-hero {
            background: rgba(247, 245, 240, 0.90);
            border: 1px solid rgba(20, 22, 26, 0.10);
        }
        body.style-bento-silk.mode-light .color-studio {
            background: rgba(20, 22, 26, 0.038);
            border-color: rgba(20, 22, 26, 0.08);
        }
        body.style-bento-silk.mode-light .omni-input-wrap {
            background: #FFFFFF;
            box-shadow: 0 2px 10px -4px rgba(20, 22, 26, 0.12);
        }

        /* 3. LINEAR SWISS — Bottom-Underline Tabs, Header-Ruled Flat Cards, Left-Rail Active Tiles (Zero Glow!) */
        body.style-linear-precision .tabs {
            background: transparent;
            border: none;
            border-bottom: 1px solid var(--border-subtle);
            border-radius: 0;
            padding: 0;
            gap: 2px;
        }
        body.style-linear-precision .tab {
            border-radius: 6px 6px 0 0;
            padding: 7px 12px;
        }
        body.style-linear-precision .tab:hover {
            background: rgba(255, 255, 255, 0.035);
        }
        body.style-linear-precision .tab.active {
            background: rgba(var(--a1-rgb), 0.09);
            color: var(--text-1);
            box-shadow: inset 0 -2px 0 var(--accent-1);
        }
        body.style-linear-precision .card {
            background: rgba(255, 255, 255, 0.022);
            border: 1px solid var(--border-subtle);
            border-radius: 7px;
            padding: 11px 12px;
        }
        body.style-linear-precision .card-title {
            padding-bottom: 7px;
            margin-bottom: 9px;
            border-bottom: 1px solid var(--border-subtle);
            font-size: 10px;
            letter-spacing: 0.09em;
        }
        body.style-linear-precision .tile {
            border-radius: 6px;
            background: rgba(255, 255, 255, 0.02);
        }
        body.style-linear-precision .tile.on {
            background: rgba(var(--a1-rgb), 0.09);
            border-color: var(--border-rim);
            border-left: 3px solid var(--accent-1);
        }
        body.style-linear-precision .tile .t-icon { border-radius: 5px; }
        body.style-linear-precision .media-hero {
            background: rgba(255, 255, 255, 0.025);
            border: 1px solid var(--border-rim);
            border-left: 3px solid var(--accent-1);
            border-radius: 7px;
        }
        body.style-linear-precision .omni-input-wrap {
            border-radius: 7px;
            border-width: 1px;
            box-shadow: none;
        }
        body.style-linear-precision .cal-day.today {
            background: var(--accent-1);
            border-radius: 6px;
        }

        /* 4. MONO MATRIX — Industrial Hardware Modules, Dashed Borders, Inverted Mono Tabs & Square Bullet Titles! */
        body.style-mono-matrix .tabs {
            background: transparent;
            border: 1px dashed var(--border-rim);
            border-radius: 999px;
            padding: 3px;
            gap: 4px;
        }
        body.style-mono-matrix .tab {
            text-transform: uppercase;
            font-size: 10.5px;
            padding: 5px 11px;
        }
        body.style-mono-matrix .tab.active {
            background: var(--text-1);
            color: var(--bg-glass-1);
            font-weight: 800;
        }
        body.style-mono-matrix .card {
            background:
                radial-gradient(rgba(255, 255, 255, 0.055) 1px, transparent 1px),
                var(--bg-card);
            background-size: 10px 10px, 100% 100%;
            border: 1px dashed var(--border-rim);
            border-radius: 12px;
        }
        body.style-mono-matrix .card-title .card-title-l::before {
            content: '▪';
            color: var(--accent-1);
            font-size: 12px;
            line-height: 1;
            margin-right: 1px;
        }
        body.style-mono-matrix .studio-sec {
            border-top-style: dashed;
        }
        body.style-mono-matrix .tile {
            border-radius: 14px;
            border: 1px solid var(--border-subtle);
        }
        body.style-mono-matrix .tile:hover {
            border-style: dashed;
            border-color: var(--accent-1);
        }
        body.style-mono-matrix .tile .t-icon {
            border-radius: 50%;
            border: 1px solid var(--border-rim);
        }
        body.style-mono-matrix .tile.on {
            background: rgba(var(--a1-rgb), 0.14);
            border: 1.5px solid var(--accent-1);
        }
        body.style-mono-matrix .tile.on .t-icon {
            background: var(--accent-1);
            color: var(--accent-ink);
            border-color: var(--accent-1);
        }
        body.style-mono-matrix .media-hero {
            border-style: dashed;
            border-radius: 14px;
        }
        body.style-mono-matrix .omni-input-wrap {
            border-radius: 999px;
            padding: 9px 16px;
        }
        body.style-mono-matrix .cal-day { border-radius: 50%; }
        body.style-mono-matrix .cal-day.today { background: var(--accent-1); }

        /* 5. SPATIAL VISION — Plush visionOS Frosted Capsules, Luminous White-Glass Active Lens & Circular Pods! */
        body.style-spatial-vision #crown.expanded {
            border-radius: 30px;
            padding: 16px 18px;
        }
        body.style-spatial-vision .tabs {
            background: rgba(255, 255, 255, 0.08);
            border: 1px solid rgba(255, 255, 255, 0.22);
            border-radius: 999px;
            padding: 4px;
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.24);
        }
        body.style-spatial-vision .tab {
            border-radius: 999px;
            padding: 6px 14px;
        }
        body.style-spatial-vision .tab.active {
            background: rgba(255, 255, 255, 0.24);
            color: #FFFFFF;
            border: 1px solid rgba(255, 255, 255, 0.44);
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.56), 0 6px 18px -6px rgba(0, 0, 0, 0.40);
        }
        body.style-spatial-vision .card {
            border-radius: 22px;
            background: rgba(255, 255, 255, 0.065);
            border: 1px solid rgba(255, 255, 255, 0.18);
            box-shadow: inset 0 1.5px 0 rgba(255, 255, 255, 0.24);
        }
        body.style-spatial-vision .tile {
            border-radius: 18px;
            background: rgba(255, 255, 255, 0.06);
            border: 1px solid rgba(255, 255, 255, 0.15);
        }
        body.style-spatial-vision .tile:hover {
            background: rgba(255, 255, 255, 0.14);
            border-color: rgba(255, 255, 255, 0.36);
        }
        body.style-spatial-vision .tile .t-icon {
            border-radius: 50%;
            background: rgba(255, 255, 255, 0.11);
        }
        body.style-spatial-vision .tile.on {
            background: radial-gradient(circle at 20% 20%, rgba(255,255,255,0.22), rgba(var(--a1-rgb),0.22));
            border-color: rgba(255, 255, 255, 0.46);
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.45), 0 8px 22px -8px var(--accent-glow);
        }
        body.style-spatial-vision .media-hero {
            border-radius: 22px;
            border-color: rgba(255, 255, 255, 0.26);
        }
        body.style-spatial-vision .omni-input-wrap {
            border-radius: 999px;
            padding: 10px 18px;
            border-color: rgba(255, 255, 255, 0.40);
        }

        /* 6. CYBER HUD — Tactical Corner-Bracketed Panels, Top-Laser HUD Tabs & Monospace // Headers! */
        body.style-cyber-prism .tabs {
            background: rgba(0, 0, 0, 0.38);
            border: 1px solid rgba(var(--a1-rgb), 0.28);
            border-radius: 3px;
            padding: 3px;
            gap: 3px;
        }
        body.style-cyber-prism .tab {
            border-radius: 2px;
            text-transform: uppercase;
            font-size: 10.5px;
            padding: 6px 11px;
            border: 1px solid transparent;
        }
        body.style-cyber-prism .tab.active {
            background: rgba(var(--a1-rgb), 0.18);
            color: var(--accent-1);
            border-color: rgba(var(--a1-rgb), 0.55);
            box-shadow: inset 0 2px 0 var(--accent-1), 0 0 14px -5px var(--accent-glow);
        }
        body.style-cyber-prism .card {
            border-radius: 3px;
            background:
                linear-gradient(var(--accent-1), var(--accent-1)) 0 0 / 8px 1.5px no-repeat,
                linear-gradient(var(--accent-1), var(--accent-1)) 0 0 / 1.5px 8px no-repeat,
                linear-gradient(var(--accent-2), var(--accent-2)) 100% 100% / 8px 1.5px no-repeat,
                linear-gradient(var(--accent-2), var(--accent-2)) 100% 100% / 1.5px 8px no-repeat,
                rgba(var(--a1-rgb), 0.035);
            border: 1px solid rgba(var(--a1-rgb), 0.22);
        }
        body.style-cyber-prism .card-title .card-title-l::before {
            content: '//';
            color: var(--accent-1);
            font-family: var(--font-mono);
            font-weight: 800;
            margin-right: 2px;
        }
        body.style-cyber-prism .tile {
            border-radius: 3px;
            border: 1px solid rgba(var(--a1-rgb), 0.18);
            background: rgba(var(--a1-rgb), 0.03);
        }
        body.style-cyber-prism .tile .t-icon { border-radius: 3px; }
        body.style-cyber-prism .tile.on {
            background: linear-gradient(90deg, rgba(var(--a1-rgb), 0.22), rgba(var(--a2-rgb), 0.08));
            border-color: var(--accent-1);
            border-left: 3px solid var(--accent-1);
            box-shadow: inset 0 0 16px -6px var(--accent-glow);
        }
        body.style-cyber-prism .media-hero {
            border-radius: 3px;
            border-left: 3px solid var(--accent-1);
        }
        body.style-cyber-prism .omni-input-wrap {
            border-radius: 3px;
            border-left: 3px solid var(--accent-1);
        }
        body.style-cyber-prism .app-card,
        body.style-cyber-prism .omni-row,
        body.style-cyber-prism .tray-item,
        body.style-cyber-prism .cal-day,
        body.style-cyber-prism .clip-card {
            border-radius: 3px;
        }
        """";
}
