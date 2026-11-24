using System.Text;
using Microsoft.ML.OnnxRuntimeGenAI;
using mostlylucid.activetranslatetag.Services;
using mostlylucid.activetranslatetag.Services.Providers;

namespace mostlylucid.activetranslatetag.Onnx;

public sealed class OnnxTranslationOptions
{
    // Must contain genai_config.json and its matching tokenizer/weights, not an arbitrary .onnx file.
    public string ModelDirectory { get; set; } = "";
    public int MaxTokens { get; set; } = 1024;
    public int ContextSize { get; set; } = 4096;
}

/// <summary>Optional Qwen3 ONNX GenAI adapter. Export/runtime compatibility must be verified for the chosen artifact.</summary>
public sealed class OnnxTranslationProvider(OnnxTranslationOptions options) : IAiTranslationProvider, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private Model? _model;
    private Tokenizer? _tokenizer;

    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = "en", string? description = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _model ??= new Model(options.ModelDirectory);
            _tokenizer ??= new Tokenizer(_model);
            var prompt = TranslationPrompt.Qwen3(TranslationPrompt.User(text, targetLanguage, sourceLanguage, description));
            using var input = _tokenizer.Encode(prompt);
            var inputLength = input[0].Length;
            if (inputLength + options.MaxTokens > options.ContextSize)
                throw new InvalidOperationException("Source text exceeds the configured model context budget.");
            using var parameters = new GeneratorParams(_model);
            parameters.SetSearchOption("max_length", inputLength + options.MaxTokens);
            parameters.SetSearchOption("do_sample", false);
            using var generator = new Generator(_model, parameters);
            using var stream = _tokenizer.CreateStream();
            generator.AppendTokenSequences(input);
            var output = new StringBuilder();
            var generated = 0;
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.GenerateNextToken();
                var sequence = generator.GetSequence(0);
                output.Append(stream.Decode(sequence[^1]));
                generated++;
            }
            if (generated >= options.MaxTokens) throw new InvalidOperationException("Translation exceeded the output token budget.");
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

    public void Dispose() { _tokenizer?.Dispose(); _model?.Dispose(); _gate.Dispose(); }
}
