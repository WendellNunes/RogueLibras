using System.Collections.Generic;
using UnityEngine;

// Controla o progresso da run e verifica condição de vitória
public class GameProgressManager : MonoBehaviour
{
    // Instância única global
    public static GameProgressManager Instance { get; private set; }

    // Conjunto de inimigos derrotados ao menos uma vez
    private readonly HashSet<GameManager.EnemyId> defeated = new HashSet<GameManager.EnemyId>();

    // Garante singleton e mantém entre cenas
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

    // Reinicia o progresso da run
    public void ResetProgress()
    {
        defeated.Clear();
    }

    // Registra a derrota de um inimigo e verifica vitória
    public bool RegisterEnemyDefeated(GameManager.EnemyId enemyId)
    {
        defeated.Add(enemyId);

        // Vitória ao derrotar todos os inimigos únicos
        if (defeated.Count >= 4)
        {
            var gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.TriggerVictory();
                return true;
            }
        }

        return false;
    }

    // Verifica se um inimigo específico já foi derrotado
    public bool HasDefeated(GameManager.EnemyId enemyId) => defeated.Contains(enemyId);

    // Retorna a quantidade de inimigos únicos derrotados
    public int UniqueDefeatedCount => defeated.Count;
}