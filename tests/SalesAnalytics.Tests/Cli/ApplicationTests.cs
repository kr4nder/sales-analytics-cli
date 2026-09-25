using SalesAnalytics.Cli;

namespace SalesAnalytics.Tests.Cli;

/// <summary>
/// Тесты сценария приложения целиком: коды возврата и сообщения пользователю.
/// Потоки вывода подменяются на <see cref="StringWriter"/>, процесс не запускается.
/// </summary>
public sealed class ApplicationTests
{
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private Application CreateApplication() => new(_output, _error);

    [Fact]
    public async Task RunAsync_HelpFlag_PrintsHelpAndSucceeds()
    {
        var exitCode = await CreateApplication().RunAsync(["--help"]);

        Assert.Equal(Application.ExitSuccess, exitCode);
        Assert.Contains("ИСПОЛЬЗОВАНИЕ", _output.ToString());
        Assert.Contains("--input", _output.ToString());
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public async Task RunAsync_NoArguments_PrintsHelpAndSucceeds()
    {
        var exitCode = await CreateApplication().RunAsync([]);

        Assert.Equal(Application.ExitSuccess, exitCode);
        Assert.Contains("РЕЖИМЫ", _output.ToString());
    }

    [Fact]
    public async Task RunAsync_Help_DocumentsEveryMode()
    {
        await CreateApplication().RunAsync(["--help"]);
        var help = _output.ToString();

        foreach (var mode in SalesAnalytics.Cli.Configuration.ExecutionModes.Names)
        {
            Assert.Contains(mode, help);
        }
    }

    [Fact]
    public async Task RunAsync_InvalidArguments_FailsWithHint()
    {
        var exitCode = await CreateApplication().RunAsync(["--mode=turbo", "--input=sales.csv"]);

        Assert.Equal(Application.ExitFailure, exitCode);
        Assert.Contains("Ошибка:", _error.ToString());
        Assert.Contains("--help", _error.ToString());
    }

    [Fact]
    public async Task RunAsync_MissingInputFile_FailsWithReadableMessage()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"нет-файла-{Guid.NewGuid():N}.csv");

        var exitCode = await CreateApplication().RunAsync(["--mode=console", $"--input={missing}"]);

        Assert.Equal(Application.ExitFailure, exitCode);
        Assert.Contains("не найден", _error.ToString());
        // Пользователю показывается сообщение, а не стек вызовов.
        Assert.DoesNotContain("   at ", _error.ToString());
    }

    [Fact]
    public async Task RunAsync_UnwritableOutputDirectory_Fails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"нет-каталога-{Guid.NewGuid():N}", "result.json");

        var exitCode = await CreateApplication().RunAsync(
            ["--mode=file", "--input=sales.csv", $"--output={path}"]);

        Assert.Equal(Application.ExitFailure, exitCode);
        Assert.Contains("не существует", _error.ToString());
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeStart_FailsWithoutCrashing()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exitCode = await CreateApplication().RunAsync(
            ["--mode=async", "--input=sales.csv"],
            cancellation.Token);

        Assert.Equal(Application.ExitFailure, exitCode);
    }

    [Fact]
    public void Constructor_NullWriters_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new Application(null!, _error));
        Assert.Throws<ArgumentNullException>(() => new Application(_output, null!));
    }
}
