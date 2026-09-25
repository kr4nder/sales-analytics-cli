using System.Globalization;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Cli.Configuration;

/// <summary>
/// Разбирает аргументы командной строки.
/// Поддерживаются обе записи — <c>--mode=console</c> и <c>--mode console</c>,
/// а также короткие псевдонимы <c>-m</c>, <c>-i</c>, <c>-o</c>.
/// </summary>
public sealed class CommandLineParser
{
    /// <summary>Форматы дат, принимаемые в <c>--start_date</c> и <c>--end_date</c>.</summary>
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "M/d/yyyy", "MM/dd/yyyy"];

    /// <summary>
    /// Разбирает аргументы.
    /// </summary>
    /// <param name="args">Аргументы, полученные приложением.</param>
    public CommandLineParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args.Any(IsHelpFlag))
        {
            return new CommandLineParseResult.HelpRequested();
        }

        string? mode = null;
        string? input = null;
        string? output = null;
        string? startDate = null;
        string? endDate = null;
        string? maxRows = null;

        for (var index = 0; index < args.Length; index++)
        {
            var (name, inlineValue) = SplitArgument(args[index]);

            if (name is null)
            {
                return Fail($"Не распознан аргумент «{args[index]}». Ожидался параметр вида --имя=значение.");
            }

            // Значение либо записано через «=», либо идёт следующим аргументом.
            var value = inlineValue ?? PeekValue(args, ref index);

            switch (name)
            {
                case "mode" or "m":
                    mode = value;
                    break;
                case "input" or "i":
                    input = value;
                    break;
                case "output" or "o":
                    output = value;
                    break;
                case "start_date" or "start-date":
                    startDate = value;
                    break;
                case "end_date" or "end-date":
                    endDate = value;
                    break;
                case "max_rows" or "max-rows":
                    maxRows = value;
                    break;
                default:
                    return Fail($"Неизвестный аргумент «--{name}». Полный список параметров: --help.");
            }

            if (value is null)
            {
                return Fail($"Для аргумента «--{name}» не указано значение.");
            }
        }

        return Build(mode, input, output, startDate, endDate, maxRows);
    }

    /// <summary>
    /// Проверяет значения и собирает итоговые параметры запуска.
    /// </summary>
    private static CommandLineParseResult Build(
        string? mode,
        string? input,
        string? output,
        string? startDate,
        string? endDate,
        string? maxRows)
    {
        // Режим по умолчанию — самый простой из перечисленных в задании.
        var executionMode = ExecutionMode.Console;
        if (mode is not null && !ExecutionModes.TryParse(mode, out executionMode))
        {
            return Fail($"Неизвестный режим «{mode}». Доступные режимы: {string.Join(", ", ExecutionModes.Names)}.");
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return Fail("Не указан обязательный аргумент --input с путём к CSV-файлу.");
        }

        if (executionMode == ExecutionMode.File && string.IsNullOrWhiteSpace(output))
        {
            return Fail("Режим «file» сохраняет результат в файл, поэтому требуется аргумент --output.");
        }

        if (!TryParseBoundary(startDate, "--start_date", out var start, out var startError))
        {
            return Fail(startError!);
        }

        if (!TryParseBoundary(endDate, "--end_date", out var end, out var endError))
        {
            return Fail(endError!);
        }

        if (start is not null && end is not null && start >= end)
        {
            return Fail(
                $"Дата начала ({start:yyyy-MM-dd}) должна быть строго раньше даты окончания ({end:yyyy-MM-dd}): " +
                "верхняя граница периода не включается.");
        }

        var rowLimit = 24;
        if (maxRows is not null &&
            (!int.TryParse(maxRows, NumberStyles.Integer, CultureInfo.InvariantCulture, out rowLimit) || rowLimit < 0))
        {
            return Fail($"Значение --max-rows должно быть неотрицательным целым числом, получено «{maxRows}».");
        }

        return new CommandLineParseResult.Success(new CommandLineOptions
        {
            Mode = executionMode,
            InputPath = input,
            OutputPath = string.IsNullOrWhiteSpace(output) ? null : output,
            Period = new AnalyticsPeriod(start, end),
            MaxRowsPerBlock = rowLimit,
        });
    }

    /// <summary>
    /// Разделяет аргумент на имя и записанное через «=» значение.
    /// Возвращает <c>null</c> в качестве имени, если аргумент не похож на параметр.
    /// </summary>
    private static (string? Name, string? Value) SplitArgument(string argument)
    {
        var trimmed = argument.TrimStart('-');
        if (trimmed.Length == argument.Length || trimmed.Length == 0)
        {
            return (null, null);
        }

        var separator = trimmed.IndexOf('=');
        return separator < 0
            ? (trimmed.ToLowerInvariant(), null)
            : (trimmed[..separator].ToLowerInvariant(), trimmed[(separator + 1)..]);
    }

    /// <summary>
    /// Забирает значение из следующего аргумента, если он не является очередным параметром.
    /// Индекс сдвигается, чтобы значение не было разобрано повторно.
    /// </summary>
    private static string? PeekValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            return null;
        }

        index++;
        return args[index];
    }

    private static bool TryParseBoundary(string? raw, string argumentName, out DateOnly? value, out string? error)
    {
        value = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateOnly.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = $"Аргумент {argumentName} содержит «{raw}», ожидалась дата в формате {string.Join(" / ", DateFormats)}.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool IsHelpFlag(string argument) =>
        argument is "--help" or "-h" or "-?" or "/?" or "help";

    private static CommandLineParseResult.Failure Fail(string message) => new(message);
}
