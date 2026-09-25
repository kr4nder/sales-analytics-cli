using SalesAnalytics.Cli.Composition;
using SalesAnalytics.Cli.Configuration;
using SalesAnalytics.Core.Application;
using SalesAnalytics.Core.Exceptions;

namespace SalesAnalytics.Cli;

/// <summary>
/// Сценарий работы приложения целиком: разбор аргументов, запуск конвейера
/// и превращение ошибок в понятные сообщения с кодом возврата.
/// Отделён от <c>Program</c> и принимает потоки вывода параметрами,
/// чтобы поведение можно было проверять тестами без запуска процесса.
/// </summary>
/// <param name="output">Поток обычного вывода.</param>
/// <param name="error">Поток сообщений об ошибках.</param>
public sealed class Application(TextWriter output, TextWriter error)
{
    /// <summary>Код возврата при успешном завершении.</summary>
    public const int ExitSuccess = 0;

    /// <summary>Код возврата при любой обработанной ошибке.</summary>
    public const int ExitFailure = 1;

    private readonly TextWriter _output = output ?? throw new ArgumentNullException(nameof(output));
    private readonly TextWriter _error = error ?? throw new ArgumentNullException(nameof(error));
    private readonly CommandLineParser _parser = new();

    /// <summary>
    /// Выполняет приложение с заданными аргументами.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="cancellationToken">Токен отмены (нажатие Ctrl+C).</param>
    /// <returns><see cref="ExitSuccess"/> или <see cref="ExitFailure"/>.</returns>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        switch (_parser.Parse(args))
        {
            case CommandLineParseResult.HelpRequested:
                await _output.WriteLineAsync(HelpText.Value).ConfigureAwait(false);
                return ExitSuccess;

            case CommandLineParseResult.Failure failure:
                await WriteErrorAsync(failure.Message).ConfigureAwait(false);
                await _error.WriteLineAsync("Подсказка: полный список параметров доступен по --help.").ConfigureAwait(false);
                return ExitFailure;

            case CommandLineParseResult.Success success:
                return await ExecuteAsync(success.Options, cancellationToken).ConfigureAwait(false);

            default:
                await WriteErrorAsync("Не удалось разобрать аргументы командной строки.").ConfigureAwait(false);
                return ExitFailure;
        }
    }

    /// <summary>
    /// Запускает конвейер и переводит исключения в коды возврата.
    /// </summary>
    private async Task<int> ExecuteAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        try
        {
            using var composed = PipelineComposer.Create(options, _output);

            var request = new PipelineRequest
            {
                InputPath = options.InputPath,
                OutputPath = options.OutputPath,
                Mode = options.Mode.ToArgumentValue(),
                Period = options.Period,
                UseAsyncIo = options.Mode.UsesAsyncIo(),
            };

            await composed.Pipeline.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            if (options.OutputPath is not null)
            {
                await _output.WriteLineAsync($"Результат сохранён в «{options.OutputPath}».").ConfigureAwait(false);
            }

            return ExitSuccess;
        }
        catch (SalesAnalyticsException ex)
        {
            // Ожидаемые проблемы: нет файла, битые данные, недоступный каталог.
            await WriteErrorAsync(ex.Message).ConfigureAwait(false);
            return ExitFailure;
        }
        catch (OperationCanceledException)
        {
            await WriteErrorAsync("Выполнение прервано пользователем.").ConfigureAwait(false);
            return ExitFailure;
        }
        catch (Exception ex)
        {
            // Непредвиденная ошибка: тип исключения помогает при разборе,
            // но стек вызовов пользователю в консоль не выводится.
            await WriteErrorAsync($"Непредвиденная ошибка ({ex.GetType().Name}): {ex.Message}").ConfigureAwait(false);
            return ExitFailure;
        }
    }

    private async Task WriteErrorAsync(string message)
    {
        await _error.WriteLineAsync($"Ошибка: {message}").ConfigureAwait(false);
        await _error.FlushAsync().ConfigureAwait(false);
    }
}
