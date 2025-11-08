/**
 * Minimal client-only translation helper demo copy.
 */
(function (global) {
  const state = {
    defaultLang: 'en',
    currentLang: 'en',
    baseUrl: '/translations',
    cache: {},
    loading: false
  };

  async function loadLang(lang) {
    if (state.cache[lang]) return state.cache[lang];
    const res = await fetch(`${state.baseUrl}/${encodeURIComponent(lang)}.json`, { cache: 'no-cache' });
    if (!res.ok) throw new Error('Failed to load translations');
    const json = await res.json();
    state.cache[lang] = json || {};
    return state.cache[lang];
  }

  async function apply(lang) {
    const map = await loadLang(lang);
    document.querySelectorAll('[data-translate-key]').forEach(el => {
      const key = el.getAttribute('data-translate-key');
      const text = map[key];
      if (typeof text === 'string') el.innerText = text;
    });
    const badge = document.getElementById('current-lang');
    if (badge) badge.innerText = (lang || 'en').toUpperCase();
  }

  const TranslationClient = {
    init: function (opts) {
      if (opts?.baseUrl) state.baseUrl = opts.baseUrl;
      if (opts?.defaultLang) state.defaultLang = opts.defaultLang;
      const cookieLang = (document.cookie.match(/(?:^|; )preferred-language=([^;]*)/)||[])[1];
      state.currentLang = decodeURIComponent(cookieLang || state.defaultLang || 'en');
      if (state.currentLang.toLowerCase() !== 'en') this.setLanguage(state.currentLang);
      else {
        const badge = document.getElementById('current-lang');
        if (badge) badge.innerText = 'EN';
      }
    },
    setLanguage: async function (lang) {
      if (!lang || state.loading) return;
      try { state.loading = true; await apply(lang); state.currentLang = lang; document.cookie = `preferred-language=${encodeURIComponent(lang)}; path=/; max-age=31536000; SameSite=Lax`; }
      finally { state.loading = false; }
    }
  };

  global.TranslationClient = TranslationClient;
})(window);
