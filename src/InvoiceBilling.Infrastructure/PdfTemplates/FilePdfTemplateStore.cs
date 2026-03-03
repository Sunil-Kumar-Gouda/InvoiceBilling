using Microsoft.Extensions.Options;

namespace InvoiceBilling.Infrastructure.PdfTemplates;

public sealed class FilePdfTemplateStore : IPdfTemplateStore
{
    private readonly PdfTemplatesOptions _options;
    private readonly string _contentRoot;

    public FilePdfTemplateStore(IOptions<PdfTemplatesOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        _options = options.Value;
        _contentRoot = env.ContentRootPath;
    }

    public async Task<string?> GetActiveTemplateJsonAsync(CancellationToken ct)
    {
        var path = ResolvePath();
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    public async Task SaveActiveTemplateJsonAsync(string json, CancellationToken ct)
    {
        var path = ResolvePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public Task DeleteActiveTemplateAsync(CancellationToken ct)
    {
        var path = ResolvePath();
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath()
    {
        var p = _options.StoragePath?.Trim();
        if (string.IsNullOrWhiteSpace(p)) p = "App_Data/pdf-template.active.json";

        return Path.IsPathRooted(p)
            ? p
            : Path.Combine(_contentRoot, p.Replace('/', Path.DirectorySeparatorChar));
    }
}

