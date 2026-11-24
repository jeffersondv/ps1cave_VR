using UnityEngine;
using UnityEngine.SceneManagement; // Importante para gerenciar cenas

public class GameController : MonoBehaviour
{
    public void RestartGame()
    {
        // Garante que o tempo do jogo volte ao normal (caso o jogo estivesse pausado)
        Time.timeScale = 1f;
        CountdownTimer.ResetForNewGame();

        Debug.Log("Reiniciando o jogo...");

        // Obtém o índice da cena ativa atual e a recarrega
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }
}