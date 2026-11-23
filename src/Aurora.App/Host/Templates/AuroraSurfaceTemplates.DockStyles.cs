namespace Aurora.App.Host;

public static partial class AuroraSurfaceTemplates
{
    private const string DockCss = """"
        :root {
            --radial-disc-bg: radial-gradient(circle at 50% 30%, rgba(20, 30, 52, 0.96), rgba(8, 12, 22, 0.985));
            --radial-orb-bg: rgba(255, 255, 255, 0.065);
            --radial-orb-border: rgba(255, 255, 255, 0.14);
            --radial-orb-hover: rgba(var(--a1-rgb), 0.20);
            --radial-orbit-bg: rgba(12, 18, 32, 0.96);
            --radial-caption-bg: rgba(10, 15, 28, 0.96);
            --radial-ring-stroke: rgba(148, 163, 184, 0.30);
            --radial-shadow: 0 22px 52px -10px rgba(0, 0, 0, 0.76), 0 6px 20px -4px rgba(0, 0, 0, 0.42);
            --acc-bg: rgba(255, 255, 255, 0.045);
            --acc-open-bg: rgba(var(--a1-rgb), 0.10);
        }
        .dock-stage {
            width: 100%; height: 100%;
            display: flex; flex-direction: column;
            justify-content: flex-end; align-items: center;
            padding-bottom: 8px;
            pointer-events: none;
            position: relative;
        }
        .dock-stage.pinned-bar-stage {
            padding-bottom: 0 !important;
        }
        .dock-stage.tucked-stage {
            padding-bottom: 3px;
        }

        /* ========== RADIAL WINDOW ORB HUB + ORBITAL FEATURE RING ========== */
        #radialHub {
            position: fixed;
            bottom: 62px;
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
        #radialHub.animate-in { animation: radialBloom 0.20s var(--spring); }
        @keyframes radialBloom {
            from { opacity: 0; transform: translateY(12px) scale(0.88); }
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
            box-shadow: 0 10px 26px -6px rgba(0,0,0,0.40), inset 0 1px 0 rgba(255,255,255,0.16);
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
            background: var(--accent-soft);
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
                inset 0 1px 0 rgba(255, 255, 255, 0.22),
                inset 0 0 24px rgba(var(--a1-rgb), 0.07);
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
            transition: transform 0.17s var(--spring), background 0.14s, border-color 0.14s, box-shadow 0.17s;
            box-shadow: 0 4px 12px -4px rgba(0, 0, 0, 0.30);
            z-index: 2;
        }
        .win-orb:hover, .win-orb.targeted {
            transform: translate(-50%, -50%) scale(1.11);
            background: var(--radial-orb-hover);
            border-color: var(--accent-1);
            box-shadow: 0 0 0 2px rgba(var(--a1-rgb), 0.26), 0 10px 22px -6px var(--accent-glow);
            z-index: 4;
        }
        .win-orb.active-win:not(.targeted) {
            border-color: var(--accent-ring);
            box-shadow: 0 0 0 1px rgba(var(--a1-rgb), 0.24);
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
            transition: all 0.14s ease;
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
            box-shadow: 0 8px 20px -6px rgba(0, 0, 0, 0.48), inset 0 1px 0 rgba(255,255,255,0.14);
            transition: transform 0.16s var(--spring), background 0.14s, color 0.14s, border-color 0.14s, box-shadow 0.14s;
            z-index: 3;
        }
        .orbit-btn:hover {
            transform: translate(-50%, -50%) scale(1.15);
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink);
            border-color: transparent;
            box-shadow: 0 10px 24px -5px var(--accent-glow);
            z-index: 5;
        }
        .orbit-btn.active {
            background: var(--accent-tint);
            color: var(--accent-1);
            border-color: var(--accent-1);
        }
        .orbit-btn.danger:hover {
            background: linear-gradient(135deg, #FB7185, #E11D48);
            color: #FFFFFF;
            border-color: transparent;
            box-shadow: 0 10px 24px -4px rgba(251, 113, 133, 0.52);
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
            box-shadow: 0 8px 18px -6px rgba(0, 0, 0, 0.42);
            transition: all 0.14s var(--ease);
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
            border-color: var(--accent-ring);
        }
        .radial-stem-pip {
            position: absolute;
            bottom: 3px;
            width: 24px; height: 3px;
            border-radius: 99px;
            transform: translateX(-50%);
            background: linear-gradient(90deg, transparent, var(--accent-1), var(--accent-2), transparent);
            box-shadow: 0 0 10px var(--accent-1);
            pointer-events: none;
        }

        /* ========== 2026 FLOATING CRYSTAL DOCK & EDGE-TO-EDGE FIXED BAR ========== */
        #dockBar {
            --ds: 1;
            --item-sz: calc(40px * var(--ds));
            --img-sz: calc(25px * var(--ds));
            pointer-events: auto;
            position: relative;
            display: flex; align-items: center; justify-content: center;
            gap: calc(6px * var(--ds));
            max-width: calc(100vw - 24px);
            width: auto;
            height: auto;
            min-height: calc(54px * var(--ds));
            padding: calc(6px * var(--ds)) calc(11px * var(--ds));
            border-radius: var(--radius-shell);
            transition: width 0.22s var(--ease), height 0.22s var(--ease), border-radius 0.22s var(--ease), padding 0.20s var(--ease), opacity 0.18s;
            z-index: 20;
        }

        /* PINNED EDGE-TO-EDGE FIXED BAR ("Sabit Bar") — 0px wallpaper bleed, exact WorkArea height! */
        #dockBar.pinned-bar {
            width: 100vw;
            max-width: 100vw;
            height: calc(52px * var(--ds));
            min-height: calc(52px * var(--ds));
            padding: 0 calc(16px * var(--ds));
            gap: calc(5px * var(--ds));
            border-radius: 0 !important;
            border-left: none !important;
            border-right: none !important;
            border-bottom: none !important;
            left: 0 !important;
            top: 0 !important;
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.14);
        }
        #dockBar.pinned-bar {
            --item-sz: calc(38px * var(--ds));
            --img-sz: calc(24px * var(--ds));
        }
        #dockBar.pinned-bar.density-compact {
            height: calc(44px * var(--ds));
            min-height: calc(44px * var(--ds));
            --item-sz: calc(33px * var(--ds));
            --img-sz: calc(20px * var(--ds));
            padding: 0 calc(14px * var(--ds));
        }
        #dockBar.pinned-bar.density-cozy {
            height: calc(60px * var(--ds));
            min-height: calc(60px * var(--ds));
            --item-sz: calc(44px * var(--ds));
            --img-sz: calc(28px * var(--ds));
            padding: 0 calc(18px * var(--ds));
        }

        /* 3-Zone Balanced Fixed Bar Layout (Left: Search & Folders | Center: Apps | Right: Tray, Derli, Pin) */
        #dockBar.pinned-bar.layout-split {
            display: grid;
            grid-template-columns: 1fr auto 1fr;
            align-items: center;
            gap: calc(10px * var(--ds));
        }
        .bar-zone-left {
            display: flex; align-items: center; justify-content: flex-start;
            gap: calc(6px * var(--ds)); min-width: 0;
        }
        .bar-zone-center {
            display: flex; align-items: center; justify-content: center;
            gap: calc(5px * var(--ds)); min-width: 0;
        }
        .bar-zone-right {
            display: flex; align-items: center; justify-content: flex-end;
            gap: calc(6px * var(--ds)); min-width: 0;
        }

        /* TUCKED INTELLIHIDE / AUTO-HIDE HOME INDICATOR — Ultra-slim 4px flush edge trigger! */
        #dockBar.tucked {
            height: 4px; min-height: 4px; width: 148px; min-width: 0; padding: 0; border-radius: 99px;
            background: linear-gradient(90deg, transparent 0%, var(--accent-1) 26%, var(--accent-2) 74%, transparent 100%);
            border: none !important;
            box-shadow: 0 0 12px var(--accent-glow), 0 1px 6px rgba(0, 0, 0, 0.35);
            cursor: pointer; opacity: 0.86;
        }
        #dockBar.tucked::after { display: none !important; }

        .dock-pin-btn {
            position: relative;
        }
        .dock-pin-btn.smart-dodge {
            background: rgba(var(--a2-rgb), 0.15);
            color: var(--accent-2);
            border-color: rgba(var(--a2-rgb), 0.36);
        }
        .dock-pin-btn .hover-label {
            top: -34px;
        }
        .dock-pin-btn:hover .hover-label {
            opacity: 1;
        }

        /* Attention Pulse & Live Progress Arc on Dock Icons */
        @keyframes attnPulse {
            0%, 100% { box-shadow: 0 0 0 1.5px rgba(251,191,36,0.72), 0 0 14px -2px rgba(251,191,36,0.48); border-color: var(--warning); }
            50% { box-shadow: 0 0 0 2.5px rgba(251,191,36,0.92), 0 0 20px 0px rgba(251,191,36,0.68); border-color: var(--warning); }
        }
        .dock-item.attention {
            animation: attnPulse 1.65s ease-in-out infinite;
        }
        .attn-pip {
            position: absolute; top: 1px; left: 1px;
            width: 8px; height: 8px; border-radius: 50%;
            background: var(--warning);
            box-shadow: 0 0 7px var(--warning);
            border: 1.5px solid var(--bg-glass-2);
            pointer-events: none;
        }
        .dock-item.drag-target {
            border-color: var(--accent-1) !important;
            background: var(--accent-tint) !important;
            box-shadow: 0 0 0 2px var(--accent-ring), 0 8px 20px -4px var(--accent-glow) !important;
            transform: translateY(-5px) scale(1.10) !important;
        }
        .dock-prog-svg {
            position: absolute; inset: -1px;
            width: calc(100% + 2px); height: calc(100% + 2px);
            pointer-events: none;
        }
        .prog-pill {
            position: absolute; bottom: -3px; right: -4px;
            padding: 0 4px; height: 13px; border-radius: 99px;
            background: var(--success); color: #04101F;
            font-size: 8.5px; font-weight: 800;
            display: inline-flex; align-items: center; justify-content: center;
            box-shadow: 0 2px 6px rgba(0,0,0,0.32);
            pointer-events: none;
        }

        /* Dock Folder Stacks ("Klasör Yığınları") */
        .folder-chip {
            position: relative;
            width: calc(var(--item-sz) * 0.90); height: calc(var(--item-sz) * 0.90); flex-shrink: 0;
            border-radius: var(--radius-item);
            display: inline-flex; align-items: center; justify-content: center;
            background: transparent;
            border: 1px solid transparent;
            color: var(--text-2);
            cursor: pointer;
            transform-origin: bottom center;
            transition: background 0.14s, border-color 0.14s, color 0.14s, box-shadow 0.14s;
        }
        .folder-chip:hover, .folder-chip.open {
            background: var(--accent-soft);
            border-color: var(--accent-ring);
            color: var(--accent-1);
        }
        .folder-chip:hover:not(.open) .hover-label { opacity: 1; }
        #folderStackCard {
            position: fixed;
            bottom: 62px;
            left: 50%;
            width: 288px;
            max-height: 318px;
            border-radius: var(--radius-shell);
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
            padding: 6px 8px; border-radius: 10px;
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

        /* 2026 RESTRAINED SEARCH TRIGGER (Understated neutral at rest, accent bloom on hover) */
        .search-pill {
            position: relative;
            height: calc(var(--item-sz) * 0.86); padding: 0 calc(11px * var(--ds));
            border-radius: var(--radius-btn);
            display: inline-flex; align-items: center; gap: calc(6px * var(--ds));
            background: var(--bg-card);
            border: 1px solid var(--border-subtle);
            color: var(--text-2); font-size: calc(11.5px * var(--ds)); font-weight: 600;
            cursor: pointer; transform-origin: bottom center; flex-shrink: 0;
            transition: all 0.15s var(--ease);
        }
        .search-pill:hover {
            background: var(--accent-soft);
            color: var(--accent-1); border-color: var(--accent-ring);
        }
        .search-pill:hover .hover-label { opacity: 1; }

        /* 2026 UNBOXED DOCK ICONOGRAPHY — Zero heavy idle borders, pure tactile hover/active focus! */
        .dock-item {
            position: relative;
            width: var(--item-sz); height: var(--item-sz); flex-shrink: 0;
            border-radius: var(--radius-item);
            display: flex; align-items: center; justify-content: center;
            cursor: pointer;
            transform-origin: bottom center;
            will-change: transform;
            background: transparent;
            border: 1px solid transparent;
            transition: background 0.15s var(--ease), border-color 0.15s var(--ease), box-shadow 0.16s var(--ease);
        }
        .dock-item::before {
            content: '';
            position: absolute;
            inset: -3px -3px -10px -3px;
            pointer-events: auto;
        }
        .dock-item:hover {
            background: var(--bg-card-hover);
            border-color: var(--border-subtle);
        }
        .dock-item.active {
            background: var(--accent-soft);
            border-color: rgba(var(--a1-rgb), 0.28);
        }
        .dock-item.radial-anchored {
            border-color: var(--accent-1);
            background: var(--accent-tint);
        }
        .dock-item img {
            width: var(--img-sz); height: var(--img-sz);
            border-radius: calc(6px * var(--ds));
            pointer-events: none;
            filter: drop-shadow(0 2px 4px rgba(0,0,0,0.28));
        }
        .dock-item .fallback-badge {
            width: var(--img-sz); height: var(--img-sz); border-radius: calc(7px * var(--ds));
            background: linear-gradient(135deg, var(--accent-1), var(--accent-2));
            color: var(--accent-ink); font-weight: 800; font-size: calc(12px * var(--ds));
            display: flex; align-items: center; justify-content: center;
        }
        .dock-dot {
            position: absolute; bottom: -2px; left: 50%; transform: translateX(-50%);
            height: 3px; border-radius: 99px;
            background: var(--text-2);
            pointer-events: none;
            transition: width 0.18s var(--ease), background 0.18s;
        }
        #dockBar.pinned-bar .dock-dot { bottom: -1px; }
        .dock-item.active .dock-dot {
            width: 14px;
            background: linear-gradient(90deg, var(--accent-1), var(--accent-2));
            box-shadow: 0 0 8px var(--accent-glow);
        }
        .dock-item.running:not(.active) .dock-dot { width: 4.5px; opacity: 0.72; }
        .count-pill {
            position: absolute; top: -2px; right: -2px;
            min-width: 15px; height: 15px; padding: 0 4px;
            border-radius: 99px;
            background: var(--accent-1);
            color: var(--accent-ink);
            font-size: 9px; font-weight: 800;
            display: flex; align-items: center; justify-content: center;
            box-shadow: 0 2px 6px rgba(0,0,0,0.32);
            pointer-events: none;
        }
        .hover-label {
            position: absolute; top: -34px; left: 50%; transform: translateX(-50%);
            padding: 4px 10px; border-radius: 8px;
            background: var(--bg-glass-2);
            border: 1px solid var(--border-rim);
            color: var(--text-1); font-size: 11px; font-weight: 600;
            white-space: nowrap; opacity: 0; pointer-events: none;
            transition: opacity 0.12s ease, transform 0.14s var(--ease);
            box-shadow: 0 10px 24px -6px rgba(0,0,0,0.48);
            z-index: 95;
        }
        .dock-item:hover:not(.radial-anchored) .hover-label { opacity: 1; }
        .dock-v-btn { position: relative; height: calc(var(--item-sz) * 0.78); padding: 0 9px; font-size: 11px; }
        .dock-v-btn:hover .hover-label { opacity: 1; }
        .dock-v-btn.tidy-on {
            background: var(--accent-soft);
            border-color: var(--accent-ring);
            color: var(--accent-1);
        }
        .dock-v-badge {
            display: none;
            position: absolute; top: -3px; right: -3px;
            min-width: 15px; height: 15px; padding: 0 3.5px;
            border-radius: 99px;
            background: var(--bg-glass-2);
            border: 1px solid var(--border-rim);
            color: var(--text-1);
            font-size: 8.5px; font-weight: 800;
            align-items: center; justify-content: center;
            pointer-events: none;
            box-shadow: 0 2px 6px rgba(0,0,0,0.28);
        }
        .dock-sep {
            width: 1px; height: 22px;
            background: var(--border-subtle);
            margin: 0 3px; flex-shrink: 0;
        }

        /* Dock Density Variants (Floating Pill Mode) */
        #dockBar:not(.pinned-bar).density-compact {
            --item-sz: calc(34px * var(--ds));
            --img-sz: calc(21px * var(--ds));
            min-height: calc(46px * var(--ds));
            padding: calc(5px * var(--ds)) calc(9px * var(--ds));
            gap: calc(5px * var(--ds));
        }
        #dockBar.density-compact .acc-bucket { height: calc(34px * var(--ds)); }
        #dockBar.density-compact .acc-head { height: calc(30px * var(--ds)); padding: 0 calc(7px * var(--ds)); font-size: calc(11px * var(--ds)); }
        #dockBar.density-compact .dock-sep { height: calc(18px * var(--ds)); }

        #dockBar:not(.pinned-bar).density-cozy {
            --item-sz: calc(46px * var(--ds));
            --img-sz: calc(29px * var(--ds));
            min-height: calc(62px * var(--ds));
            padding: calc(8px * var(--ds)) calc(14px * var(--ds));
            gap: calc(8px * var(--ds));
        }
        #dockBar.density-cozy .acc-bucket { height: calc(46px * var(--ds)); }
        #dockBar.density-cozy .acc-head { height: calc(40px * var(--ds)); font-size: calc(12px * var(--ds)); }

        /* Inline Accordion Category Stack for Smart Tidy ("Derli") */
        .acc-bucket {
            display: inline-flex; align-items: center;
            height: calc(var(--item-sz) * 0.90); border-radius: var(--radius-item);
            background: var(--acc-bg);
            border: 1px solid var(--border-subtle);
            padding: 0 2px;
            flex-shrink: 0;
            transition: background 0.16s ease, border-color 0.16s ease, box-shadow 0.16s ease;
        }
        .acc-bucket.has-active {
            border-color: var(--accent-ring);
            background: var(--accent-soft);
        }
        .acc-bucket.open {
            background: var(--acc-open-bg);
            border-color: var(--accent-1);
            padding: 0 calc(5px * var(--ds)) 0 calc(3px * var(--ds));
        }
        .acc-head {
            height: calc(var(--item-sz) * 0.82); padding: 0 calc(8px * var(--ds)); border-radius: calc(var(--radius-item) - 2px);
            display: inline-flex; align-items: center; gap: calc(6px * var(--ds));
            cursor: pointer; font-size: calc(11.5px * var(--ds)); font-weight: 600;
            color: var(--text-1);
            transition: background 0.14s ease;
        }
        .acc-head:hover { background: var(--bg-card-hover); }
        .stack-icons { display: flex; align-items: center; }
        .stack-icons img {
            width: calc(17px * var(--ds)); height: calc(17px * var(--ds)); border-radius: calc(4.5px * var(--ds));
            margin-left: calc(-5px * var(--ds)); border: 1.5px solid var(--bg-glass-2);
            background: var(--bg-glass-2);
        }
        .stack-icons img:first-child { margin-left: 0; }
        .acc-count {
            min-width: calc(16px * var(--ds)); height: calc(16px * var(--ds)); padding: 0 calc(4px * var(--ds));
            border-radius: 99px;
            background: var(--accent-soft);
            color: var(--accent-1);
            font-size: calc(10px * var(--ds)); font-weight: 800;
            display: inline-flex; align-items: center; justify-content: center;
        }
        .acc-drawer {
            display: none;
            align-items: center;
            gap: calc(4px * var(--ds));
            padding-left: calc(6px * var(--ds));
            margin-left: calc(4px * var(--ds));
            border-left: 1px solid var(--border-subtle);
        }
        .acc-bucket.open .acc-drawer {
            display: inline-flex;
            animation: accFanIn 0.18s var(--spring);
        }
        .acc-bucket.open .acc-drawer .dock-item {
            width: calc(var(--item-sz) * 0.84); height: calc(var(--item-sz) * 0.84);
        }
        .acc-bucket.open .acc-drawer .dock-item img { width: calc(var(--img-sz) * 0.84); height: calc(var(--img-sz) * 0.84); }
        .acc-bucket.open .acc-drawer .dock-item .dock-dot { bottom: -2px; height: 2.5px; }
        @keyframes accFanIn {
            from { opacity: 0; transform: translateX(-6px) scale(0.94); }
            to { opacity: 1; transform: translateX(0) scale(1); }
        }

        /* ========== 2026 ARCHITECTURAL THEME SIGNATURES ON DOCK & FIXED BAR ========== */

        /* 1. LINEAR PRECISION: razor 2px active bar, flat matte fixed bar, zero glow */
        body.style-linear-precision .dock-item.active .dock-dot {
            height: 2px; width: 16px; border-radius: 1px; box-shadow: none;
            background: var(--accent-1);
        }
        body.style-linear-precision #dockBar.pinned-bar {
            border-top: 1px solid var(--border-rim) !important;
            box-shadow: none;
        }

        /* 2. BENTO SILK (AÇIK): warm porcelain bar, soft elevated active wash */
        body.style-bento-silk #dockBar.pinned-bar {
            border-top: 1px solid rgba(20, 22, 26, 0.10) !important;
            box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.92);
        }
        body.style-bento-silk .dock-item.active {
            background: rgba(var(--a1-rgb), 0.11);
            border-color: rgba(var(--a1-rgb), 0.22);
        }

        /* 3. MONO MATRIX: circular signal LED active pips, circular utility buttons, monochrome industrial poise */
        body.style-mono-matrix .dock-item.active .dock-dot {
            width: 5px; height: 5px; border-radius: 50%;
            background: var(--accent-1);
            box-shadow: 0 0 8px var(--accent-1);
        }
        body.style-mono-matrix .dock-item.running:not(.active) .dock-dot {
            width: 3.5px; height: 3.5px; border-radius: 50%;
        }
        body.style-mono-matrix .search-pill,
        body.style-mono-matrix .dock-v-btn {
            border-radius: 999px;
        }
        body.style-mono-matrix #dockBar.pinned-bar {
            border-top: 1px dashed rgba(255, 255, 255, 0.22) !important;
        }

        /* 4. SPATIAL VISION: full-stadium pill dock (999px), circular icon halos */
        body.style-spatial-vision #dockBar:not(.pinned-bar):not(.tucked) {
            border-radius: 999px;
            padding-left: calc(16px * var(--ds));
            padding-right: calc(16px * var(--ds));
        }
        body.style-spatial-vision .dock-item,
        body.style-spatial-vision .folder-chip {
            border-radius: 50%;
        }
        body.style-spatial-vision .acc-bucket {
            border-radius: 999px;
        }
        body.style-spatial-vision .dock-item.active .dock-dot {
            width: 6px; height: 6px; border-radius: 50%;
            bottom: -3px;
        }

        /* 5. CYBER PRISM: edge-lit top laser trace on Dock & Fixed Bar + top-edge HUD active bar */
        body.style-cyber-prism #dockBar:not(.tucked)::after {
            content: '';
            position: absolute;
            top: -1px; left: 12%; right: 12%; height: 1.5px;
            background: linear-gradient(90deg, transparent, var(--accent-1) 30%, var(--accent-2) 70%, transparent);
            pointer-events: none;
        }
        body.style-cyber-prism #dockBar.pinned-bar::after {
            left: 0; right: 0; height: 1.5px;
            background: linear-gradient(90deg, rgba(var(--a1-rgb), 0.15), var(--accent-1) 25%, var(--accent-2) 75%, rgba(var(--a2-rgb), 0.15));
        }
        body.style-cyber-prism .dock-item.active .dock-dot {
            top: -1px; bottom: auto;
            width: 18px; height: 2px; border-radius: 1px;
        }

        /* ========== VERTICAL DOCK & VERTICAL FIXED RAIL (LEFT & RIGHT) ========== */
        body.edge-left .dock-stage {
            flex-direction: row; justify-content: flex-start; align-items: center;
            padding: 10px 0 10px 7px;
        }
        body.edge-right .dock-stage {
            flex-direction: row; justify-content: flex-end; align-items: center;
            padding: 10px 7px 10px 0;
        }
        body.edge-left .dock-stage.pinned-bar-stage,
        body.edge-right .dock-stage.pinned-bar-stage {
            padding: 0 !important;
        }
        body.edge-left .dock-stage.tucked-stage { padding-left: 3px; }
        body.edge-right .dock-stage.tucked-stage { padding-right: 3px; }

        body.edge-left #dockBar,
        body.edge-right #dockBar {
            flex-direction: column;
            width: auto;
            height: auto;
            min-height: 0;
            min-width: calc(54px * var(--ds));
            padding: calc(9px * var(--ds)) calc(7px * var(--ds));
            gap: calc(6px * var(--ds));
            max-width: none;
            max-height: calc(100vh - 20px);
            border-radius: var(--radius-shell);
        }
        body.edge-left #dockBar.pinned-bar,
        body.edge-right #dockBar.pinned-bar {
            height: 100vh;
            max-height: 100vh;
            width: calc(58px * var(--ds));
            min-width: calc(58px * var(--ds));
            padding: calc(12px * var(--ds)) 0;
            border-radius: 0 !important;
            border-top: none !important;
            border-bottom: none !important;
        }
        body.edge-left #dockBar.pinned-bar { border-left: none !important; border-right: 1px solid var(--border-rim) !important; }
        body.edge-right #dockBar.pinned-bar { border-right: none !important; border-left: 1px solid var(--border-rim) !important; }
        body.edge-left #dockBar.pinned-bar.density-compact,
        body.edge-right #dockBar.pinned-bar.density-compact {
            width: calc(50px * var(--ds)); min-width: calc(50px * var(--ds));
        }
        body.edge-left #dockBar.pinned-bar.density-cozy,
        body.edge-right #dockBar.pinned-bar.density-cozy {
            width: calc(66px * var(--ds)); min-width: calc(66px * var(--ds));
        }
        body.edge-left #dockBar.pinned-bar.layout-split,
        body.edge-right #dockBar.pinned-bar.layout-split {
            display: grid;
            grid-template-columns: 1fr;
            grid-template-rows: 1fr auto 1fr;
            justify-items: center;
        }
        body.edge-left .bar-zone-left, body.edge-right .bar-zone-left { flex-direction: column; justify-content: flex-start; }
        body.edge-left .bar-zone-center, body.edge-right .bar-zone-center { flex-direction: column; justify-content: center; }
        body.edge-left .bar-zone-right, body.edge-right .bar-zone-right { flex-direction: column; justify-content: flex-end; }

        body.edge-left #dockBar.density-compact:not(.pinned-bar),
        body.edge-right #dockBar.density-compact:not(.pinned-bar) {
            min-width: calc(46px * var(--ds));
            padding: calc(7px * var(--ds)) calc(6px * var(--ds));
            gap: calc(5px * var(--ds));
        }
        body.edge-left #dockBar.density-cozy:not(.pinned-bar),
        body.edge-right #dockBar.density-cozy:not(.pinned-bar) {
            min-width: calc(62px * var(--ds));
            padding: calc(10px * var(--ds)) calc(8px * var(--ds));
            gap: calc(8px * var(--ds));
        }
        body.edge-left #dockBar.tucked,
        body.edge-right #dockBar.tucked {
            width: 4px; min-width: 4px; height: 112px; min-height: 112px; padding: 0;
            border-radius: 99px;
            background: linear-gradient(180deg, transparent 0%, var(--accent-1) 26%, var(--accent-2) 74%, transparent 100%);
            box-shadow: 0 0 12px var(--accent-glow), 0 2px 8px rgba(0, 0, 0, 0.38);
        }
        body.edge-left .dock-sep,
        body.edge-right .dock-sep {
            width: calc(22px * var(--ds)); height: 1px;
            background: var(--border-subtle);
            margin: calc(2px * var(--ds)) 0;
        }
        body.edge-left .search-pill,
        body.edge-right .search-pill {
            width: var(--item-sz); height: var(--item-sz);
            border-radius: var(--radius-item);
            padding: 0; justify-content: center;
        }
        body.edge-left .folder-chip,
        body.edge-right .folder-chip {
            width: var(--item-sz); height: var(--item-sz);
            border-radius: var(--radius-item);
        }
        body.edge-left .dock-item,
        body.edge-left .folder-chip,
        body.edge-left .acc-bucket { transform-origin: center left; }
        body.edge-right .dock-item,
        body.edge-right .folder-chip,
        body.edge-right .acc-bucket { transform-origin: center right; }
        body.edge-left .dock-item::before { inset: -3px -10px -3px -6px; }
        body.edge-right .dock-item::before { inset: -3px -6px -3px -10px; }
        body.edge-left .dock-dot {
            left: -4px; right: auto; bottom: auto; top: 50%; transform: translateY(-50%);
            width: 3px; height: 5px;
        }
        body.edge-left .dock-item.active .dock-dot,
        body.edge-left .acc-bucket.has-active > .acc-head .dock-dot { width: 3px; height: 14px; background: linear-gradient(180deg, var(--accent-1), var(--accent-2)); }
        body.edge-left .dock-item.running:not(.active) .dock-dot { width: 3px; height: 4.5px; }
        body.edge-right .dock-dot {
            right: -4px; left: auto; bottom: auto; top: 50%; transform: translateY(-50%);
            width: 3px; height: 5px;
        }
        body.edge-right .dock-item.active .dock-dot,
        body.edge-right .acc-bucket.has-active > .acc-head .dock-dot { width: 3px; height: 14px; background: linear-gradient(180deg, var(--accent-1), var(--accent-2)); }
        body.edge-right .dock-item.running:not(.active) .dock-dot { width: 3px; height: 4.5px; }

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
            opacity: 1; transform: translateY(-50%) translateX(0);
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
            opacity: 1; transform: translateY(-50%) translateX(0);
        }

        body.edge-left .acc-bucket,
        body.edge-right .acc-bucket {
            flex-direction: column;
            width: var(--item-sz); height: auto !important;
            padding: 0;
            border-radius: var(--radius-item);
            background: var(--bg-card);
            border: 1px solid var(--border-subtle);
            overflow: visible;
        }
        body.edge-left .acc-bucket.open,
        body.edge-right .acc-bucket.open {
            background: var(--acc-open-bg);
            border-color: var(--accent-1);
            padding: 2px 0 4px 0;
        }
        body.edge-left .acc-head,
        body.edge-right .acc-head {
            position: relative;
            width: calc(var(--item-sz) - 2px); height: calc(var(--item-sz) - 2px) !important;
            padding: 0;
            flex-direction: column; justify-content: center; align-items: center; gap: 0;
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
        .v-mosaic.pair { grid-template-rows: 1fr; align-content: center; height: auto; }
        .v-mosaic.pair img { width: calc(var(--item-sz) * 0.35); height: calc(var(--item-sz) * 0.35); }
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
        }
        body.edge-left .acc-bucket.open .acc-drawer .dock-item img,
        body.edge-right .acc-bucket.open .acc-drawer .dock-item img {
            width: calc(var(--img-sz) * 0.84); height: calc(var(--img-sz) * 0.84);
        }
        body.edge-left .dock-v-btn,
        body.edge-right .dock-v-btn,
        body.edge-left .dock-pin-btn,
        body.edge-right .dock-pin-btn {
            width: var(--item-sz);
            height: calc(var(--item-sz) * 0.82);
            border-radius: var(--radius-item);
            padding: 0;
            justify-content: center;
            background: transparent;
            border: 1px solid transparent;
            color: var(--text-2);
            box-shadow: none;
        }
        body.edge-left .dock-v-btn:hover,
        body.edge-right .dock-v-btn:hover,
        body.edge-left .dock-pin-btn:hover,
        body.edge-right .dock-pin-btn:hover {
            transform: none;
            background: var(--bg-card-hover);
            border-color: var(--border-subtle);
            color: var(--text-1);
        }
        body.edge-left .dock-v-btn.tidy-on,
        body.edge-right .dock-v-btn.tidy-on,
        body.edge-left .dock-pin-btn.active,
        body.edge-right .dock-pin-btn.active {
            background: var(--accent-soft);
            border-color: var(--accent-ring);
            color: var(--accent-1);
        }
        body.edge-left .dock-pin-btn.smart-dodge,
        body.edge-right .dock-pin-btn.smart-dodge {
            background: rgba(var(--a2-rgb), 0.16);
            border-color: rgba(var(--a2-rgb), 0.42);
            color: var(--accent-2);
        }
        body.edge-left .dock-v-badge,
        body.edge-right .dock-v-badge { display: inline-flex; }
        body.edge-left .radial-stem-pip,
        body.edge-right .radial-stem-pip { display: none; }
        """";
}
