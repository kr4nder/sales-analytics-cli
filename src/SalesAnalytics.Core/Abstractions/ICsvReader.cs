using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Abstractions;

/// <summary>
/// Построчно читает CSV из произвольного <see cref="TextReader"/> и возвращает продажи.
/// Работа с <see cref="TextReader"/>, а не с путём к файлу, позволяет покрывать чтение
/// тестами через <see cref="StringReader"/>, не создавая файлов на диске.
/// </summary>
public interface ICsvReader
{
    /// <summary>
    /// Синхронно читает все строки и разбирает их в продажи.
    /// </summary>
    /// <param name="reader">Источник строк CSV.</param>
    /// <returns>Список разобранных продаж в порядке следования в файле.</returns>
    /// <exception cref="Exceptions.CsvFormatException">Встретилась строка некорректного формата.</exception>
    IReadOnlyList<Sale> Read(TextReader reader);

    /// <summary>
    /// Асинхронно читает все строки и разбирает их в продажи.
    /// Ввод-вывод выполняется без блокировки потока.
    /// </summary>
    /// <param name="reader">Источник строк CSV.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список разобранных продаж в порядке следования в файле.</returns>
    /// <exception cref="Exceptions.CsvFormatException">Встретилась строка некорректного формата.</exception>
    Task<IReadOnlyList<Sale>> ReadAsync(TextReader reader, CancellationToken cancellationToken = default);
}
