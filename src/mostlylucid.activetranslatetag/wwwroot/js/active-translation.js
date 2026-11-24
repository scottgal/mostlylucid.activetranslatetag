// SignalR carries readiness only. HTMX owns requests and DOM swaps.
(() => {
    "use strict";
    const script = document.querySelector("script[data-active-translation-hub]");
    if (!script || !window.htmx || !window.signalR) return;
    const hub = new window.signalR.HubConnectionBuilder()
        .withUrl(script.dataset.activeTranslationHub)
        .withAutomaticReconnect().build();
    const watched = new Map();
    let seen = new WeakSet();
    const pending = () => [...document.querySelectorAll('[data-translation-state="pending"][data-translation-token]')];
    let connected = false;
    let synchronizing = false;
    let rescan = false;
    const updateStatus = (message) => {
        const count = pending().length;
        const failed = document.querySelectorAll('[data-translation-state="failed"]').length;
        document.querySelectorAll("[data-active-translation-status]").forEach(el => {
            const text = message || (failed ? "Translation unavailable for some content. Original text retained; reload to retry." : count ? `Translating ${count} piece${count === 1 ? "" : "s"} of content…` : "Translations ready.");
            if (el.textContent !== text) el.textContent = text;
        });
    };
    const matching = id => pending().filter(el => el.dataset.translationId === id);
    hub.on("TranslationReady", ({ id }) => {
        for (const el of matching(id)) window.htmx.trigger(el, "translation-ready");
    });
    hub.on("TranslationFailed", ({ id }) => {
        for (const el of matching(id)) {
            el.dataset.translationState = "failed";
            el.setAttribute("aria-busy", "false");
        }
        updateStatus("Translation unavailable for some content. Original text retained; reload to retry.");
        synchronize();
    });
    // Also handles forum posts inserted after initial page load and removes old subscriptions.
    async function synchronize() {
        updateStatus();
        if (!connected) return;
        if (synchronizing) { rescan = true; return; }
        synchronizing = true;
        try {
            do {
                rescan = false;
                const elements = pending();
                const added = new Set(elements.filter(el => !seen.has(el)).map(el => el.dataset.translationId));
                const current = new Map(elements.map(el => [el.dataset.translationId, el.dataset.translationToken]));
                elements.forEach(el => {
                    if (!seen.has(el)) window.htmx.process(el);
                    seen.add(el);
                });
                for (const [id, token] of watched) {
                    if (!current.has(id)) { await hub.invoke("Unwatch", token); watched.delete(id); }
                }
                for (const [id, token] of current) {
                    if (!watched.has(id) || added.has(id)) {
                        watched.set(id, token);
                        try { await hub.invoke("Watch", token); }
                        catch (error) { watched.delete(id); throw error; }
                    }
                }
            } while (rescan);
        } catch (error) {
            console.error("Translation subscription failed", error);
            updateStatus("Translation updates interrupted. Original text retained.");
        } finally { synchronizing = false; }
    }
    document.body.addEventListener("translation-swapped", event => {
        const el = event.target.closest("[data-translation-id]");
        if (!el) return;
        el.dataset.translationState = "ready";
        el.lang = el.dataset.translationLanguage;
        el.setAttribute("aria-busy", "false");
        synchronize();
    });
    const failedRequest = event => {
        const el = event.detail?.ctx?.sourceElement || event.target;
        if (el?.dataset?.translationState !== "pending") return;
        el.dataset.translationState = "failed";
        el.setAttribute("aria-busy", "false");
        synchronize();
    };
    document.body.addEventListener("htmx:error", failedRequest);
    document.body.addEventListener("htmx:response:error", failedRequest);
    new MutationObserver(records => {
        if (records.some(record => !record.target.closest?.("[data-active-translation-status]") && (record.addedNodes.length || record.removedNodes.length))) synchronize();
    }).observe(document.body, { childList: true, subtree: true });
    hub.onreconnecting(() => { connected = false; watched.clear(); seen = new WeakSet(); updateStatus("Reconnecting translation updates…"); });
    hub.onreconnected(() => { connected = true; watched.clear(); seen = new WeakSet(); synchronize(); });
    hub.onclose(() => { connected = false; watched.clear(); seen = new WeakSet(); start(); });
    async function start() {
        try { await hub.start(); connected = true; await synchronize(); }
        catch (error) { updateStatus("Connecting translation updates…"); setTimeout(start, 3000); }
    }
    start();
})();
