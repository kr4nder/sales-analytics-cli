using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Abstractions;

/// <summary>
/// Выводит готовый отчёт в целевое хранилище: консоль, файл или произвольный поток.
/// </summary>
public interface IResultWriter
{
    /// <summary>
    /// Синхронно записывает отчёт.
    /// </summary>
    /// <param name="report">Готовый отчёт.</param>
    /// <exception cref="Exceptions.OutputPathException">Не удалось записать результат.</exception>
    void Write(AnalyticsReport report);

    /// <summary>
    /// Асинхронно записывает отчёт, не блокируя поток на вводе-выводе.
    /// </summary>
    /// <param name="report">Готовый отчёт.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="Exceptions.OutputPathException">Не удалось записать результат.</exception>
    Task WriteAsync(AnalyticsReport report, CancellationToken cancellationToken = default);
}

/// <summary>
/// Создаёт подходящий <see cref="IResultWriter"/> по параметрам запуска.
/// Нужен потому, что цель записи известна только во время выполнения,
/// а не в момент регистрации сервисов в контейнере.
/// </summary>
public interface IResultWriterFactory
{
    /// <summary>
    /// Создаёт писателя результата.
    /// </summary>
    /// <param name="outputPath">
    /// Путь к JSON-файлу или <see langword="null"/>, если результат нужно вывести в консоль.
    /// </param>
    /// <exception cref="Exceptions.OutputPathException">Каталог назначения отсутствует или недоступен.</exception>
    IResultWriter Create(string? outputPath);
}
