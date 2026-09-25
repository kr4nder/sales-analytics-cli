using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Abstractions;

/// <summary>
/// Загружает продажи из файла по указанному пути.
/// Отделён от <see cref="ICsvReader"/>, потому что отвечает за другое:
/// проверку доступности файла и открытие потока, а не за разбор содержимого.
/// </summary>
public interface ISaleLoader
{
    /// <summary>
    /// Синхронно загружает продажи из файла.
    /// </summary>
    /// <param name="path">Путь к CSV-файлу.</param>
    /// <exception cref="Exceptions.InputFileException">Файл не найден или недоступен.</exception>
    /// <exception cref="Exceptions.EmptyInputException">В файле нет ни одной записи.</exception>
    /// <exception cref="Exceptions.CsvFormatException">Файл содержит строку некорректного формата.</exception>
    IReadOnlyList<Sale> Load(string path);

    /// <summary>
    /// Асинхронно загружает продажи из файла, не блокируя поток на вводе-выводе.
    /// </summary>
    /// <param name="path">Путь к CSV-файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="Exceptions.InputFileException">Файл не найден или недоступен.</exception>
    /// <exception cref="Exceptions.EmptyInputException">В файле нет ни одной записи.</exception>
    /// <exception cref="Exceptions.CsvFormatException">Файл содержит строку некорректного формата.</exception>
    Task<IReadOnlyList<Sale>> LoadAsync(string path, CancellationToken cancellationToken = default);
}
