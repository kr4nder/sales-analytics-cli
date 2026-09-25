namespace SalesAnalytics.Tests.Fakes;

/// <summary>
/// Обёртка над <see cref="TextReader"/>, которая запоминает, каким способом читались строки.
/// Нужна, чтобы проверить, что асинхронный путь действительно вызывает
/// <see cref="TextReader.ReadLineAsync(CancellationToken)"/>, а не синхронный аналог под обёрткой Task.
/// </summary>
/// <param name="inner">Исходный источник строк.</param>
public sealed class TrackingTextReader(TextReader inner) : TextReader
{
    /// <summary>Сколько раз строка была прочитана синхронно.</summary>
    public int SyncReadCount { get; private set; }

    /// <summary>Сколько раз строка была прочитана асинхронно.</summary>
    public int AsyncReadCount { get; private set; }

    /// <inheritdoc />
    public override string? ReadLine()
    {
        SyncReadCount++;
        return inner.ReadLine();
    }

    /// <inheritdoc />
    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        AsyncReadCount++;
        return inner.ReadLineAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
