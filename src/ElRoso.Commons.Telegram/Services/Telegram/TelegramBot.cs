// <copyright file="TelegramBot.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace ElRoso.Services
{
    using ElRoso.Exceptions;
    using ElRoso.Telegram;
    using Microsoft.Extensions.Logging;
    using System.Net.Http.Json;
    using System.Text.Json;

    /// <summary>
    /// Named Telegram bot instance. Token is encapsulated — callers never handle it directly.
    /// Instancia de bot con token encapsulado — los callers nunca lo manejan directamente.
    /// </summary>
    internal sealed class TelegramBot(
        string name,
        string token,
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramBot> logger) : ITelegramBot
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public string Name { get; } = name;

        private string ApiBase => $"https://api.telegram.org/bot{token}";

        public async Task<TelegramBotInfo> GetMeAsync(CancellationToken ct = default)
        {
            var client = httpClientFactory.CreateClient(TelegramClientNames.Send);
            var response = await client.GetAsync($"{this.ApiBase}/getMe", ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TelegramApiResponse<TelegramBotInfo>>(JsonOptions, ct);

            if (result is not { Ok: true, Result: not null })
            {
                throw new InvalidOperationException($"Telegram GetMe failed for bot '{this.Name}': {result?.Description}");
            }

            logger.LogInformation("Telegram bot '{BotName}' validated: @{Username}", this.Name, result.Result.Username);
            return result.Result;
        }

        public async Task SendMessageAsync(long chatId, string message, TelegramSendOptions? options = null, CancellationToken ct = default)
            => await this.SendAsync("sendMessage", chatId, new Dictionary<string, object?> { ["text"] = message }, options, ct);

        public async Task SendDocumentAsync(long chatId, string documentUrl, string? caption = null, TelegramSendOptions? options = null, CancellationToken ct = default)
            => await this.SendAsync("sendDocument", chatId, new Dictionary<string, object?> { ["document"] = documentUrl, ["caption"] = caption }, options, ct);

        public async Task SendPhotoAsync(long chatId, string photoUrl, string? caption = null, TelegramSendOptions? options = null, CancellationToken ct = default)
            => await this.SendAsync("sendPhoto", chatId, new Dictionary<string, object?> { ["photo"] = photoUrl, ["caption"] = caption }, options, ct);

        private static Dictionary<string, object?> BuildPayload(long chatId, Dictionary<string, object?> fields, TelegramSendOptions? options)
        {
            var payload = new Dictionary<string, object?> { ["chat_id"] = chatId };

            foreach (var field in fields)
            {
                payload[field.Key] = field.Value;
            }

            if (options is null)
            {
                return payload;
            }

            // EN: Only what was asked for travels. Sending parse_mode = "None" is not the same as not
            //     sending it: Telegram would reject the value.
            // ES: Viaja solo lo que se pidió. Mandar parse_mode = "None" no es lo mismo que no
            //     mandarlo: Telegram rechazaría ese valor.
            if (options.ParseMode != TelegramParseMode.None)
            {
                payload["parse_mode"] = ParseModeName(options.ParseMode);
            }

            if (options.DisableNotification)
            {
                payload["disable_notification"] = true;
            }

            if (options.InlineKeyboard is { Rows.Count: > 0 } keyboard)
            {
                payload["reply_markup"] = new
                {
                    inline_keyboard = keyboard.Rows
                        .Select(row => row.Select(BuildButton).ToList())
                        .ToList(),
                };
            }

            return payload;
        }

        // EN: A button carries a url or a callback, never both — Telegram rejects one that has the
        //     two, so the key that does not apply must not be in the JSON at all.
        // ES: Un botón lleva url o callback, nunca los dos — Telegram rechaza el que tiene ambos, así
        //     que la clave que no aplica no puede estar en el JSON.
        private static Dictionary<string, object?> BuildButton(TelegramInlineButton button)
        {
            var json = new Dictionary<string, object?> { ["text"] = button.Text };

            if (button.Url is not null)
            {
                json["url"] = button.Url;
            }
            else
            {
                json["callback_data"] = button.CallbackData;
            }

            return json;
        }

        private static string ParseModeName(TelegramParseMode parseMode) => parseMode switch
        {
            TelegramParseMode.Markdown => "Markdown",
            TelegramParseMode.MarkdownV2 => "MarkdownV2",
            TelegramParseMode.Html => "HTML",
            _ => throw new ArgumentOutOfRangeException(nameof(parseMode), parseMode, "Unknown Telegram parse mode."),
        };

        // EN: Telegram puts the wait in the body ("parameters":{"retry_after":N}) and usually also in
        //     a Retry-After header. The body is the authoritative one; the header is the fallback for
        //     an answer that is not the JSON we expect — a proxy's error page, for instance. Without
        //     either, a conservative second so a caller that retries does not hammer.
        // ES: Telegram pone la espera en el cuerpo ("parameters":{"retry_after":N}) y normalmente
        //     también en un header Retry-After. El cuerpo manda; el header es el respaldo para una
        //     respuesta que no sea el JSON esperado — la página de error de un proxy, por ejemplo. Sin
        //     ninguno de los dos, un segundo conservador para que quien reintente no martille.
        private static async Task<TimeSpan> ReadRetryAfterAsync(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                var error = await response.Content.ReadFromJsonAsync<TelegramApiResponse<object>>(JsonOptions, ct);

                if (error?.Parameters?.RetryAfter is int seconds and > 0)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
            catch (JsonException)
            {
                // El cuerpo no era el JSON de Telegram: queda el header.
            }

            if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
            {
                return delta;
            }

            return TimeSpan.FromSeconds(1);
        }

        // EN: One place that builds the request and reads the answer, because the three send methods
        //     only differ in the endpoint and one field — and the 429 handling has to be identical in
        //     the three, which is exactly the kind of thing that rots when it is copied.
        // ES: Un solo lugar que arma el request y lee la respuesta, porque los tres métodos de envío
        //     difieren solo en el endpoint y un campo — y el manejo del 429 tiene que ser idéntico en
        //     los tres, que es justo lo que se pudre cuando se copia.
        private async Task SendAsync(string method, long chatId, Dictionary<string, object?> fields, TelegramSendOptions? options, CancellationToken ct)
        {
            var client = httpClientFactory.CreateClient(TelegramClientNames.Send);
            var payload = BuildPayload(chatId, fields, options);

            var response = await client.PostAsJsonAsync($"{this.ApiBase}/{method}", payload, JsonOptions, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var retryAfter = await ReadRetryAfterAsync(response, ct);

                logger.LogWarning(
                    "Telegram is rate limiting bot '{BotName}' on {Method} to chatId {ChatId}. It asked to wait {RetryAfterSeconds}s.",
                    this.Name,
                    method,
                    chatId,
                    retryAfter.TotalSeconds);

                throw new TelegramRateLimitException(this.Name, retryAfter);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Telegram {Method} failed for bot '{BotName}', chatId {ChatId}: {Status}", method, this.Name, chatId, response.StatusCode);
                response.EnsureSuccessStatusCode();
            }

            logger.LogInformation("Telegram {Method} sent via bot '{BotName}' to chatId {ChatId}", method, this.Name, chatId);
        }

        public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long? offset = null, int limit = 100, int timeoutSeconds = 0, CancellationToken ct = default)
        {
            // EN: The polling client, not the sending one: this call waits 25 seconds on purpose and
            //     must not be retried — the loop around it is already the retry.
            // ES: El cliente de polling, no el de envío: esta llamada espera 25 segundos a propósito y
            //     no debe reintentarse — el loop que la rodea ya es el reintento.
            var client = httpClientFactory.CreateClient(TelegramClientNames.Polling);
            var url = $"{this.ApiBase}/getUpdates?limit={limit}&timeout={timeoutSeconds}";
            if (offset.HasValue)
            {
                url += $"&offset={offset.Value}";
            }

            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TelegramApiResponse<List<TelegramUpdate>>>(JsonOptions, ct);
            return result?.Result ?? [];
        }
    }
}
