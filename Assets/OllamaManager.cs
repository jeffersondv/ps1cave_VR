using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
        public int num_ctx = 1024;       // Contexto limitado para velocidade
        public int num_predict = 150;     // Máximo de tokens na resposta do NPC
        public float temperature = 0.7f;
    }

    [Serializable]
    private class ChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream = false;      // Defina true se quiser processar via streaming
        public OptionsPayload options;
    }

    [Serializable]
    private class ChatResponse
    {
        public ChatMessage message;
        public bool done;
    }

    /// <summary>
    /// Envia uma mensagem para o Ollama e retorna a resposta do NPC assincronamente.
    /// </summary>
    public async Task<string> SendPromptAsync(string systemPrompt, string userMessage)
    {
        string url = $"http://{serverIp}:{port}/api/chat";

        ChatRequest requestData = new ChatRequest
        {
            model = modelName,
            messages = new ChatMessage[]
            {
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user", content = userMessage }
            },
            stream = false,
            options = new OptionsPayload()
        };

        string jsonPayload = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = requestTimeoutSeconds;

            // Envia a requisição sem travar a thread principal do Unity
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield(); // Devolve o controle para o frame do Unity
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Erro ao conectar ao Ollama em {url} (modelo '{modelName}'): {request.error} {request.downloadHandler?.text}");
                return null;
            }

            string jsonResponse = request.downloadHandler.text;
            ChatResponse responseData = JsonUtility.FromJson<ChatResponse>(jsonResponse);

            return responseData.message?.content;
        }
    }
}