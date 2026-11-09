using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using Microsoft.Playwright;
using PTL.ExternalWeb.Infrastructure.Pdf;

namespace PTL.ExternalWeb.Tests.TestSupport;

// Browser-free stand-ins for the Playwright interfaces the PDF renderer touches (CI has no Chromium).
internal sealed class FakePdfBrowser
{
    private int printCount;

    public static readonly byte[] ValidPdf = "%PDF-1.7 fake"u8.ToArray();

    public FakePdfBrowser()
    {
        Instance = FakeProxy.Create<IBrowser>((name, _) => name switch
        {
            "get_IsConnected" => Connected,
            "NewContextAsync" => NewContext(),
            "DisposeAsync" => OnDispose(),
            _ => FakeProxy.Unhandled
        });
    }

    public IBrowser Instance { get; }

    public bool Connected { get; set; } = true;

    public byte[] PdfBytes { get; set; } = ValidPdf;

    public Exception? PdfFailure { get; set; }

    public Exception? DisposeFailure { get; set; }

    public int DisposeCount { get; private set; }

    public int PrintCount => Volatile.Read(ref printCount);

    public string? LastHtml { get; private set; }

    public PagePdfOptions? LastPdfOptions { get; private set; }

    private object? OnDispose()
    {
        DisposeCount++;
        return DisposeFailure is null ? FakeProxy.Unhandled : throw DisposeFailure;
    }

    private IBrowserContext NewContext() => FakeProxy.Create<IBrowserContext>((name, _) => name switch
    {
        "NewPageAsync" => NewPage(),
        _ => FakeProxy.Unhandled
    });

    private IPage NewPage() => FakeProxy.Create<IPage>((name, args) =>
    {
        switch (name)
        {
            case "SetContentAsync":
                LastHtml = (string)args[0]!;
                return FakeProxy.Unhandled;
            case "PdfAsync":
                Interlocked.Increment(ref printCount);
                LastPdfOptions = (PagePdfOptions?)args[0];
                return PdfFailure is null ? PdfBytes : throw PdfFailure;
            default:
                return FakeProxy.Unhandled;
        }
    });
}

internal sealed class FakePdfBrowserLauncher(params FakePdfBrowser[] browsers) : IPdfBrowserLauncher
{
    private readonly Queue<FakePdfBrowser> pending = new(browsers);

    public List<string?> Channels { get; } = [];

    public bool Disposed { get; private set; }

    public TaskCompletionSource? Gate { get; init; }

    public async Task<IBrowser> LaunchAsync(string? channel)
    {
        Channels.Add(channel);
        if (Gate is not null)
        {
            await Gate.Task;
        }

        return pending.Dequeue().Instance;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

// DispatchProxy emits a subclass at runtime, so the proxy type cannot be sealed.
[SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a subclass at runtime.")]
internal class FakeProxy : DispatchProxy
{
    public static readonly object Unhandled = new();

    private Func<string, object?[], object?> handler = (_, _) => Unhandled;

    public static T Create<T>(Func<string, object?[], object?> handler)
        where T : class
    {
        var proxy = Create<T, FakeProxy>();
        ((FakeProxy)(object)proxy).handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        var result = handler(targetMethod.Name, args ?? []);
        var returnType = targetMethod.ReturnType;

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType == typeof(ValueTask))
        {
            return default(ValueTask);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var value = ReferenceEquals(result, Unhandled) ? null : result;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(returnType.GetGenericArguments()[0]).Invoke(null, [value]);
        }

        if (ReferenceEquals(result, Unhandled))
        {
            return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
        }

        return result;
    }
}
