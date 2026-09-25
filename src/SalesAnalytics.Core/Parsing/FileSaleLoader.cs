using System.Text;
using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Parsing;

/// <summary>
/// Загрузка продаж из файла на диске: проверяет доступность файла,
/// открывает поток и передаёт его в <see cref="ICsvReader"/>.
/// </summary>
/// <param name="csvReader">Читатель CSV.</param>
public sealed class FileSaleLoader(ICsvReader csvReader) : ISaleLoader
{
    /// <summary>Размер буфера потока: компромисс между числом системных вызовов и памятью.</summary>
    private const int BufferSize = 64 * 1024;

    private readonly ICsvReader _csvReader = csvReader ?? throw new ArgumentNullException(nameof(csvReader));

    /// <inheritdoc />
    public IReadOnlyList<Sale> Load(string path)
    {
        EnsureFileExists(path);

        try
        {
            using var reader = CreateReader(path, useAsyncIo: false);
            return EnsureNotEmpty(_csvReader.Read(reader), path);
        }
        catch (IOException ex)
        {
            throw new InputFileException($"Не удалось прочитать файл «{path}»: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InputFileException($"Нет прав на чтение файла «{path}».", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Sale>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureFileExists(path);

        try
        {
            using var reader = CreateReader(path, useAsyncIo: true);
            var sales = await _csvReader.ReadAsync(reader, cancellationToken).ConfigureAwait(false);
            return EnsureNotEmpty(sales, path);
        }
        catch (IOException ex)
        {
            throw new InputFileException($"Не удалось прочитать файл «{path}»: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InputFileException($"Нет прав на чтение файла «{path}».", ex);
        }
    }

    /// <summary>
    /// Открывает файл на чтение. Для асинхронного режима поток создаётся с
    /// <see cref="FileOptions.Asynchronous"/>, иначе «асинхронное» чтение
    /// свелось бы к блокирующим вызовам под обёрткой Task.
    /// </summary>
    private static StreamReader CreateReader(string path, bool useAsyncIo)
    {
        var fileOptions = FileOptions.SequentialScan | (useAsyncIo ? FileOptions.Asynchronous : FileOptions.None);

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            fileOptions);

        // detectEncodingFromByteOrderMarks: файл может быть сохранён с BOM.
        return new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, BufferSize);
    }

    private static void EnsureFileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InputFileException("Путь к входному файлу не задан. Укажите аргумент --input.");
        }

        if (!File.Exists(path))
        {
            throw InputFileException.NotFound(path);
        }
    }

    private static IReadOnlyList<Sale> EnsureNotEmpty(IReadOnlyList<Sale> sales, string path) =>
        sales.Count > 0 ? sales : throw EmptyInputException.ForFile(path);
}
