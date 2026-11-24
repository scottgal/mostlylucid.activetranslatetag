# Active translation: Tag Helpers, View Components, and HTMX 4

An ASP.NET Core tutorial using translation to demonstrate **dynamic content replacement**.
A forum message renders immediately in its original language. A small generative LLM translates
it in a hosted worker. SignalR announces that the translation is available; HTMX fetches that
single HTML fragment and swaps it in.

```cshtml
<active-translate language="fr" source-language="en" description="An informal forum reply">
    <p>@Model.Message</p>
</active-translate>
```

Razor encodes `Model.Message` as usual. No JavaScript call, translation key, or database ID is required.

## Run the example

Requires the .NET 11 SDK (11.0.100 or a later .NET 11 SDK) and Node.js.

```sh
npm ci
npm run build
dotnet run --project src/mostlylucid.activetranslatetag.Demo
```

The default **Sample** provider contains fixed French examples. It deliberately delays responses
so you can see the active swaps; it does not run a model or claim translation quality.
Refresh after completion to see SQLite cache hits render immediately.

```sh
dotnet test 'mostlylucid.activetranslatetag .sln'
npm test
```

Historical EasyNMT live-server tests are skipped. No conventional machine translation engine
is used by this example. Older provider/storage classes remain for reference; the active path
below has its own direct SQLite store and hosted queue.

## Part 1: server rendering and View Components

`ActiveTranslateTagHelper` captures the Razor child content and delegates to `ActiveTranslateViewComponent`.
The component preserves original markup with placeholders and builds a SHA-256 key from source content, languages, description and model/prompt
cache namespace. Add `mode="server"` to wait for the translated result during rendering:

```cshtml
<active-translate language="fr" mode="server"><p>Hello, forum!</p></active-translate>
@await Component.InvokeAsync("ActiveTranslationStatus")
```

The custom Tag Helper is a small adapter to the View Component; the component prepares the view model,
and its `Default.cshtml` owns the wrapper markup. The separate status component demonstrates the built-in
`<vc:active-translation-status>` syntax. A parameter-driven component can use built-in `<vc:...>` directly;
a custom wrapper remains useful when capturing arbitrary child Razor content. The default `active` mode avoids waiting for inference in the HTTP response.
Cached content is immediate in either mode. A provider failure retains the original content.

## Part 2: active fragments with HTMX 4 + SignalR

For an uncached message, the Tag Helper emits a wrapper like:

```html
<div id="translation-unique-instance"
     data-translation-id="content-and-language-hash"
     data-translation-token="short-lived-protected-capability"
     hx-get="/_active-translate/fragment?token=..."
     hx-trigger="translation-ready"
     hx-swap="innerHTML"
     hx-sync="this:replace"
     aria-live="polite" aria-busy="true">
    <p>Hello, forum!</p>
</div>
```

1. The helper records the source in SQLite and queues its content key. Repeated content shares one job.
2. The browser connects to `ActiveTranslationHub` and watches that protected capability.
3. The hosted worker creates a DI scope, runs inference, validates placeholders and saves the HTML.
4. SignalR sends **only an ID**, to connections watching that ID. It sends neither model output nor forum text.
5. The small client bridge triggers `translation-ready` on every matching wrapper.
6. **HTMX**, not the bridge, issues the GET and swaps the returned HTML.

The worker uses a C# 15 union for ready and failed results. A typed SignalR client contract keeps those notifications consistent between the worker and the hub. The hub only subscribes and reports status. The browser fetches the completed HTML from a read-only Minimal API GET. Only the Razor render path queues inference.

The hub joins the group before rechecking SQLite. This closes the render/connect race and recovers
missed completion events after reconnect. A MutationObserver subscribes posts added after page load
and removes subscriptions for detached posts. Each occurrence has its own DOM ID.

