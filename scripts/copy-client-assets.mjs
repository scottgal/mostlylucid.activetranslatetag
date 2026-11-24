import { mkdir, copyFile } from 'node:fs/promises';
const root = new URL('../', import.meta.url);
for (const [source, destination] of [
    ['node_modules/htmx.org/dist/htmx.min.js', 'src/mostlylucid.activetranslatetag.Demo/wwwroot/lib/htmx/htmx.min.js'],
    ['node_modules/@microsoft/signalr/dist/browser/signalr.min.js', 'src/mostlylucid.activetranslatetag.Demo/wwwroot/lib/signalr/signalr.min.js']
]) {
    const target = new URL(destination, root);
    await mkdir(new URL('./', target), { recursive: true });
    await copyFile(new URL(source, root), target);
}
