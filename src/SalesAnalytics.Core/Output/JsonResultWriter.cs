using System.Text.Json;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Output;

/// <summary>
/// Сохраняет отчёт в JSON.
/// Поток назначения создаётся фабрикой в момент записи: это позволяет
/// подставить <see cref="MemoryStream"/> в тестах и не держать файл открытым заранее.
/// </summary>
/// <param name="streamFactory">Фабрика потока назначения.</param>
/// <param name="leaveOpen">Не закрывать поток после записи — нужно для тестов.</param>
public sealed class JsonResultWriter(Func<Stream> streamFactory, bool leaveOpen = false) : IResultWriter
{
    private readonly Func<Stream> _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));

    /// <inheritdoc />
    public void Write(AnalyticsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var stream = _streamFactory();

        try
        {
            JsonSerializer.Serialize(stream, report, ReportJson.Options);
            stream.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OutputPathException($"Не удалось сохранить результат: {ex.Message}", ex);
        }
        finally
        {
            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(AnalyticsReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var stream = _streamFactory();

        try
        {
            await JsonSerializer.SerializeAsync(stream, report, ReportJson.Options, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OutputPathException($"Не удалось сохранить результат: {ex.Message}", ex);
        }
        finally
        {
            if (!leaveOpen)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
