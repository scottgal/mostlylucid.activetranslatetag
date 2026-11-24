import { test } from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFile } from 'node:fs/promises';
const code = await readFile(new URL('../src/mostlylucid.activetranslatetag/wwwroot/js/active-translation.js', import.meta.url), 'utf8');
const flush = async () => { for (let i = 0; i < 8; i++) await new Promise(resolve => setImmediate(resolve)); };

function harness(initial, cached = new Set()) {
    const elements = [...initial];
    const events = new Map();
    const calls = [], swaps = [];
    let mutation;
    const status = { textContent: '' };
    const hub = {
        handlers: {}, on(name, callback) { this.handlers[name] = callback; },
        onreconnecting(callback) { this.reconnecting = callback; },
        onreconnected(callback) { this.reconnected = callback; }, onclose() {},
        async start() {},
        async invoke(method, token) {
            calls.push([method, token]);
            if (method === 'Watch' && cached.has(token)) this.handlers.TranslationReady({ id: token });
        }
    };
    class Builder { withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return hub; } }
    const document = {
        querySelector() { return { dataset: { activeTranslationHub: '/hub' } }; },
        querySelectorAll(selector) {
            if (selector === '[data-active-translation-status]') return [status];
            if (selector === '[data-translation-state="failed"]') return elements.filter(el => el.dataset.translationState === 'failed');
            return elements.filter(el => el.dataset.translationState === 'pending');
        },
        body: { addEventListener(name, callback) { events.set(name, callback); } }
    };
    vm.runInNewContext(code, {
        window: { signalR: { HubConnectionBuilder: Builder }, htmx: { process() {}, trigger(el, name) { swaps.push([el, name]); } } },
        document, console, setTimeout() {},
        MutationObserver: class { constructor(callback) { mutation = callback; } observe() {} }
    });
    return { elements, calls, swaps, hub, status,
        mutate() { mutation([{ target: { closest() { return null; } }, addedNodes: [1], removedNodes: [] }]); },
        failedRequest(el) { events.get('htmx:response:error')({ detail: { ctx: { sourceElement: el } } }); },
        swapped(el) { el.dataset.translationState = 'ready'; events.get('translation-swapped')({ target: el }); }
    };
}
function element(id) {
    return { dataset: { translationId: id, translationToken: id, translationState: 'pending', translationLanguage: 'fr' },
        setAttribute(name, value) { this[name] = value; }, closest() { return this; } };
}

test('one watch per content key; readiness triggers HTMX for every occurrence', async () => {
    const a = element('same'), b = element('same');
    const h = harness([a, b]); await flush();
    assert.equal(h.calls.filter(([method]) => method === 'Watch').length, 1);
    h.hub.handlers.TranslationReady({ id: 'same' });
    assert.equal(h.swaps.length, 2);
    assert.ok(h.swaps.every(([, event]) => event === 'translation-ready'));
    h.swapped(a); h.swapped(b); await flush();
    assert.equal(a.lang, 'fr'); assert.equal(a['aria-busy'], 'false');
    assert.ok(h.calls.some(([method]) => method === 'Unwatch'));
});

test('a post inserted after readiness rechecks the cached translation', async () => {
    const original = element('same');
    const h = harness([original], new Set(['same'])); await flush();
    assert.equal(h.swaps.length, 1);
    const inserted = element('same'); h.elements.push(inserted); h.mutate(); await flush();
    assert.equal(h.calls.filter(([method]) => method === 'Watch').length, 2);
    assert.ok(h.swaps.some(([el]) => el === inserted));
});

test('reconnection reestablishes watches for pending posts', async () => {
    const h = harness([element('one')]); await flush();
    h.hub.reconnecting(); h.hub.reconnected(); await flush();
    assert.equal(h.calls.filter(([method]) => method === 'Watch').length, 2);
});

test('failed translations retain source and show failure status', async () => {
    const source = element('one');
    const h = harness([source]); await flush();
    h.hub.handlers.TranslationFailed({ id: 'one' }); await flush();
    assert.equal(source.dataset.translationState, 'failed');
    assert.equal(source['aria-busy'], 'false');
    assert.equal(h.swaps.length, 0);
    assert.match(h.status.textContent, /unavailable/);
});


test('a failed fragment request releases busy state without another swap', async () => {
    const source = element('one');
    const h = harness([source]); await flush();
    h.failedRequest(source); await flush();
    assert.equal(source.dataset.translationState, 'failed');
    assert.equal(source['aria-busy'], 'false');
    assert.equal(h.swaps.length, 0);
    assert.match(h.status.textContent, /unavailable/);
});
