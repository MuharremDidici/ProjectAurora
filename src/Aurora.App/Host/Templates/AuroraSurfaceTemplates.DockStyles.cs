namespace Aurora.App.Host;

public static partial class AuroraSurfaceTemplates
{
    private const string DockCss = """"
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
            border-color: var(--border-rim);
            box-shadow: 0 8px 20px -6px rgba(0, 0, 0, 0.42), 0 0 0 1px rgba(56, 189, 248, 0.28);
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
            background: var(--bg-glass-2);
            border: 1px solid var(--border-rim);
            color: var(--text-1);
            font-size: 8.5px; font-weight: 800;
            align-items: center; justify-content: center;
            pointer-events: none;
            box-shadow: 0 2px 6px rgba(0,0,0,0.28);
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
        """";
}
