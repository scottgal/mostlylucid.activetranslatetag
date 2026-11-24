using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using mostlylucid.activetranslatetag.Services;
using mostlylucid.activetranslatetag.Services.Providers;

namespace mostlylucid.activetranslatetag.LLamaSharp;

public sealed class LlamaSharpOptions
{
    public string ModelPath { get; set; } = "";
    public uint ContextSize { get; set; } = 4096;
    public int GpuLayerCount { get; set; }
    public int MaxTokens { get; set; } = 1024;
}

/// <summary>Singleton model weights, serialized inference, fresh context for every message. Qwen3 GGUF only.</summary>
public sealed class LlamaSharpTranslationProvider(LlamaSharpOptions options) : IAiTranslationProvider, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private LLamaWeights? _weights;

    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var parameters = new ModelParams(options.ModelPath) { ContextSize = options.ContextSize, GpuLayerCount = options.GpuLayerCount };
            _weights ??= await LLamaWeights.LoadFromFileAsync(parameters, ct);
            var executor = new StatelessExecutor(_weights, parameters);
            var prompt = TranslationPrompt.Qwen3(TranslationPrompt.User(text, targetLanguage, sourceLanguage, description));
            if (_weights.Tokenize(prompt, true, true, Encoding.UTF8).Length + options.MaxTokens > options.ContextSize)
                throw new InvalidOperationException("Source text exceeds the configured model context budget.");
            var inference = new InferenceParams {
                MaxTokens = options.MaxTokens,
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.2f },
                AntiPrompts = ["<|im_end|>", "<|im_start|>"]
            };
            var output = new StringBuilder();
            var chunks = 0;
            await foreach (var chunk in executor.InferAsync(prompt, inference, ct)) { output.Append(chunk); chunks++; }
            if (_weights.Tokenize(output.ToString(), false, true, Encoding.UTF8).Length >= options.MaxTokens)
                throw new InvalidOperationException("Translation exceeded the output token budget.");
            if (chunks == 0) throw new InvalidOperationException("The local model produced no output.");
            return TranslationPrompt.ValidateOutput(output.ToString());
        }
        finally { _gate.Release(); }
    }

    public async Task<Dictionary<string, string>> TranslateBatchAsync(Dictionary<string, string> items, string targetLanguage,
        string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>();
        foreach (var (key, text) in items) result[key] = await TranslateAsync(text, targetLanguage, sourceLanguage, description, ct);
        return result;
    }

    public void Dispose() { _weights?.Dispose(); _gate.Dispose(); }
}
