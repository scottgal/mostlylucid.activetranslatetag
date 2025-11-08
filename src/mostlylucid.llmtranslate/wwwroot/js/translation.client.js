/**
 * Minimal client-only translation helper (vanilla JS).
 * Works without any server endpoints. Provide static JSON files under `/translations/{lang}.json`.
 *
 * Usage:
 * - Include: <script src="/js/translation.client.js"></script>
 * - Call: TranslationClient.init({ baseUrl: '/translations', defaultLang: 'en' })
 * - Mark elements: <span data-translate-key="home.title">Welcome</span>
 * - Switch: TranslationClient.setLanguage('fr')
 */
(function (global) {
  const state = {
    defaultLang: 'en',
    currentLang: 'en',
    baseUrl: '/translations', // folder with {lang}.json files
    cache: {}, // { lang: { key: value } }
    loading: false,
    debug: false
  };

  function normalizeMap(input) {
    // Accept either object map { key: "text" } or array [{ key, text|translatedText }]
    if (!input) return {};
    if (Array.isArray(input)) {
      const out = {};
      for (const item of input) {
        if (!item) continue;
        const k = item.key || item.Key;
        const v = item.text || item.translatedText || item.Text || item.TranslatedText;
        if (k && typeof v === 'string') out[k] = v;
      }
      return out;
    }
    return input; // assume already { key: value }
  }

  async function loadLang(lang) {
    if (state.cache[lang]) return state.cache[lang];
    const url = `${state.baseUrl}/${encodeURIComponent(lang)}.json`;
    const res = await fetch(url, { cache: 'no-cache' });
    if (!res.ok) throw new Error(`Failed to load translations for ${lang}`);
    const json = await res.json();
    state.cache[lang] = normalizeMap(json) || {};
    return state.cache[lang];
  }

  async function applyTranslations(lang) {
    const map = await loadLang(lang);
    const elements = document.querySelectorAll('[data-translate-key]');
    elements.forEach(el => {
      const key = el.getAttribute('data-translate-key');
      const text = map[key];
      if (typeof text === 'string' && text.length > 0) {
        el.innerText = text;
      }
    });
    const badges = document.querySelectorAll('#current-lang, [data-current-lang]');
    badges.forEach(b => b.textContent = (lang || 'en').toUpperCase());
  }

  function getCookieLang() {
    const match = (document.cookie.match(/(?:^|; )preferred-language=([^;]*)/)||[])[1];
    return decodeURIComponent(match || '');
  }

  const TranslationClient = {
    init: function (opts) {
      opts = opts || {};
      if (opts.baseUrl) state.baseUrl = opts.baseUrl;
      if (opts.defaultLang) state.defaultLang = opts.defaultLang;
      if (typeof opts.debug === 'boolean') state.debug = opts.debug;

      const cookieLang = getCookieLang();
      state.currentLang = cookieLang || state.defaultLang || 'en';

      if (state.currentLang && state.currentLang.toLowerCase() !== 'en') {
        this.setLanguage(state.currentLang);
      } else {
        const badges = document.querySelectorAll('#current-lang, [data-current-lang]');
        badges.forEach(b => b.textContent = 'EN');
      }
    },
    setLanguage: async function (lang) {
      if (!lang || state.loading) return;
      try {
        state.loading = true;
        await applyTranslations(lang);
        state.currentLang = lang;
        document.cookie = `preferred-language=${encodeURIComponent(lang)}; path=/; max-age=31536000; SameSite=Lax`;
      } finally {
        state.loading = false;
      }
    },
    getCurrentLanguage: function() {
      return state.currentLang || getCookieLang() || state.defaultLang || 'en';
    },
    translatePage: function(lang) {
      const target = lang || this.getCurrentLanguage();
      return applyTranslations(target);
    }
  };

  // Auto-init from window.translationConfig if present
  if (global.translationConfig && (global.translationConfig.baseUrl || global.translationConfig.defaultLang)) {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', () => TranslationClient.init(global.translationConfig));
    } else {
      TranslationClient.init(global.translationConfig);
    }
  }

  global.TranslationClient = TranslationClient;
})(window);
