using UnityEngine;

// Controla o comportamento de Image Targets (Enemy ou Action)
public class ImageTargets : MonoBehaviour
{
    // Define o tipo de target reconhecido
    public enum TargetType { None, Enemy, Action }

    // IDs de inimigos (compatível com GameManager.EnemyId)
    public enum EnemyIdOption
    {
        None = -1,
        Goblin = 0,
        Orc = 1,
        Minotaur = 2,
        Dragon = 3
    }

    // IDs de ações (compatível com GameManager.ActionId)
    public enum ActionIdOption
    {
        None = -1,
        Water = 0,
        Fire = 1,
        Rock = 2,
        Thunder = 3,
        Apple = 4,
        Bread = 5,
        Escape = 6,
        Coin = 7,
        Coins = 8,
        MultiCoins = 9
    }

    [Header("Tipo do Target")]
    // Tipo do target (Enemy ou Action)
    [SerializeField] private TargetType targetType = TargetType.None;

    [Header("Enemy ID")]
    // Identificador do inimigo associado
    [SerializeField] private EnemyIdOption enemyId = EnemyIdOption.None;

    [Header("Action ID")]
    // Identificador da ação associada
    [SerializeField] private ActionIdOption actionId = ActionIdOption.None;

    [Header("Visual Root")]
    // Objeto raiz dos elementos visuais do target
    [SerializeField] private GameObject visualRoot;

    [Header("References")]
    // Referência ao GameManager
    [SerializeField] private GameManager gameManager;

    // Referência ao SFXManager (opcional)
    [SerializeField] private SFXManager sfxManager;

    // Cache do estado visual para evitar chamadas repetidas
    private bool visualIsActive;

    // Inicializa referências e estado visual
    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (sfxManager == null)
            sfxManager = FindFirstObjectByType<SFXManager>();

        if (visualRoot == null)
            visualRoot = gameObject;

        if (targetType == TargetType.Action)
            SetVisual(false);
        else
            visualIsActive = visualRoot.activeSelf;
    }

    // Chamado quando o target é reconhecido
    public void HandleTargetFound()
    {
        if (gameManager == null) return;

        switch (targetType)
        {
            case TargetType.Enemy:
                if (enemyId == EnemyIdOption.None) return;

                SetVisual(true);
                // SFX: tocar "appear" do inimigo (1x)
                sfxManager?.PlayEnemyAppearOnce((GameManager.EnemyId)(int)enemyId);
                // Dragon: intro + troca música (boss)
                if (enemyId == EnemyIdOption.Dragon)
                    sfxManager?.TriggerDragonBossSequence();
                gameManager.OnEnemyTracked((GameManager.EnemyId)(int)enemyId);
                break;

            case TargetType.Action:
                if (actionId == ActionIdOption.None) return;

                if (!gameManager.CanAcceptActionTracking())
                {
                    SetVisual(false);
                    return;
                }

                SetVisual(true);
                RestartParticles();
                // SFX: cartas podem tocar sempre ao aparecer
                sfxManager?.PlayCardAppear((GameManager.ActionId)(int)actionId);
                gameManager.OnActionTracked((GameManager.ActionId)(int)actionId);
                break;
        }
    }

    // Chamado quando o target deixa de ser reconhecido
    public void HandleTargetLost()
    {
        switch (targetType)
        {
            case TargetType.Enemy:
            case TargetType.Action:
                SetVisual(false);
                break;
        }

        if (gameManager == null) return;

        switch (targetType)
        {
            case TargetType.Enemy:
                gameManager.OnEnemyTrackingLost();
                break;

            case TargetType.Action:
                gameManager.OnActionTrackingLost();
                break;
        }
    }

    // Ativa ou desativa o visual do target
    private void SetVisual(bool active)
    {
        if (visualRoot == null) return;
        if (visualIsActive == active) return;

        visualIsActive = active;
        visualRoot.SetActive(active);
    }

    // Reinicia partículas ao reconhecer uma ação
    private void RestartParticles()
    {
        if (visualRoot == null) return;

        var psList = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in psList)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }
    }
}