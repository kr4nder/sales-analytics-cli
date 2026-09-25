using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Cli.Configuration;

/// <summary>
/// Разобранные и проверенные аргументы командной строки.
/// </summary>
public sealed record CommandLineOptions
{
    /// <summary>Режим работы приложения.</summary>
    public required ExecutionMode Mode { get; init; }

    /// <summary>Путь к входному CSV-файлу.</summary>
    public required string InputPath { get; init; }

    /// <summary>Путь к JSON-файлу результата или <see langword="null"/> для вывода в консоль.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Период, за который считается статистика.</summary>
    public AnalyticsPeriod Period { get; init; } = AnalyticsPeriod.All;

    /// <summary>Максимум строк в одном блоке консольного отчёта; <c>0</c> — без ограничения.</summary>
    public int MaxRowsPerBlock { get; init; } = 24;
}

/// <summary>
/// Результат разбора аргументов: успех, запрос справки или ошибка.
/// Ошибка возвращается значением, а не исключением, потому что неверный ввод
/// пользователя — ожидаемый сценарий, а не исключительная ситуация.
/// </summary>
public abstract record CommandLineParseResult
{
    private CommandLineParseResult()
    {
    }

    /// <summary>Аргументы разобраны успешно.</summary>
    /// <param name="Options">Параметры запуска.</param>
    public sealed record Success(CommandLineOptions Options) : CommandLineParseResult;

    /// <summary>Пользователь запросил справку через <c>--help</c>.</summary>
    public sealed record HelpRequested : CommandLineParseResult;

    /// <summary>Аргументы некорректны.</summary>
    /// <param name="Message">Понятное пользователю описание проблемы.</param>
    public sealed record Failure(string Message) : CommandLineParseResult;
}
