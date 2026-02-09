using UnityEngine;
using TMPro;

// Atualiza os textos da tela de Game Over
public class GameOverUI : MonoBehaviour
{
    [Header("UI (TMP)")]
    // Texto que exibe o tempo da partida
    [SerializeField] private TextMeshProUGUI timeText;

    // Texto que exibe a pontuação final
    [SerializeField] private TextMeshProUGUI scoreText;

    // Preenche os textos com os dados da sessão ao iniciar a cena
    private void Start()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        if (timeText != null)
            timeText.text = $"Tempo:{session.GetFormattedTime()}";

        if (scoreText != null)
            scoreText.text = $"Pontos:{session.Score}";
    }
}