using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace filetto_con_ai
{
    public enum AiProvider
    {
        OpenAI,
        DeepSeek,
        GitHubModels,
        Gemini,
        Anthropic,
        Mistral,
        Groq,
        Ollama
    }

    public class AiClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly AiConfig _config;

        public AiClient()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"))
        {
        }

        public AiClient(string configPath)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            _http.Timeout = TimeSpan.FromSeconds(60);

            _config = LoadConfig(configPath);
        }

        public async Task<string> AskAsync(AiProvider provider, string prompt)
        {
            ProviderSettings settings = GetProviderSettings(provider);

            return await AskAsync(provider, prompt, settings.DefaultModel);
        }

        public async Task<string> AskAsync(AiProvider provider, string prompt, string model)
        {
            ProviderSettings settings = GetProviderSettings(provider);

            switch (provider)
            {
                case AiProvider.OpenAI:
                case AiProvider.DeepSeek:
                case AiProvider.Mistral:
                case AiProvider.Groq:
                    return await AskOpenAiCompatibleAsync(settings, prompt, model, null, null);

                case AiProvider.GitHubModels:
                    return await AskOpenAiCompatibleAsync(
                        settings,
                        prompt,
                        model,
                        "application/vnd.github+json",
                        null);

                case AiProvider.Ollama:
                    return await AskOpenAiCompatibleAsync(
                        settings,
                        prompt,
                        model,
                        null,
                        "ollama");

                case AiProvider.Gemini:
                    return await AskGeminiAsync(settings, prompt, model);

                case AiProvider.Anthropic:
                    return await AskAnthropicAsync(settings, prompt, model);

                default:
                    throw new NotSupportedException("Provider non supportato: " + provider);
            }
        }

        private async Task<string> AskOpenAiCompatibleAsync(
            ProviderSettings settings,
            string prompt,
            string model,
            string acceptHeader,
            string fakeBearerToken)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl))
            {
                if (!string.IsNullOrEmpty(settings.ApiKey))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                }
                else if (!string.IsNullOrEmpty(fakeBearerToken))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", fakeBearerToken);
                }

                if (!string.IsNullOrEmpty(acceptHeader))
                    request.Headers.TryAddWithoutValidation("Accept", acceptHeader);

                string body =
                    "{"
                    + "\"model\":\"" + EscapeJson(model) + "\","
                    + "\"messages\":["
                    + "{"
                    + "\"role\":\"user\","
                    + "\"content\":\"" + EscapeJson(prompt) + "\""
                    + "}"
                    + "]"
                    + "}";

                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await _http.SendAsync(request))
                {
                    string json = await response.Content.ReadAsStringAsync();

                    EnsureSuccess(response, json);

                    return ExtractBetween(
                        json,
                        "\"content\":\"",
                        "\"",
                        true);
                }
            }
        }

        private async Task<string> AskGeminiAsync(
            ProviderSettings settings,
            string prompt,
            string model)
        {
            if (string.IsNullOrEmpty(settings.ApiKey))
                throw new Exception("API key Gemini mancante nel file config.json.");

            string url = settings.BaseUrl
                         + "/"
                         + model
                         + ":generateContent?key="
                         + settings.ApiKey;

            string body =
                "{"
                + "\"contents\":["
                + "{"
                + "\"parts\":["
                + "{"
                + "\"text\":\"" + EscapeJson(prompt) + "\""
                + "}"
                + "]"
                + "}"
                + "]"
                + "}";

            using (HttpResponseMessage response = await _http.PostAsync(
                url,
                new StringContent(body, Encoding.UTF8, "application/json")))
            {
                string json = await response.Content.ReadAsStringAsync();

                EnsureSuccess(response, json);

                return ExtractBetween(
                    json,
                    "\"text\":\"",
                    "\"",
                    true);
            }
        }

        private async Task<string> AskAnthropicAsync(
            ProviderSettings settings,
            string prompt,
            string model)
        {
            if (string.IsNullOrEmpty(settings.ApiKey))
                throw new Exception("API key Anthropic mancante nel file config.json.");

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", settings.ApiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

                string body =
                    "{"
                    + "\"model\":\"" + EscapeJson(model) + "\","
                    + "\"max_tokens\":1024,"
                    + "\"messages\":["
                    + "{"
                    + "\"role\":\"user\","
                    + "\"content\":\"" + EscapeJson(prompt) + "\""
                    + "}"
                    + "]"
                    + "}";

                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await _http.SendAsync(request))
                {
                    string json = await response.Content.ReadAsStringAsync();

                    EnsureSuccess(response, json);

                    return ExtractBetween(
                        json,
                        "\"text\":\"",
                        "\"",
                        true);
                }
            }
        }

        private ProviderSettings GetProviderSettings(AiProvider provider)
        {
            ProviderSettings settings = null;

            switch (provider)
            {
                case AiProvider.OpenAI:
                    settings = _config.OpenAI;
                    break;

                case AiProvider.DeepSeek:
                    settings = _config.DeepSeek;
                    break;

                case AiProvider.GitHubModels:
                    settings = _config.GitHubModels;
                    break;

                case AiProvider.Gemini:
                    settings = _config.Gemini;
                    break;

                case AiProvider.Anthropic:
                    settings = _config.Anthropic;
                    break;

                case AiProvider.Mistral:
                    settings = _config.Mistral;
                    break;

                case AiProvider.Groq:
                    settings = _config.Groq;
                    break;

                case AiProvider.Ollama:
                    settings = _config.Ollama;
                    break;
            }

            if (settings == null)
                throw new Exception("Configurazione mancante per provider: " + provider);

            if (string.IsNullOrEmpty(settings.BaseUrl))
                throw new Exception("BaseUrl mancante per provider: " + provider);

            if (string.IsNullOrEmpty(settings.DefaultModel))
                throw new Exception("DefaultModel mancante per provider: " + provider);

            return settings;
        }

        private static AiConfig LoadConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    "File config.json non trovato. Copia config.example.json e rinominalo in config.json.",
                    configPath);
            }

            using (FileStream stream = new FileStream(configPath, FileMode.Open, FileAccess.Read))
            {
                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(AiConfig));

                AiConfig config = serializer.ReadObject(stream) as AiConfig;

                if (config == null)
                    throw new Exception("Impossibile leggere config.json.");

                return config;
            }
        }

        private static void EnsureSuccess(HttpResponseMessage response, string responseBody)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    "Errore API "
                    + (int)response.StatusCode
                    + " "
                    + response.ReasonPhrase
                    + Environment.NewLine
                    + responseBody);
            }
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string UnescapeJson(string value)
        {
            if (value == null)
                return "";

            return value
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }

        private static string ExtractBetween(
            string source,
            string startToken,
            string endToken,
            bool unescape)
        {
            int startIndex = source.IndexOf(startToken, StringComparison.Ordinal);

            if (startIndex < 0)
                throw new Exception("Campo risposta non trovato. Risposta ricevuta: " + source);

            startIndex += startToken.Length;

            StringBuilder result = new StringBuilder();
            bool escaped = false;

            for (int i = startIndex; i < source.Length; i++)
            {
                char c = source[i];

                if (escaped)
                {
                    result.Append('\\');
                    result.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (source.Substring(i).StartsWith(endToken))
                    break;

                result.Append(c);
            }

            string extracted = result.ToString();

            if (unescape)
                return UnescapeJson(extracted);

            return extracted;
        }

        [DataContract]
        private class AiConfig
        {
            [DataMember]
            public ProviderSettings OpenAI { get; set; }

            [DataMember]
            public ProviderSettings DeepSeek { get; set; }

            [DataMember]
            public ProviderSettings GitHubModels { get; set; }

            [DataMember]
            public ProviderSettings Gemini { get; set; }

            [DataMember]
            public ProviderSettings Anthropic { get; set; }

            [DataMember]
            public ProviderSettings Mistral { get; set; }

            [DataMember]
            public ProviderSettings Groq { get; set; }

            [DataMember]
            public ProviderSettings Ollama { get; set; }
        }

        [DataContract]
        private class ProviderSettings
        {
            [DataMember]
            public string ApiKey { get; set; }

            [DataMember]
            public string BaseUrl { get; set; }

            [DataMember]
            public string DefaultModel { get; set; }
        }
    }
}