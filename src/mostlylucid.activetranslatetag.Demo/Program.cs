using mostlylucid.activetranslatetag.Extensions;
using mostlylucid.activetranslatetag.Services;
using mostlylucid.activetranslatetag.Demo;

var builder = WebApplication.CreateBuilder(args);
// Serve referenced RCL assets when running build output outside Development, too.
builder.WebHost.UseStaticWebAssets();
builder.Services.AddActiveTranslation(options =>
{
    options.DatabasePath = Path.Combine(builder.Environment.ContentRootPath, "App_Data/active-translations.db");
    options.CacheNamespace = string.Join(":",
        builder.Configuration["Translation:Provider"] ?? "Sample",
        builder.Configuration["Translation:Ollama:Model"],
        builder.Configuration["Translation:LLamaSharp:ModelPath"],
        builder.Configuration["Translation:Onnx:ModelDirectory"],
        builder.Configuration["Translation:CacheNamespace"] ?? "v1");
});

// Sample mode is deterministic and needs no model. It is explicitly labelled in the UI.
// Switch Provider to Ollama, LLamaSharp, or Onnx for real generative inference.
var provider = builder.Configuration["Translation:Provider"] ?? "Sample";
switch (provider.ToLowerInvariant())
{
    case "sample": builder.Services.AddSingleton<IAiTranslationProvider, SampleTranslationProvider>(); break;
    case "ollama":
        builder.Services.AddHttpClient("translation", client => { client.Timeout = TimeSpan.FromMinutes(2); });
        builder.Services.AddScoped<IAiTranslationProvider>(sp => new mostlylucid.activetranslatetag.Services.Providers.LocalOllamaTranslationProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("translation"),
            builder.Configuration["Translation:Ollama:BaseUrl"] ?? "http://localhost:11434/",
            builder.Configuration["Translation:Ollama:Model"] ?? "qwen3:1.7b"));
        break;
    case "llamasharp":
        var llamaOptions = new mostlylucid.activetranslatetag.LLamaSharp.LlamaSharpOptions();
        builder.Configuration.GetSection("Translation:LLamaSharp").Bind(llamaOptions);
        builder.Services.AddSingleton<IAiTranslationProvider>(_ => new mostlylucid.activetranslatetag.LLamaSharp.LlamaSharpTranslationProvider(llamaOptions));
        break;
    case "onnx":
        var onnxOptions = new mostlylucid.activetranslatetag.Onnx.OnnxTranslationOptions();
        builder.Configuration.GetSection("Translation:Onnx").Bind(onnxOptions);
        builder.Services.AddSingleton<IAiTranslationProvider>(_ => new mostlylucid.activetranslatetag.Onnx.OnnxTranslationProvider(onnxOptions));
        break;
    default: throw new InvalidOperationException($"Unknown provider: {provider}");
}
var app = builder.Build();
app.UseStaticFiles();
app.MapStaticAssets();
app.MapActiveTranslation();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
