using System.Globalization;
using System.Text.Json;
using FinanceFoodTracker.Application.Statements.Analysis;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class OpenCodeGoStatementAnalyzer : IStatementAnalyzer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly OpenCodeGoVisionClient _client;

    public OpenCodeGoStatementAnalyzer(OpenCodeGoVisionClient client)
    {
        _client = client;
    }

    public async Task<StatementAnalysisResult> AnalyzeAsync(StatementAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var content = await _client.CompleteJsonAsync(
            BuildSystemPrompt(request),
            $"Разбери банковскую выписку и верни JSON по заданной схеме.\n\nТекст выписки:\n{request.Text}",
            null,
            request.SessionId,
            cancellationToken);

        var payload = ParsePayload(content);

        if (payload?.Operations is null)
        {
            return new StatementAnalysisResult(null, null, null, Array.Empty<StatementOperationResult>(), content);
        }

        var operations = payload.Operations
            .Select(Map)
            .Where(operation => operation is not null)
            .Select(operation => operation!)
            .ToList();

        return new StatementAnalysisResult(
            payload.Currency,
            payload.OpeningBalance,
            payload.ClosingBalance,
            operations,
            content);
    }

    private static string BuildSystemPrompt(StatementAnalysisRequest request)
    {
        var expense = request.ExpenseCategories.Count > 0 ? string.Join(", ", request.ExpenseCategories) : "нет";
        var income = request.IncomeCategories.Count > 0 ? string.Join(", ", request.IncomeCategories) : "нет";

        return $$"""
            Ты — ассистент, который разбирает банковские выписки (Беларусь: Беларусбанк, Приорбанк и др.).
            На вход — текст PDF-выписки, построчно с разделителем колонок "|". Верни СТРОГО один валидный JSON
            без markdown и пояснений по схеме:
            {
              "currency": string | null,
              "opening_balance": number | null,
              "closing_balance": number | null,
              "operations": [
                {
                  "date": "YYYY-MM-DD",
                  "time": "HH:MM" | null,
                  "amount": number,
                  "direction": "income" | "expense",
                  "description": string,
                  "place": string | null,
                  "currency": string | null,
                  "mcc": string | null,
                  "is_transfer": boolean,
                  "category": string | null,
                  "confidence": number от 0 до 1
                }
              ]
            }
            Правила:
            - "Приход" → direction "income", "Расход" → direction "expense".
            - Сумма: бери значение В ВАЛЮТЕ СЧЁТА (второе число из двух, перед остатком после операции). Всегда положительная.
            - Дата — дата совершения операции (не дата отражения). Время — если есть.
            - is_transfer = true для переводов между своими счетами: "Перевод P2P", "PERSON TO PERSON",
              переводы/снятие наличных/банкомат/ATM, пополнение наличными. Для покупок и зарплаты — false.
            - category: выбери СТРОГО из списка расходов для direction "expense": {{expense}};
              для direction "income" из списка доходов: {{income}}. Если не подходит — null.
              MCC помогает: 5411 продукты, 5541 АЗС/топливо, 4900 коммунальные/связь, 6012 финансовые переводы.
            - Если строка не является операцией (заголовки, итоги) — не включай её.
            """;
    }

    private static StatementOperationResult? Map(StatementPayloadOperation operation)
    {
        var amount = Math.Abs(operation.Amount ?? 0m);
        var direction = string.Equals(operation.Direction, "income", StringComparison.OrdinalIgnoreCase)
            ? "income"
            : "expense";

        return new StatementOperationResult(
            ParseMoment(operation.Date, operation.Time),
            amount,
            direction,
            Trim(operation.Description),
            Trim(operation.Place),
            Trim(operation.Currency),
            Trim(operation.Mcc),
            operation.IsTransfer ?? false,
            Trim(operation.Category),
            operation.Confidence);
    }

    private static DateTimeOffset ParseMoment(string? date, string? time)
    {
        var datePart = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow.ToString("yyyy-MM-dd") : date.Trim();
        var timePart = string.IsNullOrWhiteSpace(time) ? "00:00" : time.Trim();

        if (DateTime.TryParse($"{datePart}T{timePart}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return new DateTimeOffset(parsed, TimeSpan.FromHours(3));
        }

        return DateTimeOffset.UtcNow;
    }

    private static StatementPayload? ParsePayload(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StatementPayload>(content[start..(end + 1)], SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
