using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;

namespace SalesAnalytics.Core.Output;

/// <summary>
/// Выбирает способ вывода: без <c>--output</c> отчёт печатается в консоль,
/// с <c>--output</c> — сохраняется в JSON-файл.
/// </summary>
/// <param name="consoleOutput">Приёмник консольного вывода.</param>
/// <param name="consoleOptions">Настройки консольного отчёта.</param>
public sealed class ResultWriterFactory(TextWriter consoleOutput, ConsoleReportOptions? consoleOptions = null)
    : IResultWriterFactory
{
    private readonly TextWriter _consoleOutput = consoleOutput ?? throw new ArgumentNullException(nameof(consoleOutput));

    /// <inheritdoc />
    public IResultWriter Create(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return new ConsoleResultWriter(_consoleOutput, consoleOptions);
        }

        var fullPath = ResolveAndValidate(outputPath);

        // Поток открывается лениво, уже в момент записи: это позволяет
        // сообщить об ошибке ввода-вывода там же, где обрабатываются остальные ошибки.
        return new JsonResultWriter(() => OpenForWriting(fullPath));
    }

    /// <summary>
    /// Проверяет, что каталог назначения существует и доступен для записи.
    /// Лучше сообщить об этом до расчёта, чем после него.
    /// </summary>
    private static string ResolveAndValidate(string outputPath)
    {
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new OutputPathException($"Некорректный путь для сохранения результата: «{outputPath}».", ex);
        }

        if (Directory.Exists(fullPath))
        {
            throw new OutputPathException(
                $"Путь «{outputPath}» указывает на каталог. Укажите в --output имя файла, например result.json.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            throw new OutputPathException(
                $"Каталог «{directory}» не существует. Создайте его или укажите другой путь в --output.");
        }

        return fullPath;
    }

    private static Stream OpenForWriting(string fullPath)
    {
        try
        {
            return new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OutputPathException($"Не удалось открыть файл «{fullPath}» для записи: {ex.Message}", ex);
        }
    }
}
