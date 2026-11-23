namespace Aurora.App.Host;

public static partial class AuroraSurfaceTemplates
{
    private const string DockScript = """"
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
            root.setProperty('--success', isLight ? '#059669' : '#34D399');
            root.setProperty('--warning', isLight ? '#D97706' : '#FBBF24');
            root.setProperty('--danger', isLight ? '#E11D48' : '#FB7185');

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
        """";
}
