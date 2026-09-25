namespace SalesAnalytics.Cli.Configuration;

/// <summary>
/// Режим работы приложения, задаётся аргументом <c>--mode</c>.
/// </summary>
public enum ExecutionMode
{
    /// <summary>Синхронное чтение CSV, расчёт и вывод в консоль.</summary>
    Console,

    /// <summary>То же, что <see cref="Console"/>, но результат сохраняется в JSON-файл.</summary>
    File,

    /// <summary>Асинхронное чтение файла и асинхронная запись результата.</summary>
    Async,

    /// <summary>Параллельный расчёт аналитик.</summary>
    Parallel,

    /// <summary>Все сервисы разрешаются из DI-контейнера.</summary>
    Di,

    /// <summary>Комбинированный режим: асинхронный ввод-вывод, параллельный расчёт и DI.</summary>
    Full,
}

/// <summary>
/// Разбор и отображение значений <see cref="ExecutionMode"/>.
/// </summary>
public static class ExecutionModes
{
    /// <summary>Список названий режимов в том виде, в котором их вводит пользователь.</summary>
    public static IReadOnlyList<string> Names { get; } =
        Enum.GetValues<ExecutionMode>().Select(ToArgumentValue).ToArray();

    /// <summary>Возвращает название режима в нижнем регистре: <c>console</c>, <c>full</c> и т. д.</summary>
    public static string ToArgumentValue(this ExecutionMode mode) => mode.ToString().ToLowerInvariant();

    /// <summary>Пытается разобрать название режима без учёта регистра.</summary>
    public static bool TryParse(string? value, out ExecutionMode mode) =>
        Enum.TryParse(value, ignoreCase: true, out mode) && Enum.IsDefined(mode);

    /// <summary>Использует ли режим асинхронный ввод-вывод.</summary>
    public static bool UsesAsyncIo(this ExecutionMode mode) => mode is ExecutionMode.Async or ExecutionMode.Full;

    /// <summary>Использует ли режим параллельный расчёт.</summary>
    public static bool UsesParallelAnalytics(this ExecutionMode mode) => mode is ExecutionMode.Parallel or ExecutionMode.Full;

    /// <summary>Строится ли режим на DI-контейнере.</summary>
    public static bool UsesDependencyInjection(this ExecutionMode mode) => mode is ExecutionMode.Di or ExecutionMode.Full;
}