HTMX and SignalR are pinned npm dependencies copied locally by `npm run build`; no floating CDN versions.
HTMX 4 uses explicit per-element request attributes, including `hx-sync="this:replace"`.
See [HTMX 4 changes](https://four.htmx.org/docs/whats-new-in-htmx-4/).

## Host integration

```csharp
builder.WebHost.UseStaticWebAssets(); // Referenced RCL assets when running build output.
builder.Services.AddControllersWithViews();
builder.Services.AddActiveTranslation(options =>
{
    options.DatabasePath = Path.Combine(builder.Environment.ContentRootPath, "App_Data/translations.db");
    options.CacheNamespace = "qwen3-1.7b-q4-prompt-v1";
});
// Register exactly one IAiTranslationProvider (examples below).

var app = builder.Build();
app.UseStaticFiles();
app.MapStaticAssets();
app.MapActiveTranslation(); // Maps the fragment GET and the SignalR hub.
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
```

Put `@addTagHelper *, mostlylucid.activetranslatetag` in **Views/_ViewImports.cshtml**.
Load the host's pinned HTMX and SignalR clients, then the RCL bridge:

```cshtml
<script src="~/lib/htmx/htmx.min.js" defer></script>
<script src="~/lib/signalr/signalr.min.js" defer></script>
<script src="~/_content/mostlylucid.activetranslatetag/js/active-translation.js"
        data-active-translation-hub="@Url.Content("~/_active-translate/hub")" defer></script>
```

The fragment/hub use short-lived Data Protection capabilities for content already rendered by Razor.
Keep authentication and forum access checks on the original page. Capabilities are bearer links:
do not share them beyond the audience allowed to see that content. Persist Data Protection keys
if capabilities must survive application restarts; the SQLite translations themselves already do.

## Tiny generative LLMs

The adapters intentionally target **Qwen3's non-thinking template**, rather than silently applying
one chat format to every model family. Translation quality, throughput and memory use must be
measured on the actual languages, hardware and quantization.

| Candidate | Role | License / integration |
| --- | --- | --- |
| [Qwen3 0.6B](https://huggingface.co/Qwen/Qwen3-0.6B) | Smallest baseline for short messages | Apache 2.0; Qwen3 GGUF or compatible GenAI ONNX export |
| [Qwen3.5 0.8B](https://huggingface.co/Qwen/Qwen3.5-0.8B) | Newer tiny candidate | Apache 2.0; use Ollama while in-process architecture/template support is evaluated |
| [Qwen3 1.7B](https://huggingface.co/Qwen/Qwen3-1.7B) | Next candidate to compare with 0.6B | Apache 2.0; same adapter |
| [TranslateGemma 4B](https://huggingface.co/google/translategemma-4b-it) | Translation-tuned, larger comparison model | Gemma terms; needs its own input/template adapter, not implemented |

### Ollama: easiest real-model setup

```sh
ollama pull qwen3:1.7b
dotnet run --project src/mostlylucid.activetranslatetag.Demo --   --Translation:Provider=Ollama --Translation:CacheNamespace=qwen3-1.7b-ollama-v1
```

The active Ollama provider uses `/api/chat`, `think: false`, bounded output and explicit errors.
It does not cache a failed HTTP request as a successful source-language translation.

### LLamaSharp: inference inside .NET

The optional `mostlylucid.activetranslatetag.LLamaSharp` project loads Qwen3 GGUF weights once,
serializes inference and creates a fresh context per message. The demo installs the CPU backend;
choose the matching LLamaSharp native backend for CUDA/Vulkan/Metal deployments.

```sh
dotnet run --project src/mostlylucid.activetranslatetag.Demo --   --Translation:Provider=LLamaSharp   --Translation:LLamaSharp:ModelPath=/absolute/path/Qwen3-1.7B-Q4_K_M.gguf   --Translation:CacheNamespace=qwen3-1.7b-q4km-v1
```

Model files are local configuration, never committed or automatically downloaded.
See [LLamaSharp](https://github.com/SciSharp/LLamaSharp).

### ONNX Runtime GenAI: optional adapter

`mostlylucid.activetranslatetag.Onnx` uses the generative C# API, not a conventional translation model.
Supply a **Qwen3 GenAI-compatible model directory**, including `genai_config.json`, matching tokenizer
and weights. An arbitrary `.onnx` export is insufficient. Verify the export's architecture, native
runtime/platform support, context size and quantization before selecting this provider.

```sh
dotnet run --project src/mostlylucid.activetranslatetag.Demo --   --Translation:Provider=Onnx   --Translation:Onnx:ModelDirectory=/absolute/path/qwen3-genai   --Translation:CacheNamespace=qwen3-onnx-export-v1
```

The ONNX GenAI API is marked preview upstream. The adapter is optional; its model compatibility
is not established by a successful build. See [C# API](https://onnxruntime.ai/docs/genai/api/csharp.html).

A reproducible structural smoke check against models already installed in Ollama is available:

```sh
python3 scripts/evaluate-ollama.py --models qwen3:1.7b qwen3.5:0.8b
```

This translates text fragments while keeping HTML boundaries on the server, matching the active
pipeline. `--mode markers` exercises the less reliable alternative of asking a tiny model to preserve
HTML markers itself. Structural checks do not measure translation quality.

## SQLite as a fast KV store

The active path does not use Entity Framework. It uses parameterized primary-key SQL against a
`WITHOUT ROWID` table, WAL mode and pooled connections. A bounded process-local memory cache sits
in front of completed values. This is a memory cache, not a custom LFU implementation. Set
`MemoryCacheEntries = 0` for SQLite-only operation.

The key includes markup, source/target language, context and `CacheNamespace`. Editing a post or
changing the configured model/prompt namespace naturally creates a different key. SQLite keeps
completed translations across restarts; queued work is process-local and requeued when content
is viewed again. Stored source/results have no automatic retention policy in this tutorial.

The model only sees individual text fragments and user placeholders such as `{name}`. HTML boundary
markers stay on the server; tiny models are not asked to reproduce them. This preserves markup but can
lose grammatical context across inline tags. Translated output is HTML-encoded before original
server-rendered tags are restored. Missing/reordered HTML markers or changed `{name}` placeholders
fail the job, retaining the original content. The wrapper's language and `aria-busy` update on swap.
Do not wrap scripts, styles or another active translation tag. Treat user HTML with your forum's
normal sanitization rules before marking it as trusted Razor HTML.

One worker and one deduplication map are scoped to a single app process. Multiple app instances
need shared job coordination and a SignalR backplane. Keep the example single-instance until that
is needed. Cache lookup speed does not imply model latency or translation quality.

## License

Unlicense. [mostlylucid.net](https://www.mostlylucid.net)
