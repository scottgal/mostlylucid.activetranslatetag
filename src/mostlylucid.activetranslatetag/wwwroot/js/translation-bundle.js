/**
 * LLM Translation System - Complete Bundle
 * Includes SignalR connection and all translation functionality
 * No external dependencies required
 */

// Include SignalR client library inline (for standalone operation)
// In production, you should include the actual SignalR client library here
// or load it from CDN via the translation-scripts tag helper

(function(window) {
    'use strict';

    /**
     * Translation Manager - Handles HTMX-based translation switching
     */
    class TranslationManager {
        constructor(options = {}) {
            this.currentLanguage = this.getCurrentLanguage();
            this.isTranslating = false;
            this.debug = options.debug || false;
            this.signalRHub = options.signalRHub || '/hubs/translation';
            this.enableNotifications = options.enableNotifications !== false;
            this.signalRConnection = null;

            if (this.debug) {
                console.log('[Translation] Initializing with options:', options);
            }
        }

        getCurrentLanguage() {
            const value = `; ${document.cookie}`;
            const parts = value.split(`; preferred-language=`);
            if (parts.length === 2) {
                return parts.pop().split(';').shift();
            }
            return 'en';
        }

        /**
         * Collect all translation keys from the current page
         */
        collectTranslationKeys() {
            const elements = document.querySelectorAll('[data-translate-key]');
            return Array.from(elements).map(el => el.getAttribute('data-translate-key'));
        }

        /**
         * Switch language using HTMX OOB swaps
         */
        async switchLanguageHtmx(languageCode) {
            if (this.isTranslating) {
                if (this.debug) console.log('[Translation] Already translating, ignoring request');
                return;
            }

            try {
                this.isTranslating = true;
                this.showLoadingIndicator();

                const keys = this.collectTranslationKeys();

                if (keys.length === 0) {
                    if (this.debug) console.log('[Translation] No translatable content on this page');
                    this.currentLanguage = languageCode;
                    document.cookie = `preferred-language=${languageCode}; path=/; max-age=31536000; SameSite=Lax`;
                    this.updateCurrentLanguageDisplay(languageCode);
                    return;
                }

                const formData = new FormData();
                keys.forEach(key => formData.append('keys', key));

                const response = await fetch(`/Language/Switch/${languageCode}`, {
                    method: 'POST',
                    body: formData
                });

                if (!response.ok) {
                    throw new Error(`Failed to switch language: ${response.statusText}`);
                }

                const html = await response.text();
                const temp = document.createElement('div');
                temp.innerHTML = html;

                let updatedCount = 0;
                temp.querySelectorAll('[hx-swap-oob]').forEach(element => {
                    const targetId = element.id;
                    const target = document.getElementById(targetId);

                    if (target) {
                        target.innerHTML = element.innerHTML;
                        this.animateTranslationUpdate(target);
                        updatedCount++;
                    }
                });

                this.currentLanguage = languageCode;
                this.updateCurrentLanguageDisplay(languageCode);

                if (this.debug) {
                    console.log(`[Translation] Language switched to ${languageCode} (${updatedCount}/${keys.length} elements updated)`);
                }

                if (this.enableNotifications) {
                    this.showNotification(`Language changed to ${this.getLanguageName(languageCode)}`, 'success');
                }

            } catch (error) {
                console.error('[Translation] Error switching language:', error);
                if (this.enableNotifications) {
                    this.showNotification('Failed to switch language', 'error');
                }
            } finally {
                this.isTranslating = false;
                this.hideLoadingIndicator();
            }
        }

        async switchLanguage(languageCode) {
            if (languageCode === this.currentLanguage) {
                if (this.debug) console.log('[Translation] Already in this language');
                return;
            }

            if (languageCode === 'en') {
                document.cookie = `preferred-language=en; path=/; max-age=31536000; SameSite=Lax`;
                window.location.reload();
                return;
            }

            await this.switchLanguageHtmx(languageCode);
        }

        animateTranslationUpdate(element) {
            element.style.transition = 'background-color 0.5s ease';
            element.style.backgroundColor = '#ffffcc';
            setTimeout(() => {
                element.style.backgroundColor = '';
                setTimeout(() => {
                    element.style.transition = '';
                }, 500);
            }, 500);
        }

        updateCurrentLanguageDisplay(langCode) {
            const displays = document.querySelectorAll('#current-lang, [data-current-lang]');
            displays.forEach(display => {
                try {
                    display.textContent = (langCode || 'en').toUpperCase();
                } catch {
                    display.textContent = langCode;
                }
            });
        }

        showLoadingIndicator() {
            const indicators = document.querySelectorAll('#translation-loading-indicator, [data-translation-loading]');
            indicators.forEach(indicator => indicator.classList.remove('d-none'));

            let indicator = document.getElementById('translation-loading');
            if (!indicator) {
                indicator = document.createElement('div');
                indicator.id = 'translation-loading';
                indicator.className = 'toast-container position-fixed top-0 end-0 p-3';
                indicator.innerHTML = `
                    <div class="toast show" role="alert">
                        <div class="toast-body d-flex align-items-center gap-2">
                            <div class="spinner-border spinner-border-sm" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                            <span>Loading translations...</span>
                        </div>
                    </div>
                `;
                document.body.appendChild(indicator);
            } else {
                indicator.style.display = 'block';
            }
        }

        hideLoadingIndicator() {
            const indicators = document.querySelectorAll('#translation-loading-indicator, [data-translation-loading]');
            indicators.forEach(indicator => indicator.classList.add('d-none'));

            const indicator = document.getElementById('translation-loading');
            if (indicator) {
                setTimeout(() => {
                    indicator.style.display = 'none';
                }, 300);
            }
        }

        showNotification(message, type = 'info') {
            const container = document.getElementById('translation-notifications') || this.createNotificationContainer();
            const notification = document.createElement('div');
            notification.className = `alert alert-${type === 'error' ? 'danger' : type === 'success' ? 'success' : 'info'} alert-dismissible fade show`;
            notification.innerHTML = `
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;
            container.appendChild(notification);

            setTimeout(() => {
                notification.classList.remove('show');
                setTimeout(() => notification.remove(), 150);
            }, 3000);
        }

        createNotificationContainer() {
            const container = document.createElement('div');
            container.id = 'translation-notifications';
            container.className = 'position-fixed top-0 end-0 p-3';
            container.style.zIndex = '1060';
            document.body.appendChild(container);
            return container;
        }

        getLanguageName(code) {
            const names = {
                'en': 'English', 'es': 'Español', 'fr': 'Français', 'de': 'Deutsch',
                'it': 'Italiano', 'pt': 'Português', 'ru': 'Русский', 'ja': '日本語',
                'ko': '한국어', 'zh': '中文', 'ar': 'العربية', 'hi': 'हिन्दी'
            };
            return names[code.toLowerCase()] || (code || 'en').toUpperCase();
        }

        /**
         * Initialize SignalR connection for real-time updates
         */
        initializeSignalR() {
            const cfg = (window.translationConfig || {});
            if (cfg.enableSignalR === false) {
                if (this.debug) console.warn('[Translation] SignalR disabled by config');
                return;
            }
            if (typeof signalR === 'undefined') {
                if (this.debug) console.warn('[Translation] SignalR not available, skipping real-time updates');
                return;
            }

            try {
                if (window.__translationHubConnected) {
                    if (this.debug) console.log('[Translation] SignalR already initialized');
                    return;
                }
                this.signalRConnection = new signalR.HubConnectionBuilder()
                    .withUrl(this.signalRHub)
                    .withAutomaticReconnect()
                    .build();

                this.signalRConnection.on('StringTranslated', (data) => {
                    if (this.debug) console.log('[Translation] String translated:', data);

                    const elementId = `t-${this.simpleHash(data.key)}`;
                    const element = document.getElementById(elementId);

                    if (element && data.languageCode === this.currentLanguage) {
                        element.innerHTML = data.translatedText;
                        this.animateTranslationUpdate(element);
                    }
                });

                this.signalRConnection.on('TranslationProgress', (data) => {
                    if (this.debug) console.log('[Translation] Progress:', data);
                    this.updateProgressToast(data);
                });

                this.signalRConnection.on('TranslationComplete', (data) => {
                    if (this.debug) console.log('[Translation] Complete:', data);
                    this.hideProgressToast(true);

                    if (this.enableNotifications) {
                        this.showNotification(`${data.translatedCount} translations completed`, 'success');
                    }
                });

                this.signalRConnection.start()
                    .then(() => {
                        window.__translationHubConnected = true;
                        if (this.debug) console.log('[Translation] SignalR connected');
                    })
                    .catch(err => {
                        console.error('[Translation] SignalR connection error:', err);
                    });

            } catch (error) {
                console.error('[Translation] Error initializing SignalR:', error);
            }
        }

        // Progress Toast UI (bottom-right, closable)
        ensureProgressToast() {
            if (sessionStorage.getItem('translationToastDismissed') === '1') return null;
            let toast = document.getElementById('translation-progress-toast');
            if (toast) return toast;

            const container = document.createElement('div');
            container.id = 'translation-progress-toast';
            container.className = 'position-fixed bottom-0 end-0 p-3';
            container.style.zIndex = '1060';
            container.style.maxWidth = '360px';
            container.setAttribute('role', 'status');
            container.setAttribute('aria-live', 'polite');

            container.innerHTML = `
                <div class="toast show" style="min-width:280px;" data-bs-autohide="false">
                    <div class="toast-header">
                        <strong class="me-auto">Translating…</strong>
                        <small id="translation-progress-text">0 / 0 (0%)</small>
                        <button type="button" class="btn-close ms-2 mb-1" aria-label="Close"></button>
                    </div>
                    <div class="toast-body">
                        <div class="progress" role="progressbar" aria-valuemin="0" aria-valuemax="100">
                            <div class="progress-bar" id="translation-progress-bar" style="width: 0%"></div>
                        </div>
                        <div class="mt-2 small text-muted" id="translation-progress-current"></div>
                    </div>
                </div>`;

            document.body.appendChild(container);

            const closeBtn = container.querySelector('.btn-close');
            closeBtn?.addEventListener('click', () => {
                sessionStorage.setItem('translationToastDismissed', '1');
                container.remove();
            });

            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') {
                    sessionStorage.setItem('translationToastDismissed', '1');
                    container.remove();
                }
            }, { once: true });

            return container;
        }

        updateProgressToast(data) {
            const toast = this.ensureProgressToast();
            if (!toast) return;
            const bar = toast.querySelector('#translation-progress-bar');
            const text = toast.querySelector('#translation-progress-text');
            const cur = toast.querySelector('#translation-progress-current');
            if (bar) bar.style.width = `${data.percentage}%`;
            if (text) text.textContent = `${data.completed} / ${data.total} (${Math.round(data.percentage)}%)`;
            if (cur) cur.textContent = data.currentKey ? `Current: ${data.currentKey}` : '';
        }

        hideProgressToast(completed = false) {
            const toast = document.getElementById('translation-progress-toast');
            if (!toast) return;
            if (completed) {
                const header = toast.querySelector('.toast-header .me-auto');
                if (header) header.textContent = 'Translations complete';
            }
            setTimeout(() => {
                toast.remove();
            }, completed ? 1500 : 300);
        }

        simpleHash(str) {
            let hash = 0;
            for (let i = 0; i < str.length; i++) {
                const char = str.charCodeAt(i);
                hash = ((hash << 5) - hash) + char;
                hash |= 0;
            }
            return Math.abs(hash).toString(16).substring(0, 16).padStart(16, '0');
        }

        initialize() {
            this.updateCurrentLanguageDisplay(this.currentLanguage);

            const desiredLang = this.currentLanguage || 'en';
            if (desiredLang && desiredLang.toLowerCase() !== 'en') {
                setTimeout(() => {
                    this.switchLanguageHtmx(desiredLang);
                }, 100);
            }

            this.initializeSignalR();

            if (this.debug) {
                console.log(`[Translation] System initialized (language: ${this.currentLanguage})`);
            }
        }
    }

    // Global API
    window.TranslationManager = TranslationManager;

    // Auto-initialize with config from tag helper or defaults
    const config = window.translationConfig || {};
    window.translationManager = new TranslationManager({
        debug: config.debug || false,
        signalRHub: config.signalRHub || '/hubs/translation',
        enableNotifications: config.enableNotifications !== false
    });

    // Provide a simple, vanilla-friendly facade to match client-only API
    window.TranslationClient = {
        init: function (opts = {}) {
            if (opts.debug != null) window.translationManager.debug = !!opts.debug;
            if (opts.signalRHub) window.translationManager.signalRHub = opts.signalRHub;
            if (opts.enableNotifications != null) window.translationManager.enableNotifications = !!opts.enableNotifications;
            const desired = (opts.defaultLang) ? String(opts.defaultLang) : window.translationManager.getCurrentLanguage();
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', () => {
                    window.translationManager.initialize();
                    if (desired && desired.toLowerCase() !== 'en') {
                        window.translationManager.switchLanguageHtmx(desired);
                    }
                }, { once: true });
            } else {
                window.translationManager.initialize();
                if (desired && desired.toLowerCase() !== 'en') {
                    window.translationManager.switchLanguageHtmx(desired);
                }
            }
        },
        setLanguage: function (lang) {
            return window.translationManager.switchLanguage(lang);
        },
        getCurrentLanguage: function () {
            return window.translationManager.getCurrentLanguage();
        },
        translatePage: function (lang) {
            const target = lang || window.translationManager.currentLanguage || 'en';
            return window.translationManager.switchLanguageHtmx(target);
        }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            window.translationManager.initialize();
        });
    } else {
        window.translationManager.initialize();
    }

    // Global function for language switching (backward compatibility)
    window.setLanguage = function(languageCode) {
        window.translationManager.switchLanguage(languageCode);
    };

})(window);
