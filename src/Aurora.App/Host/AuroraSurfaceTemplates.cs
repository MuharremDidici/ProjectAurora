namespace Aurora.App.Host;

public static partial class AuroraSurfaceTemplates
{
    public static string BuildCrownHtml() => $$$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="UTF-8">
        <style>
        {{{SharedDesignCss}}}
        {{{CrownCss}}}
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

        {{{CrownScript}}}
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
        {{{DockCss}}}
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

        {{{DockScript}}}
        </script>
        </body>
        </html>
        """;
}
