using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly AiConfig _config;

        public AiClient()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json"))
        {
        }

        public AiClient(string configPath)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _config = LoadConfig(configPath);
        }

        public Task<string> AskAsync(string prompt)
        {
            string configuredProvider = _config.DefaultProvider;
            if (string.IsNullOrWhiteSpace(configuredProvider))
                return AskAsync(AiProvider.Gemini, prompt);

            AiProvider provider;
            if (!Enum.TryParse(configuredProvider, true, out provider) ||
                !Enum.IsDefined(typeof(AiProvider), provider))
                throw new InvalidOperationException("DefaultProvider sconosciuto in config.json: " + configuredProvider);

            return AskAsync(provider, prompt);
        }

        public Task<string> AskAsync(AiProvider provider, string prompt)
        {
            return AskAsync(provider, prompt, null);
        }

        public async Task<string> AskAsync(AiProvider provider, string prompt, string model)
        {
            ProviderSettings settings = GetProviderSettings(provider);
            model = model ?? settings.DefaultModel;
            bool hasApiKey = !string.IsNullOrWhiteSpace(settings.ApiKey) &&
                             !settings.ApiKey.StartsWith("INSERISCI_", StringComparison.OrdinalIgnoreCase);

            if (!hasApiKey && provider != AiProvider.Ollama)
                throw new InvalidOperationException("API key " + provider + " mancante nel file config.json.");

            switch (provider)
            {
                case AiProvider.Gemini:
                    return await AskGeminiAsync(settings, prompt, model);

                case AiProvider.Anthropic:
                    return await AskAnthropicAsync(settings, prompt, model);

                default:
                    return await AskOpenAiCompatibleAsync(provider, settings, prompt, model, hasApiKey);
            }
        }

        private async Task<string> AskOpenAiCompatibleAsync(
            AiProvider provider,
            ProviderSettings settings,
            string prompt,
            string model,
            bool hasApiKey)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl))
            {
                if (hasApiKey)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

                JObject response = await SendAsync(request, CreateMessagesBody(model, prompt));
                JToken content = response.SelectToken("choices[0].message.content");
                string answer = content != null && content.Type == JTokenType.String
                    ? content.Value<string>()
                    : null;
                return RequireText(provider, answer);
            }
        }

        private async Task<string> AskGeminiAsync(ProviderSettings settings, string prompt, string model)
        {
            string url = settings.BaseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(model) + ":generateContent";
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("x-goog-api-key", settings.ApiKey);

                JObject partBody = new JObject { { "text", prompt ?? "" } };
                JObject contentBody = new JObject { { "parts", new JArray(partBody) } };
                JObject body = new JObject { { "contents", new JArray(contentBody) } };

                JObject response = await SendAsync(request, body);
                StringBuilder answer = new StringBuilder();
                foreach (JToken part in response.SelectTokens("candidates[0].content.parts[*]"))
                {
                    if (part.Value<bool?>("thought") != true)
                        answer.Append(part.Value<string>("text"));
                }

                return RequireText(AiProvider.Gemini, answer.ToString());
            }
        }

        private async Task<string> AskAnthropicAsync(ProviderSettings settings, string prompt, string model)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl))
            {
                request.Headers.Add("x-api-key", settings.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                JObject body = CreateMessagesBody(model, prompt);
                body.Add("max_tokens", 1024);

                JObject response = await SendAsync(request, body);
                StringBuilder answer = new StringBuilder();
                foreach (JToken text in response.SelectTokens("content[?(@.type == 'text')].text"))
                    answer.Append(text.Value<string>());

                return RequireText(AiProvider.Anthropic, answer.ToString());
            }
        }

        private static JObject CreateMessagesBody(string model, string prompt)
        {
            JObject message = new JObject { { "role", "user" }, { "content", prompt ?? "" } };
            return new JObject { { "model", model }, { "messages", new JArray(message) } };
        }

        private static async Task<JObject> SendAsync(HttpRequestMessage request, JObject body)
        {
            request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            using (HttpResponseMessage response = await _http.SendAsync(request))
            {
                string json = await response.Content.ReadAsStringAsync();
                EnsureSuccess(response, json);
                return JObject.Parse(json);
            }
        }

        private static string RequireText(AiProvider provider, string answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
                throw new InvalidOperationException(provider + " non ha restituito una risposta testuale.");

            return answer;
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
                    throw new NotSupportedException("L'API di GitHub Models è stata ritirata.");

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

        [DataContract]
        private class AiConfig
        {
            [DataMember]
            public string DefaultProvider { get; set; }

            [DataMember]
            public ProviderSettings OpenAI { get; set; }

            [DataMember]
            public ProviderSettings DeepSeek { get; set; }

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