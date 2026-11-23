namespace Aurora.App.Host;

public static partial class AuroraSurfaceTemplates
{
    private const string CrownScript = """"
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
        let tourStep = 0;
        let lastTourSig = '';
        let armedPowerAction = null;
        let armedPowerTimer = null;
        let calMonthOffset = 0;
        let selectedDateKey = '';
        let isDraggingFile = false;
        let dragDropZone = 'shelf';
        let liveGifRec = null;
        let armedKillProcKey = null;
        let armedKillProcTimer = null;
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
            root.setProperty('--success', isLight ? '#059669' : '#34D399');
            root.setProperty('--warning', isLight ? '#D97706' : '#FBBF24');
            root.setProperty('--danger', isLight ? '#E11D48' : '#FB7185');
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
                        ${qr.downloads > 0 ? `<div style="font-size:10.5px;font-weight:700;color:var(--success);">✓ ${qr.downloads} kez indirildi</div>` : `<div style="font-size:10px;color:var(--text-2);line-height:1.3;">Telefon kamerasıyla okutun · İlk seferde Güvenlik Duvarı'na izin verin</div>`}
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
                } else if (recState.recording && !isAnyPopupOpen()) {
                    // Lock compact recording pill even on hover so the Stop button NEVER shifts sideways (Fitts's Law!)
                    lastDropletSig = '';
                    crown.className = 'glass-shell droplet rec-live';
                    crown.innerHTML = `
                        <span class="rec-dot"></span>
                        <span class="num" style="font-size:11.5px;font-weight:800;color:var(--danger);">REC ${formatRecTime(recState.elapsed)}</span>
                        <button class="pill-btn" style="height:22px;padding:0 10px;font-size:10.5px;border-color:var(--danger);color:var(--danger);background:rgba(225,29,72,0.12);" onclick="event.stopPropagation();send('stopGifRecording')">Durdur</button>
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
                        <button class="pill-btn num" style="border-color:var(--danger);color:var(--danger);background:rgba(225,29,72,0.12);" onclick="send('stopGifRecording')" title="GIF Ekran Kaydını Durdur ve Kaydet">
                            <span class="rec-dot"></span><span>${formatRecTime(recState.elapsed)}</span>
                        </button>
                    ` : ''}
                    ${state.privacy?.camera ? `
                        <button class="pill-btn" style="border-color:var(--success);color:var(--success);background:rgba(5,150,105,0.12);" onclick="send('openUri',{uri:'ms-settings:privacy-webcam'})" title="Kamera aktif: ${escHtml((state.privacy.cameraApps||[]).join(', '))}">
                            <span class="priv-dot cam"></span><span>${escHtml((state.privacy.cameraApps||['Kamera'])[0])}</span>
                        </button>
                    ` : ''}
                    ${state.privacy?.mic ? `
                        <button class="pill-btn" style="border-color:var(--warning);color:var(--warning);background:rgba(217,119,6,0.12);" onclick="send('toggleMicMute')" title="Mikrofon aktif: ${escHtml((state.privacy.micApps||[]).join(', '))} (Susturmak için tıklayın)">
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
                            ${monBrights.length > 0 ? monBrights.slice(0, 3).map((mb, idx) => `
                            <div class="slider-row" style="margin-bottom:5px;gap:7px;">
                                <span class="micro-btn" title="${escHtml(mb.name)} (${mb.isInternal ? 'Dahili WMI' : 'Harici DDC/CI'})">${SVG.sun}</span>
                                <span style="width:82px;font-size:10.5px;font-weight:600;color:var(--text-2);overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${escHtml(mb.name)}">${escHtml(mb.name)}</span>
                                <input type="range" min="${mb.isInternal ? 5 : 0}" max="100" value="${mb.percent}" oninput="setMonitorBrightnessByIdx(${idx}, +this.value, this)" />
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
                                    const btIcon = (d.category === 'headset' || d.category === 'earbuds')
                                        ? SVG.headphones
                                        : d.category === 'mouse'
                                            ? SVG.mouse
                                            : d.category === 'keyboard'
                                                ? SVG.keyboard
                                                : SVG.battery;
                                    return `
                                    <div class="bt-batt-chip" title="${escHtml(d.name)} · %${d.percent} (${escHtml(d.source)})">
                                        <span style="color:${col};display:inline-flex;flex-shrink:0;">${btIcon}</span>
                                        <span style="flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${escHtml(d.name)}</span>
                                        <div class="bt-batt-bar"><div class="bt-batt-fill" style="width:${d.percent}%;background:${col};"></div></div>
                                        <span class="num" style="color:${col};flex-shrink:0;">%${d.percent}</span>
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
                                    <div class="t-sub" style="margin-top:-2px;">${m.cpu < 30 ? 'Serin & Sakin' : (m.cpu < 70 ? 'Dengeli Yük' : 'Yoğun Kullanım')}</div>
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
                            <div class="card-title" style="margin-bottom:6px;"><span class="card-title-l">${SVG.layers}<span>En Çok RAM Kullananlar (Top 3)</span></span><span>Çift Tık Kapat</span></div>
                            <div style="display:flex;flex-direction:column;gap:5px;">
                                ${topProcs.length === 0 ? `<div style="font-size:11px;color:var(--text-2);">Süreç listesi hazırlanıyor...</div>` :
                                  topProcs.map((p, idx) => {
                                    const isArmed = armedKillProcKey === p.name;
                                    return `
                                    <div class="proc-row">
                                        <div style="min-width:0;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">
                                            <strong style="font-weight:700;">${escHtml(p.displayName)}</strong>
                                            ${p.instances > 1 ? `<span style="font-size:10px;color:var(--text-2);"> (${p.instances})</span>` : ''}
                                        </div>
                                        <span class="num" style="font-weight:700;color:var(--accent-1);flex-shrink:0;">${p.memoryMb} MB</span>
                                        ${isArmed
                                            ? `<button class="pill-btn danger-arm" style="height:22px;padding:0 8px;font-size:10px;flex-shrink:0;" onclick="armKillTopProcess(${idx})">Onayla?</button>`
                                            : `<button class="micro-btn danger" style="width:22px;height:22px;" onclick="armKillTopProcess(${idx})" title="${escHtml(p.displayName)} kapatmak için 2 kez tıklayın">${SVG.close}</button>`}
                                    </div>`;
                                  }).join('')}
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

        function setMonitorBrightnessByIdx(idx, value, inputEl) {
            const mb = (state?.monitorBrightness || [])[idx];
            if (!mb || !mb.id) return;
            if (inputEl && inputEl.nextElementSibling) inputEl.nextElementSibling.textContent = `%${value}`;
            send('setMonitorBrightness', { id: mb.id, name: mb.name, value });
        }

        function openClipUrlById(id) {
            const c = (state?.clipboard || []).find(x => x.id === id);
            const raw = (c?.text || c?.preview || '').trim();
            if (/^https?:\/\/\S+$/i.test(raw)) send('openUri', { uri: raw });
        }

        function armKillTopProcess(idx) {
            const p = (state?.topProcesses || [])[idx];
            if (!p) return;
            if (armedKillProcKey === p.name) {
                armedKillProcKey = null;
                clearTimeout(armedKillProcTimer);
                send('killTopProcess', { name: p.name, pid: p.pid });
                return;
            }
            armedKillProcKey = p.name;
            clearTimeout(armedKillProcTimer);
            lastExpandedSig = '';
            renderAll();
            armedKillProcTimer = setTimeout(() => {
                armedKillProcKey = null;
                lastExpandedSig = '';
                renderAll();
            }, 3500);
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

        function openOnboardingTour(stepIdx = 0) {
            expanded = false;
            closeAllSubPopups();
            tourStep = Math.max(0, Math.min(5, Number(stepIdx) || 0));
            lastTourSig = '';
            manualOnboardingOpen = true;
            onboardingDismissedLocal = false;
            renderAll();
            syncFocusDemand();
        }

        function setTourStep(stepIdx) {
            tourStep = Math.max(0, Math.min(5, Number(stepIdx) || 0));
            lastTourSig = '';
            renderOnboardingCard();
            requestAnimationFrame(reportHitRects);
        }

        function stepTour(delta) {
            const next = tourStep + delta;
            if (next < 0) return;
            if (next > 5) { finishOnboarding(); return; }
            setTourStep(next);
        }

        function tourJumpToDeck(tab) {
            manualOnboardingOpen = false;
            onboardingDismissedLocal = true;
            lastTourSig = '';
            send('completeOnboarding');
            activeTab = tab || 'control';
            toggleCrown(true);
        }

        function tourJumpToOmni() {
            manualOnboardingOpen = false;
            onboardingDismissedLocal = true;
            lastTourSig = '';
            send('completeOnboarding');
            openOmni();
        }

        function finishOnboarding() {
            manualOnboardingOpen = false;
            onboardingDismissedLocal = true;
            lastTourSig = '';
            send('completeOnboarding');
            renderAll();
            syncFocusDemand();
        }

        function tourCard(icon, title, tagText, tagCls, desc) {
            return `
                <div class="tour-card">
                    <div class="tour-ic">${icon}</div>
                    <div class="tour-card-txt">
                        <div class="tour-card-head">
                            <strong>${title}</strong>
                            ${tagText ? `<span class="tour-tag ${tagCls || ''}">${tagText}</span>` : ''}
                        </div>
                        <p>${desc}</p>
                    </div>
                </div>`;
        }

        function renderOnboardingCard() {
            const el = document.getElementById('onboardingCard');
            if (!state || !isOnboardingVisible()) {
                if (el.className !== 'glass-shell') el.className = 'glass-shell';
                lastTourSig = '';
                return;
            }

            const curTheme = state.theme?.id || 'obsidian-pro-dark';
            const curDock = state.dockMode || 'SmartDodge';
            const curEdge = state.dockEdge || 'Bottom';
            const omniKey = state.omniHotkeyPreset || 'Alt+Space';
            const appVer = state.version || '2.3.1';
            const sig = `${tourStep}|${curTheme}|${curDock}|${curEdge}|${omniKey}|${appVer}`;
            if (sig === lastTourSig && el.classList.contains('open')) return;
            lastTourSig = sig;

            const navTabs = [
                { n: 1, label: 'Kurulum' },
                { n: 2, label: 'Damla & Donanım' },
                { n: 3, label: 'Dock & Pencere' },
                { n: 4, label: 'Raf, QR & Pano' },
                { n: 5, label: 'GIF & Telemetri' },
                { n: 6, label: 'Arama & Sistem' }
            ];

            let heroTitle = '';
            let heroSub = '';
            let bodyHtml = '';
            let tryBtnHtml = '';

            if (tourStep === 0) {
                heroTitle = '1. Hızlı Kurulum & Canlı Görünüm Seçici';
                heroSub = 'Aşağıdaki seçimler masaüstünde anında uygulanır; dilediğin zaman Stüdyo sekmesinden değiştirebilirsin.';
                bodyHtml = `
                    <div class="tour-setup-box">
                        <div class="tour-setup-row">
                            <span class="tour-setup-lbl">Tema Ailesi</span>
                            <div style="display:flex;gap:5px;flex-wrap:wrap;flex:1;">
                                <button class="pill-btn ${curTheme==='obsidian-pro-dark'?'accent':''}" onclick="send('applyTheme',{id:'obsidian-pro-dark'})">Obsidian Pro (Koyu)</button>
                                <button class="pill-btn ${curTheme==='aurora-glass-light'?'accent':''}" onclick="send('applyTheme',{id:'aurora-glass-light'})">Aurora Glass (Açık)</button>
                                <button class="pill-btn ${curTheme==='aurora-glass-dark'?'accent':''}" onclick="send('applyTheme',{id:'aurora-glass-dark'})">Aurora Koyu</button>
                                <button class="pill-btn ${curTheme==='nebula-dark'?'accent':''}" onclick="send('applyTheme',{id:'nebula-dark'})">Nebula</button>
                                <button class="pill-btn ${curTheme==='high-contrast-dark'?'accent':''}" onclick="send('applyTheme',{id:'high-contrast-dark'})">Yüksek Kontrast</button>
                            </div>
                        </div>
                        <div class="tour-setup-row">
                            <span class="tour-setup-lbl">Dock Konumu</span>
                            <div style="display:flex;gap:5px;flex-wrap:wrap;flex:1;">
                                <button class="pill-btn ${curEdge==='Bottom'?'accent':''}" onclick="send('setDockEdge',{edge:'Bottom'})">Alt Yatay</button>
                                <button class="pill-btn ${curEdge==='Left'?'accent':''}" onclick="send('setDockEdge',{edge:'Left'})">Sol Dikey Ray</button>
                                <button class="pill-btn ${curEdge==='Right'?'accent':''}" onclick="send('setDockEdge',{edge:'Right'})">Sağ Dikey Ray</button>
                            </div>
                        </div>
                        <div class="tour-setup-row">
                            <span class="tour-setup-lbl">Dock Davranışı</span>
                            <div style="display:flex;gap:5px;flex-wrap:wrap;flex:1;">
                                <button class="pill-btn ${curDock==='SmartDodge'?'accent':''}" onclick="send('setDockMode',{mode:'SmartDodge'})">Akıllı Kaçınma (Önerilen)</button>
                                <button class="pill-btn ${curDock==='AlwaysVisible'?'accent':''}" onclick="send('setDockMode',{mode:'AlwaysVisible'})">Her Zaman Sabit</button>
                                <button class="pill-btn ${curDock==='AutoHide'?'accent':''}" onclick="send('setDockMode',{mode:'AutoHide'})">Otomatik Gizle</button>
                            </div>
                        </div>
                    </div>
                    <div class="tour-grid">
                        ${tourCard(SVG.lock, 'Sıfır UAC & Güvenli Kabuk', 'asInvoker', 'ok', 'Yönetici izni istemez, explorer.exe’ye DLL enjekte etmez. Koruma watchdog’u kapanışta Windows çubuğunu anında geri getirir.')}
                        ${tourCard(SVG.monitor, 'Çoklu Monitör & Karma DPI', '3+ Ekran', '', 'Her ekrana ayrı Üst Damla + Dock yerleştirir; tam ekran oyun/video açıldığında yalnızca o ekranda otomatik uykuya geçer.')}
                    </div>`;
            } else if (tourStep === 1) {
                heroTitle = '2. Üst Damla (Aurora Crown) & Canlı Donanım Kontrolü';
                heroSub = 'Ekranın üst ortasındaki akışkan damlanın üzerine gelerek tüm donanım, medya ve gizlilik verilerine tek dokunuşla ulaş.';
                bodyHtml = `
                    <div class="tour-grid">
                        ${tourCard(SVG.vol, 'Canlı Donanım OSD HUD', 'Sıfır Gecikme', 'ok', 'Ses, sessiz, parlaklık, mikrofon, Caps Lock ve Num Lock tuşlarında Windows kutusunu bastırır; Üst Damla’da akıcı çubuk gösterir.')}
                        ${tourCard(SVG.sun, 'Harici Monitör DDC/CI Parlaklık', 'EDID + I2C', '', 'Dahili dizüstü ekranı (WMI) yanında harici monitörleri gerçek model adıyla (örn. VA2715-QHD) algılayıp takılmadan ayarlar.')}
                        ${tourCard(SVG.bluetooth, 'Bluetooth Cihaz Pil Yüzdeleri', 'PnP + BLE', 'warn', 'Kulaklık, fare, klavye ve oyun kollarının şarj yüzdesini cihaz ikonuyla canlı gösterir; %15 altında düşük pil uyarısı verir.')}
                        ${tourCard(SVG.camera, 'Kamera & Mikrofon Gizlilik LED’i', 'Canlı Güvenlik', 'hot', 'Hangi uygulamanın kamera (yeşil LED) veya mikrofon (turuncu LED) kullandığını hayalet süreç korumasıyla anlık bildirir.')}
                        ${tourCard(SVG.headphones, 'Medya & Uygulama Ses Yönlendirme', 'GSMTC', '', 'Spotify, YouTube ve tarayıcıyı kontrol et; Uygulama Ses Mikseri’nden her uygulama için farklı kulaklık/hoparlör çıkışı seç.')}
                        ${tourCard(SVG.coffee, 'Uyanık Tut (Awake) & Odak Modu', '1 Tık', '', 'Sunum, indirme veya izleme sırasında ekran kararmasını ve uykuyu tek tıkla (süresiz veya zamanlı) engelle; bildirimleri sustur.')}
                    </div>`;
                tryBtnHtml = `<button class="pill-btn" onclick="tourJumpToDeck('control')">${SVG.sliders}<span>Kontrol Merkezini Aç</span></button>`;
            } else if (tourStep === 2) {
                heroTitle = '3. Kristal Dock, Dikey Ray & Akıllı Pencere Yönetimi';
                heroSub = 'Fitts Yasası ile sıfır kaymalı hassas tıklama, dikey kenar desteği ve fareyle her pencereyi zahmetsizce yönetme.';
                bodyHtml = `
                    <div class="tour-grid">
                        ${tourCard(SVG.sparkles, 'Sıfır Kaymalı Hover Dalgası', 'Fitts Yasası', 'ok', 'İkonlar üzerinde gezerken komşu simgeler sağa-sola kaymaz; tıklamak istediğin uygulama asla hedeften kaçmaz.')}
                        ${tourCard(SVG.layers, 'Sol / Sağ Dikey Ray & Akıllı Derle', '2×2 Mozaik', '', 'Dikey modda yalın squircle ray, 175 ms niyet gecikmeli yan etiketler, kategori mozaiği ve dikey sürükle-bırak sabitleme.')}
                        ${tourCard(SVG.desktop, 'Radyal Hub & 60 FPS DWM Önizleme', 'Tekerlek', '', 'Çok pencereli simgeye gelince dairesel küre ve canlı 60 FPS pencere önizlemesi açılır; fare tekerleğiyle pencereler arasında dön.')}
                        ${tourCard(SVG.moveWin, 'Alt + Sürükle Pencere & Dock', 'Alt+Tık', '', 'Herhangi bir pencereyi ortasından Alt+Sol Tık ile taşı, Alt+Sağ Tık ile boyutlandır; aynı hareketle Dock kenarını ve boyutunu (%75–%135) ayarla.')}
                        ${tourCard(SVG.grid, 'Shift + Sürükle Snap Zones', 'Cam Bölgeler', '', 'Pencere taşırken Shift’e basılı tut; 65/35 Kodlama, 50/50 Böl, Üçlü veya Zen odak bölgelerine pürüzsüzce yerleştir.')}
                        ${tourCard(SVG.folder, 'Klasör Yığınları & Sürükle-Uyandır', 'Drag & Drop', '', 'Dock’a klasör bırakıp hızlı açılır yığın oluştur; bir dosyayı açık pencere simgesinin üzerine tutunca o pencere anında öne gelir.')}
                    </div>`;
            } else if (tourStep === 3) {
                heroTitle = '4. Süper Raf, Yerel QR AirDrop & SQLite Pano Kasası';
                heroSub = 'Dosya aktarımı, kalıcı pano geçmişi, hızlı notlar ve odak zamanlayıcısı tek bir cam merkezde.';
                bodyHtml = `
                    <div class="tour-grid">
                        ${tourCard(SVG.inbox, '3 Bölgeli Üst Damla Sürükle-Bırak', 'Akıllı Drop', 'ok', 'Dosya sürüklerken Üst Damla 3 bölgeye açılır: Dosya Rafına Koy, QR ile Telefona Gönder veya Hızlı Dönüştür / ZIP.')}
                        ${tourCard(SVG.qr, 'Yerel Ağ QR AirDrop', 'Sıfır Bulut', '', 'Dosyaları internet kullanmadan aynı Wi-Fi’deki telefonuna yerel HTTP sunucusu ve anlık QR kod ile saniyeler içinde gönder.')}
                        ${tourCard(SVG.zip, 'Tek Hareketle Dönüştür & ZIP', 'PNG ↔ JPG', '', 'Tek görseli anında optimize JPG/PNG’ye çevir; birden fazla dosya veya klasörü bırakınca zaman damgalı ZIP arşivi oluştur.')}
                        ${tourCard(SVG.clip, 'SQLite Pano Kasası & Oto-Yapıştır', 'Kalıcı', '', 'Metin, ekran görüntüsü, HEX/RGB renk kartı ve link geçmişini saklar; Oto-Yapıştır açıkken tıkladığın öğe önceki pencereye direkt yapışır.')}
                        ${tourCard(SVG.timer, 'Sanal Masaüstü & Pomodoro Odak', 'M1/2 · 25 dk', '', 'Tek tıkla sanal masaüstleri (M1/2) arasında geç, hazır pencere düzenleri uygula ve 25/45/5 dk odak sayacını Üst Damla’dan izle.')}
                        ${tourCard(SVG.note, 'Hızlı Cep Notu & Mini Takvim', 'Oto-Kayıt', '', 'Fikir ve görevlerini anında not al (otomatik diske kaydedilir); Üst Damla saatine tıklayarak tarih bazlı ajanda notları ekle.')}
                    </div>`;
                tryBtnHtml = `<button class="pill-btn" onclick="tourJumpToDeck('shelf')">${SVG.layers}<span>Süper Raf & Pano’yu Aç</span></button>`;
            } else if (tourStep === 4) {
                heroTitle = '5. Üretkenlik Araçları, GIF Kaydedici & Canlı Telemetri';
                heroSub = 'Ekran kaydı, çevrimdışı metin okuma, piksel renk seçici ve gerçek zamanlı sistem performansı.';
                bodyHtml = `
                    <div class="tour-grid">
                        ${tourCard(SVG.record, '10 FPS Animasyonlu GIF Kaydedici', 'Bayer + Çerçeve', 'hot', 'Seçtiğin bölgeyi kayda girmeyen kırmızı çerçeve ve Bayer dithering ile net GIF89a olarak kaydedip otomatik panoya kopyalar.')}
                        ${tourCard(SVG.scanText, 'Çevrimdışı Ekran OCR', 'İnternetsiz', 'ok', 'Video, görsel veya kopyalanamayan herhangi bir ekran alanını çiz; Windows yerel OCR motoruyla metni anında panoya çıkar.')}
                        ${tourCard(SVG.pipette, 'Ekran Renk Damlalığı', 'HEX / RGB / HSL', '', 'Büyüteçli piksel seçiciyle ekrandaki herhangi bir rengin kodunu tek tıkla yakala ve Pano Kasası’nda renk kartı olarak gör.')}
                        ${tourCard(SVG.activity, 'Canlı Telemetri Eğrileri & Top-3 RAM', 'Çift-Tık Koru', '', '1.5 sn canlı CPU, RAM, Ağ akışı SVG eğrileri; en çok RAM harcayan 3 süreç için yanlışlıkla kapatmayı önleyen 3.5 sn onay kilidi.')}
                        ${tourCard(SVG.bell, 'Canlı Windows Bildirimleri & Tepsi', 'Tüm Uygulamalar', '', 'WhatsApp, Teams ve sistem bildirimlerini Üst Damla’da topla; arka planda çalışan sistem tepsisi uygulamalarını bellek kullanımıyla gör.')}
                        ${tourCard(SVG.bolt, 'Uyku Destekli Düşük Bellek Motoru', '301 MB / 3 Ekran', '', 'Tek merkezi imleç koordinatörü hareketsizken otomatik uykuya geçer; paylaşımlı tek WebView2 renderer ile çok düşük RAM tüketir.')}
                    </div>`;
                tryBtnHtml = `<button class="pill-btn" onclick="tourJumpToDeck('metrics')">${SVG.activity}<span>Telemetri Sekmesini Aç</span></button>`;
            } else {
                heroTitle = '6. OmniSearch, Kısayollar, Yedekleme & Otomatik Güncelleme';
                heroSub = 'Klavyeden elini kaldırmadan tüm bilgisayarını yönet ve yeni sürümleri tek tıkla al.';
                bodyHtml = `
                    <div class="tour-grid">
                        ${tourCard(SVG.search, `Evrensel OmniSearch (${escHtml(omniKey)})`, '250+ Uygulama', 'ok', 'Store/Win32 uygulamalar, yerel dosyalar, canlı hesap makinesi (18% of 4500), birim çevirici (100 km to mi) ve > komutları.')}
                        ${tourCard(SVG.palette, 'Çalışma Profilleri & Tek Dokunuş Win', 'İş / Oyun / Akış', '', 'Profiller arası tek tıkla tema/odak değiştir; istersen Stüdyo’dan tek dokunuş Windows tuşunu OmniSearch’e bağla (Win+D/E/L korunur).')}
                        ${tourCard(SVG.exportPkg, 'JSON Yedekleme & Geri Yükleme', 'Taşınabilir', '', 'Tüm ayarlar, seçili tema, sabitli kısayollar ve takvim notlarını tek JSON dosyasıyla yedekle veya başka bilgisayara aktar.')}
                        ${tourCard(SVG.refresh, 'GitHub SHA-256 Otomatik Güncelleme', `v${escHtml(appVer)}`, '', 'Yeni sürüm çıkınca Üst Damla’da bildirim al; Stüdyo → Kontrol Et ile ayarlarını kaybetmeden SHA-256 doğrulamalı tek tıkla güncelle.')}
                    </div>
                    <div class="tour-kbd-strip">
                        <div class="tour-kbd-item"><span>OmniSearch</span><span class="tour-kbd">${escHtml(omniKey)}</span></div>
                        <div class="tour-kbd-item"><span>Kontrol Merkezi</span><span class="tour-kbd">Ctrl+Alt+A</span></div>
                        <div class="tour-kbd-item"><span>Stüdyo & Ayarlar</span><span class="tour-kbd">Ctrl+Alt+S</span></div>
                        <div class="tour-kbd-item"><span>Pencere / Dock Taşı</span><span class="tour-kbd">Alt+Sol Tık</span></div>
                        <div class="tour-kbd-item"><span>Pencere / Dock Boyut</span><span class="tour-kbd">Alt+Sağ Tık</span></div>
                        <div class="tour-kbd-item"><span>Snap Zones</span><span class="tour-kbd">Shift+Sürükle</span></div>
                    </div>`;
                tryBtnHtml = `<button class="pill-btn" onclick="tourJumpToOmni()">${SVG.search}<span>OmniSearch’ü Dene</span></button>`;
            }

            const progPct = Math.round(((tourStep + 1) / 6) * 100);
            el.className = 'glass-shell open';
            el.innerHTML = `
                <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;">
                    <div style="display:flex;align-items:center;gap:10px;">
                        <div class="orb">✦</div>
                        <div>
                            <div style="font-size:15px;font-weight:800;letter-spacing:-0.015em;">Project Aurora v${escHtml(appVer)} — Kapsamlı Özellik Turu</div>
                            <div style="font-size:11px;color:var(--text-2);">Adım ${tourStep + 1} / 6 · Yeni nesil Dinamik Ada, Kristal Dock ve tüm üretkenlik araçları</div>
                        </div>
                    </div>
                    <div style="display:flex;align-items:center;gap:6px;">
                        <span class="pill-btn" style="height:24px;padding:0 9px;font-size:10.5px;pointer-events:none;">${tourStep + 1} / 6</span>
                        <button class="micro-btn" onclick="finishOnboarding()" title="Turu Kapat (ESC)">${SVG.close}</button>
                    </div>
                </div>

                <div class="tour-nav">
                    ${navTabs.map((t, i) => `
                        <button class="tour-nav-btn ${i === tourStep ? 'active' : ''}" onclick="setTourStep(${i})">
                            <span class="tour-nav-num">${t.n}</span>
                            <span>${t.label}</span>
                        </button>`).join('')}
                </div>

                <div class="tour-hero">
                    <div style="min-width:0;">
                        <div class="tour-hero-title">${heroTitle}</div>
                        <div class="tour-hero-sub">${heroSub}</div>
                    </div>
                    ${tryBtnHtml}
                </div>

                <div class="tour-body">
                    ${bodyHtml}
                </div>

                <div class="tour-footer">
                    <div style="display:flex;align-items:center;gap:10px;">
                        <div class="tour-prog-track"><div class="tour-prog-fill" style="width:${progPct}%;"></div></div>
                        <span style="font-size:10.5px;color:var(--text-2);">← / → Adım Değiştir · ESC Kapat</span>
                    </div>
                    <div style="display:flex;align-items:center;gap:7px;">
                        <button class="pill-btn" style="height:31px;padding:0 13px;font-size:11.5px;${tourStep===0?'opacity:0.42;pointer-events:none;':''}" onclick="stepTour(-1)">
                            ${SVG.chevL}<span>Önceki</span>
                        </button>
                        ${tourStep < 5 ? `
                        <button class="pill-btn accent" style="height:31px;padding:0 15px;font-size:11.5px;" onclick="stepTour(1)">
                            <span>Sonraki Adım</span>${SVG.chevR}
                        </button>` : `
                        <button class="pill-btn accent" style="height:31px;padding:0 16px;font-size:11.5px;" onclick="finishOnboarding()">
                            ${SVG.sparkles}<span>Turu Tamamla ve Başla</span>
                        </button>`}
                    </div>
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
                        ${isUrl ? `<button class="micro-btn" onclick="event.stopPropagation();openClipUrlById(${c.id})" title="Tarayıcıda Aç">${SVG.extLink}</button>` : ''}
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

            window.addEventListener('keydown', e => {
                if (!isOnboardingVisible()) return;
                if (e.key === 'Escape') { e.preventDefault(); finishOnboarding(); }
                else if (e.key === 'ArrowRight') { e.preventDefault(); stepTour(1); }
                else if (e.key === 'ArrowLeft') { e.preventDefault(); stepTour(-1); }
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
            } else if (msg.type === 'openTour') {
                openOnboardingTour(msg.step || 0);
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
        """";
}
