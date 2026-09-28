using System.Globalization;
using System.Text;
using System.Text.Json;
using FinanceFoodTracker.Application.Statements.Analysis;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class OpenCodeGoStatementAnalyzer : IStatementAnalyzer
{
    private const int MaxChunkChars = 8000;
    private const int MaxParallel = 3;

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
        var chunks = Chunk(request.Text, MaxChunkChars);
        var systemPrompt = BuildSystemPrompt(request);
        var results = new StatementPayload?[chunks.Count];
        var throttle = new SemaphoreSlim(MaxParallel);

        var tasks = chunks.Select(async (chunk, index) =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                var content = await _client.CompleteJsonAsync(
                    systemPrompt,
                    $"Разбери банковскую выписку (часть {index + 1} из {chunks.Count}) и верни JSON по схеме.\n\nТекст выписки:\n{chunk}",
                    null,
                    $"{request.SessionId}-{index}",
                    cancellationToken);

                results[index] = ParsePayload(content);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);

        var operations = new List<StatementOperationResult>();
        string? currency = null;
        decimal? opening = null;
        decimal? closing = null;
        string? rawResponse = null;

        foreach (var payload in results)
        {
            if (payload is null)
            {
                continue;
            }

            currency ??= payload.Currency;
            opening ??= payload.OpeningBalance;
            closing ??= payload.ClosingBalance;

            if (payload.Operations is not null)
            {
                operations.AddRange(payload.Operations.Select(Map).Where(item => item is not null).Select(item => item!));
            }

            rawResponse = JsonSerializer.Serialize(results);
        }

        return new StatementAnalysisResult(currency, opening, closing, operations, rawResponse);
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
            - Беларусбанк: "Приход" → income, "Расход" → expense.
            - Приорбанк: есть колонки "Списание" и "Поступление". Если "Поступление" > 0 → income,
              иначе expense; сумма — ненулевое из "Списание"/"Поступление". Знак также в "(+)"/"(-)".
            - Сумма всегда положительная, в валюте счёта.
            - Дата — дата совершения операции. Время — если есть.
            - is_transfer = true для переводов между своими счетами: "Перевод P2P", "PERSON TO PERSON",
              "снятие наличных"/банкомат/ATM, "пополнение счета", "с транзитного счёта",
              "перевод между счетами", "зачисление по реестру карт-центра"/"P2P". Покупки/зарплата — false.
            - category: строго из списка расходов для expense: {{expense}}; из списка доходов для income: {{income}}.
              Если не подходит — null. MCC: 5411 продукты, 5541 АЗС, 4900 коммунальные/связь, 6012 финансовые переводы.
              Платёж через ЕРИП (в описании "ERIP") — категория "ЕРИП".
            - "description" — кратко (магазин/назначение, например "UNIVERSAM", "MOBILE BANK", "ERIP"), без повторов place.
            - Строки-заголовки и итоги не включай.
            - Не включай поля со значением null (кроме обязательных date, amount, direction).
            - Отвечай компактным JSON без лишних пробелов и переносов.
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
            return new DateTimeOffset(parsed, TimeSpan.FromHours(3)).ToUniversalTime();
        }

        return DateTimeOffset.UtcNow;
    }

    private static List<string> Chunk(string text, int size)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            if (builder.Length + line.Length + 1 > size && builder.Length > 0)
            {
                chunks.Add(builder.ToString());
                builder.Clear();
            }

            builder.AppendLine(line);
        }

        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString());
        }

        if (chunks.Count == 0)
        {
            chunks.Add(text);
        }

        return chunks;
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
