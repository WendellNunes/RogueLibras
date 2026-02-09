using UnityEngine;
using System;

// Mantém tempo e pontuação da run entre cenas
public class GameSession : MonoBehaviour
{
    // Instância única global
    public static GameSession Instance { get; private set; }

    // Indica se a run está em andamento
    public bool IsRunning { get; private set; }

    // Tempo total decorrido em segundos
    public float ElapsedSeconds { get; private set; }

    // Pontuação acumulada
    public int Score { get; private set; }

    // Garante singleton e mantém o objeto entre cenas
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Atualiza o tempo enquanto a run estiver ativa
    private void Update()
    {
        if (!IsRunning) return;
        ElapsedSeconds += Time.deltaTime;
    }

    // Inicia a run e zera tempo e pontuação
    public void StartRun()
    {
        ElapsedSeconds = 0f;
        Score = 0;
        IsRunning = true;
    }

    // Encerra a contagem da run
    public void StopRun() => IsRunning = false;

    // Adiciona pontos à pontuação
    public void AddScore(int amount)
    {
        if (amount <= 0) return;
        Score += amount;
    }

    // Retorna o tempo formatado em MM:SS
    public string GetFormattedTime()
    {
        int total = Mathf.FloorToInt(ElapsedSeconds);
        int minutes = total / 60;
        int seconds = total % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}