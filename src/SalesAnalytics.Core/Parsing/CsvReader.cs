using SalesAnalytics.Core.Abstractions;
using SalesAnalytics.Core.Exceptions;
using SalesAnalytics.Core.Models;

namespace SalesAnalytics.Core.Parsing;

/// <summary>
/// Построчное чтение CSV поверх <see cref="TextReader"/>.
/// Сам разбор строки делегируется <see cref="ISaleParser"/> — класс отвечает только
/// за обход строк, пропуск заголовка и пустых строк и за нумерацию строк для сообщений об ошибках.
/// </summary>
/// <param name="parser">Разборщик отдельной строки.</param>
/// <param name="options">Настройки чтения.</param>
public sealed class CsvReader(ISaleParser parser, CsvReaderOptions? options = null) : ICsvReader
{
    private readonly ISaleParser _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly CsvReaderOptions _options = options ?? new CsvReaderOptions();

    /// <inheritdoc />
    public IReadOnlyList<Sale> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var state = new ReadState(_options.Delimiter);
        var sales = new List<Sale>();

        while (reader.ReadLine() is { } line)
        {
            ProcessLine(line, state, sales);
        }

        return sales;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Sale>> ReadAsync(TextReader reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var state = new ReadState(_options.Delimiter);
        var sales = new List<Sale>();

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            ProcessLine(line, state, sales);
        }

        return sales;
    }

    /// <summary>
    /// Обрабатывает одну физическую строку файла. Логика общая для синхронного
    /// и асинхронного чтения, чтобы поведение обоих режимов не разъезжалось.
    /// </summary>
    private void ProcessLine(string line, ReadState state, List<Sale> sales)
    {
        state.LineNumber++;

        // Пустые строки внутри файла и завершающий перевод строки — не ошибка.
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        // Разделитель определяется один раз по первой значимой строке файла.
        if (!state.DelimiterResolved)
        {
            state.Delimiter = _options.Delimiter ?? CsvFormat.DetectDelimiter(line);
            state.DelimiterResolved = true;
        }

        // Заголовок ожидается только в начале файла; ниже такая строка — уже ошибка данных.
        if (!state.HeaderChecked)
        {
            state.HeaderChecked = true;
            if (CsvFormat.IsHeaderLine(line, state.Delimiter))
            {
                return;
            }
        }

        if (_parser.TryParse(line, state.LineNumber, state.Delimiter, out var sale, out var error))
        {
            sales.Add(sale!);
            return;
        }

        if (!_options.SkipInvalidRows)
        {
            throw new CsvFormatException(state.LineNumber, error!);
        }

        state.SkippedRows++;
    }

    /// <summary>
    /// Изменяемое состояние одного прохода по файлу.
    /// </summary>
    private sealed class ReadState(char? configuredDelimiter)
    {
        public int LineNumber { get; set; }

        public int SkippedRows { get; set; }

        public bool HeaderChecked { get; set; }

        public bool DelimiterResolved { get; set; } = configuredDelimiter is not null;

        public char Delimiter { get; set; } = configuredDelimiter ?? CsvFormat.DefaultDelimiter;
    }
}

/// <summary>
/// Настройки чтения CSV.
/// </summary>
public sealed record CsvReaderOptions
{
    /// <summary>
    /// Разделитель колонок. <see langword="null"/> — определить автоматически по первой строке файла.
    /// </summary>
    public char? Delimiter { get; init; }

    /// <summary>
    /// Пропускать ли строки, которые не удалось разобрать.
    /// По умолчанию <see langword="false"/>: некорректные данные считаются ошибкой,
    /// о которой пользователь должен узнать, а не молча потерять часть выборки.
    /// </summary>
    public bool SkipInvalidRows { get; init; }
}
