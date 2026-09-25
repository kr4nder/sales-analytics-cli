using SalesAnalytics.Cli.Configuration;

namespace SalesAnalytics.Tests.Cli;

/// <summary>
/// Тесты разбора аргументов командной строки.
/// </summary>
public sealed class CommandLineParserTests
{
    private readonly CommandLineParser _parser = new();

    [Theory]
    [InlineData("--mode=console", "--input=sales.csv")]
    [InlineData("--mode", "console", "--input", "sales.csv")]
    [InlineData("-m", "console", "-i", "sales.csv")]
    [InlineData("-m=console", "-i=sales.csv")]
    public void Parse_SupportedArgumentForms_AreEquivalent(params string[] args)
    {
        var options = AssertSuccess(args);

        Assert.Equal(ExecutionMode.Console, options.Mode);
        Assert.Equal("sales.csv", options.InputPath);
    }

    [Theory]
    [InlineData("console", ExecutionMode.Console)]
    [InlineData("file", ExecutionMode.File)]
    [InlineData("async", ExecutionMode.Async)]
    [InlineData("parallel", ExecutionMode.Parallel)]
    [InlineData("di", ExecutionMode.Di)]
    [InlineData("full", ExecutionMode.Full)]
    [InlineData("FULL", ExecutionMode.Full)]
    public void Parse_EveryDocumentedMode_IsRecognised(string value, ExecutionMode expected)
    {
        var args = expected == ExecutionMode.File
            ? new[] { $"--mode={value}", "--input=sales.csv", "--output=r.json" }
            : [$"--mode={value}", "--input=sales.csv"];

        Assert.Equal(expected, AssertSuccess(args).Mode);
    }

    [Fact]
    public void Parse_ModeOmitted_DefaultsToConsole() =>
        Assert.Equal(ExecutionMode.Console, AssertSuccess(["--input=sales.csv"]).Mode);

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void Parse_HelpFlag_RequestsHelp(string flag) =>
        Assert.IsType<CommandLineParseResult.HelpRequested>(_parser.Parse([flag]));

    [Fact]
    public void Parse_NoArguments_RequestsHelp() =>
        Assert.IsType<CommandLineParseResult.HelpRequested>(_parser.Parse([]));

    [Fact]
    public void Parse_MissingInput_Fails() =>
        Assert.Contains("--input", AssertFailure(["--mode=console"]));

    [Fact]
    public void Parse_UnknownMode_ListsAvailableModes()
    {
        var message = AssertFailure(["--mode=turbo", "--input=sales.csv"]);

        Assert.Contains("turbo", message);
        Assert.Contains("console, file, async, parallel, di, full", message);
    }

    [Fact]
    public void Parse_UnknownArgument_Fails() =>
        Assert.Contains("--verbose", AssertFailure(["--input=sales.csv", "--verbose"]));

    [Fact]
    public void Parse_FileModeWithoutOutput_Fails() =>
        Assert.Contains("--output", AssertFailure(["--mode=file", "--input=sales.csv"]));

    [Fact]
    public void Parse_ArgumentWithoutValue_Fails() =>
        Assert.Contains("не указано значение", AssertFailure(["--input=sales.csv", "--output"]));

    [Theory]
    [InlineData("2022-03-01")]
    [InlineData("01.03.2022")]
    [InlineData("3/1/2022")]
    public void Parse_SupportedDateFormats_AreAccepted(string date)
    {
        var options = AssertSuccess(["--input=sales.csv", $"--start_date={date}"]);

        Assert.Equal(new DateOnly(2022, 3, 1), options.Period.StartDate);
    }

    [Fact]
    public void Parse_BothBoundaries_BuildHalfOpenPeriod()
    {
        var options = AssertSuccess(["--input=sales.csv", "--start_date=2022-01-01", "--end_date=2023-01-01"]);

        Assert.Equal(new DateOnly(2022, 1, 1), options.Period.StartDate);
        Assert.Equal(new DateOnly(2023, 1, 1), options.Period.EndDate);
        Assert.True(options.Period.Contains(new DateOnly(2022, 12, 31)));
        Assert.False(options.Period.Contains(new DateOnly(2023, 1, 1)));
    }

    [Fact]
    public void Parse_InvalidDate_Fails() =>
        Assert.Contains("--start_date", AssertFailure(["--input=sales.csv", "--start_date=вчера"]));

    [Fact]
    public void Parse_StartNotBeforeEnd_Fails() =>
        Assert.Contains(
            "строго раньше",
            AssertFailure(["--input=sales.csv", "--start_date=2023-01-01", "--end_date=2023-01-01"]));

    [Fact]
    public void Parse_NoDates_UsesUnboundedPeriod()
    {
        var options = AssertSuccess(["--input=sales.csv"]);

        Assert.False(options.Period.HasBounds);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("50", 50)]
    public void Parse_MaxRows_IsAccepted(string value, int expected) =>
        Assert.Equal(expected, AssertSuccess(["--input=sales.csv", $"--max-rows={value}"]).MaxRowsPerBlock);

    [Theory]
    [InlineData("-5")]
    [InlineData("много")]
    public void Parse_InvalidMaxRows_Fails(string value) =>
        Assert.Contains("--max-rows", AssertFailure(["--input=sales.csv", $"--max-rows={value}"]));

    [Fact]
    public void Parse_OutputOmitted_LeavesOutputPathNull() =>
        Assert.Null(AssertSuccess(["--input=sales.csv"]).OutputPath);

    private CommandLineOptions AssertSuccess(string[] args) =>
        Assert.IsType<CommandLineParseResult.Success>(_parser.Parse(args)).Options;

    private string AssertFailure(string[] args) =>
        Assert.IsType<CommandLineParseResult.Failure>(_parser.Parse(args)).Message;
}
