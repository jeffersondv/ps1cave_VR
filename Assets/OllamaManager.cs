using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class OllamaManager : MonoBehaviour
{
    [Header("Configuração de Rede")]
    [Tooltip("IP do PC rodando o Ollama na mesma rede Wi-Fi")]
    [SerializeField] private string serverIp = "192.168.1.100";
    [SerializeField] private int port = 11434;
    [Tooltip("Tempo máximo de espera pela resposta (s). A 1ª chamada carrega o modelo e é mais lenta.")]
    [SerializeField] private int requestTimeoutSeconds = 60;

    [Header("Configuração do Modelo")]
    [SerializeField] private string modelName = "qwen2.5:1.5b";
    [Tooltip("Máximo de tokens gerados por resposta. ~60 tokens ≈ 2 frases curtas.")]
    [SerializeField] private int maxTokens = 70;
    [Tooltip("Mais baixo = respostas mais previsíveis e focadas.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float temperature = 0.4f;
    [Tooltip("Tamanho do contexto. Menor = mais rápido. Mudar este valor entre chamadas faz o Ollama recarregar o modelo.")]
    [SerializeField] private int contextSize = 1024;

    [Header("Tempo de resposta")]
    [Tooltip("Quanto tempo o Ollama mantém o modelo na memória após a última chamada (ex.: 30m, 1h, 2h; -1m = sempre). " +
             "O padrão do Ollama (5m) faz o modelo ser recarregado no meio do jogo, o que leva segundos.")]
    [SerializeField] private string keepAlive = "1h";
    [Tooltip("Carrega o modelo assim que a cena inicia, para a 1ª dica não esperar o carregamento.")]
    [SerializeField] private bool aquecerAoIniciar = true;
    [Tooltip("Mostra no Console o tempo de cada resposta.")]
    [SerializeField] private bool logTempo = false;

    // Um aquecimento por servidor+modelo, mesmo com vários OllamaManager na cena
    private static readonly HashSet<string> warmedUp = new HashSet<string>();

    // Estruturas de Dados para JSON
    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OptionsPayload
    {
        public int num_ctx = 1024;       // Contexto limitado para velocidade (igual em todas as chamadas)
        public int num_predict = 70;      // Máximo de tokens na resposta do NPC
        public float temperature = 0.4f;
        public float top_p = 0.85f;
        public float repeat_penalty = 1.15f;
    }

    [Serializable]
    private class ChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream = false;      // Defina true se quiser processar via streaming
        public string keep_alive;
        public OptionsPayload options;
    }

    [Serializable]
    private class ChatResponse
    {
        public ChatMessage message;
        public bool done;
    }

    private string Url => $"http://{serverIp}:{port}/api/chat";

    // O Ollama exige unidade ("30m"); um número sozinho vira minutos
    private string KeepAlive
    {
        get
        {
            string k = string.IsNullOrWhiteSpace(keepAlive) ? "1h" : keepAlive.Trim();
            return int.TryParse(k, out _) ? k + "m" : k;
        }
    }

    private OptionsPayload BuildOptions(int tokens, float temp) => new OptionsPayload
    {
        num_ctx = contextSize,
        num_predict = tokens > 0 ? tokens : maxTokens,
        temperature = temp >= 0f ? temp : temperature
    };

    private void Start()
    {
        if (aquecerAoIniciar)
            _ = WarmUpAsync();
    }

    /// <summary>
    /// Pede ao Ollama para carregar o modelo (chat sem mensagens) com as mesmas opções das chamadas
    /// normais. Assim a primeira dica não espera o carregamento (1–12 s conforme o modelo).
    /// </summary>
    public async Task WarmUpAsync()
    {
        string key = $"{serverIp}:{port}/{modelName}/{contextSize}";
        if (!warmedUp.Add(key)) return;

        var data = new ChatRequest
        {
            model = modelName,
            messages = new ChatMessage[0],
            stream = false,
            keep_alive = KeepAlive,
            options = BuildOptions(-1, -1f)
        };
        Stopwatch sw = Stopwatch.StartNew();
        string result = await PostAsync(JsonUtility.ToJson(data), Mathf.Max(requestTimeoutSeconds, 120));
        if (result == null)
            warmedUp.Remove(key); // tenta de novo na próxima cena/chamada
        else
            Debug.Log($"[Ollama] Modelo '{modelName}' carregado em {sw.Elapsed.TotalSeconds:0.0}s ({serverIp}).");
    }

    /// <summary>
    /// Envia uma mensagem para o Ollama e retorna a resposta do NPC assincronamente.
    /// </summary>
    public Task<string> SendPromptAsync(string systemPrompt, string userMessage) =>
        SendPromptAsync(systemPrompt, userMessage, -1, -1f);

    /// <summary>
    /// Igual ao anterior, com limite de tokens e temperatura próprios desta chamada
    /// (valores negativos usam os do Inspector).
    /// </summary>
    public async Task<string> SendPromptAsync(string systemPrompt, string userMessage, int tokens, float temp)
    {
        ChatRequest requestData = new ChatRequest
        {
            model = modelName,
            messages = new ChatMessage[]
            {
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user", content = userMessage }
            },
            stream = false,
            keep_alive = KeepAlive,
            options = BuildOptions(tokens, temp)
        };

        Stopwatch sw = Stopwatch.StartNew();
        string jsonResponse = await PostAsync(JsonUtility.ToJson(requestData), requestTimeoutSeconds);
        if (jsonResponse == null) return null;

        ChatResponse responseData = JsonUtility.FromJson<ChatResponse>(jsonResponse);
        if (logTempo)
            Debug.Log($"[Ollama] '{modelName}' respondeu em {sw.Elapsed.TotalSeconds:0.00}s.");
        return responseData?.message?.content;
    }

    private async Task<string> PostAsync(string jsonPayload, int timeoutSeconds)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(Url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            // Envia a requisição sem travar a thread principal do Unity
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield(); // Devolve o controle para o frame do Unity
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Erro ao conectar ao Ollama em {Url} (modelo '{modelName}'): {request.error} {request.downloadHandler?.text}");
                return null;
            }

            return request.downloadHandler.text;
        }
    }
}
