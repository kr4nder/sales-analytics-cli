using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalesAnalytics.Core.Output;

/// <summary>
/// Единые настройки сериализации отчёта в JSON.
/// Вынесены в одно место, чтобы файл и любой другой приёмник давали идентичный формат.
/// </summary>
public static class ReportJson
{
    /// <summary>
    /// Имена свойств переводятся в snake_case — так они совпадают по стилю
    /// с заголовками исходного CSV. Кириллица не экранируется, чтобы файл
    /// оставался читаемым в любом редакторе.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
