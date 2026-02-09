using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // =========================
    // ENUMS
    // =========================
    public enum GameState
    {
        AwaitEnemyTracking,
        BattleReady,         // inimigo encontrado (Attack + Pass)
        AwaitActionTracking, // depois de Attack (Pass)
        CardReady,           // carta encontrada (Use + Pass)
        Quiz,
        EnemyTurn,           
        // Intervalo entre batalhas (bag + uso de itens/moedas via câmera + opcionalmente acesso ao painel de compra)
        Intermission,
        Shop,
        Victory,
        GameOver
    }

    public enum EnemyId { Goblin, Orc, Minotaur, Dragon }

    public enum ActionId
    {
        
        None = -1,Water, Fire, Rock, Thunder,
        Apple, Bread,
        Escape,
        Coin, Coins, MultiCoins
    }

    // =========================
    // UI (ASSIGN IN INSPECTOR)
    // =========================
    [Header("UI Text (TMP)")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyHpText;
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI coinsText;

    [Header("UI Panels")]
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private GameObject shopPanel;

    [Header("Shop model in scene (low poly)")]
    [SerializeField] private GameObject shopModelObject;

    [Header("Buttons (enable/disable)")]
    [SerializeField] private GameObject attackButtonObject;
    [SerializeField] private GameObject passTurnButtonObject;
    [SerializeField] private GameObject useButtonObject;
    [SerializeField] private GameObject challengeButtonObject;


    [Header("Delays")]
    [SerializeField] private float uiButtonDelay = 0.2f;
    [SerializeField] private float sfxDelay = 1.0f;
    [Header("Shop Manager (opcional)")]
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ItemsManager itemsManager;

        
    [Header("Music (opcional)")]
    [SerializeField] private MusicController musicController;

    [Header("SFX (opcional)")]
    [SerializeField] private SFXManager sfxManager;

    [Header("Libras Quiz")]
    [SerializeField] private LibrasQuestionManager librasQuestionManager;

    [Header("Scenes")]
    [SerializeField] private int gameOverSceneIndex = 2;
    [Header("Win")]
    [SerializeField] private int victorySceneIndex = 2;


    // =========================
    // PLAYER STATS
    // =========================
    [Header("Player Stats")]
    [SerializeField] private int playerMaxHp = 100;
    [SerializeField] private int playerStartHp = 100;

    // =========================
    // TRACKING
    // =========================
    [Header("AR Tracking Settings")]
    [SerializeField] private float trackDebounceSeconds = 0.30f;

    [Header("Locks")]
    [SerializeField] private bool lockEnemyUntilBattleEnds = true;

    [Header("Battle timings")]
    [SerializeField] private float enemyAttackDelaySeconds = 0.5f;

    // =========================
    // INVENTORY
    // =========================
    [Header("Inventory (cards)")]
    [SerializeField] private bool enforceInventory = true;

    private readonly Dictionary<ActionId, int> inventory = new Dictionary<ActionId, int>();

    // =========================
    // RUNTIME
    // =========================
    [SerializeField] private GameState state = GameState.AwaitEnemyTracking;

    // Expor estado atual para outros managers (ItemsManager/ShopManager)
    public GameState CurrentState => state;

    private EnemyId currentEnemyId;
    private int currentEnemyMaxHp;
    private int currentEnemyHp;
    private int currentEnemyAttackDamage;

    private int playerHp;
    private int money;

    private bool enemyTracked = false;

// Action tracking state (para Intermission rearm sem precisar perder/achar novamente)
private bool actionTrackedNow = false;
private ActionId lastTrackedActionId = ActionId.None;
    private bool enemyLocked = false;

    // só aceita Action depois de Attack
    private bool attackPressedThisTurn = false;

    private bool actionTracked = false;
    private ActionId selectedAction;

    private float lastEnemyTrackTime = -999f;
    private float lastActionTrackTime = -999f;

    // quiz
    private bool correctIsA;
    private float quizStartTime;

    [Header("Monster miss chance (fast answer)")]
    [SerializeField] private float fastAnswerSeconds = 2.0f;
    [SerializeField] private float monsterMissChanceIfFast = 0.35f;
    private bool monsterNextAttackCanMiss = false;

    // =========================
    // BUGFIX GUARDS
    // =========================
    private bool resolvingTurn = false;      // impede resolver quiz 2x (dano duplicado)
    private bool usePressedLock = false;     // impede Use duplo
    private Coroutine enemyAttackRoutine;    // garante só 1 ataque do monstro por vez

    private readonly Dictionary<EnemyId, int> scoreByEnemy = new Dictionary<EnemyId, int>
    {
        { EnemyId.Goblin, 30 },
        { EnemyId.Orc, 70 },
        { EnemyId.Minotaur, 100 },
        // FIX: Dragão vale 200 (não 300)
        { EnemyId.Dragon, 200 }
    };

    // =========================
    // SHOP CURRENCY QUIZ (usar moedas dentro da lojinha)
    // =========================
    private bool shopCurrencyMode = false;
    private ActionId pendingCurrencyId;
    private int pendingCurrencyQty = 0;

    private GameState shopReturnState;
    // SHOP ITEM QUIZ (usar Apple/Bread dentro da lojinha)
    private bool shopItemMode = false;
    private ActionId pendingShopItemId;
    private int pendingShopItemQty = 0;

    // =========================
    // INTERMISSION: USAR ITENS/MOEDAS (Apple/Bread/Coin/Coins/MultiCoins)
    // =========================
    private bool intermissionUseMode = false;
    private ActionId pendingIntermissionId;
    private int pendingIntermissionQty = 0;

    private void Start()
    {
        if (librasQuestionManager == null)
            librasQuestionManager = FindObjectOfType<LibrasQuestionManager>();

        if (shopManager == null)
            shopManager = FindObjectOfType<ShopManager>();

        // Auto-assign messageText if it was not set in Inspector (helps after script changes)
        // Heurística: pega um TMP "grande" que não seja HP/Coins/Enemy.
        if (messageText == null)
        {
            var allTmp = FindObjectsOfType<TextMeshProUGUI>(true);
            TextMeshProUGUI best = null;
            float bestArea = -1f;

            foreach (var t in allTmp)
            {
                var n = t.gameObject.name.ToLowerInvariant();
                if (n.Contains("hp") || n.Contains("coin") || n.Contains("money") || n.Contains("score") || n.Contains("enemy") || n.Contains("inim"))
                    continue;

                bool looksLikeTracking = n.Contains("message") || n.Contains("msg") || n.Contains("status") || n.Contains("rastre") || n.Contains("track") || n.Contains("banner");
                var r = t.rectTransform;
                float area = Mathf.Abs(r.rect.width * r.rect.height);

                // prioridade: nomes que parecem tracking + área grande
                float weight = area + (looksLikeTracking ? 100000f : 0f);
                if (weight > bestArea)
                {
                    bestArea = weight;
                    best = t;
                }
            }

            messageText = best;
            if (messageText == null)
                Debug.LogError("[GameManager] messageText NÃO está atribuído no Inspector e não consegui auto-detectar. Arraste o TMP do banner de tracking aqui.");
        }

        if (GameSession.Instance == null)
        {
            var go = new GameObject("GameSession");
            go.AddComponent<GameSession>();
        }

        // Garante um ProgressManager para vitória ao derrotar os 4 inimigos
        if (GameProgressManager.Instance == null)
        {
            var goProg = new GameObject("GameProgressManager");
            goProg.AddComponent<GameProgressManager>();
        }

        GameSession.Instance.StartRun();

        InitGame();
        EnterState(GameState.AwaitEnemyTracking);
        RefreshUI();
    }

    private void InitGame()
    {
        playerHp = Mathf.Clamp(playerStartHp, 0, playerMaxHp);
        money = 100;

        enemyTracked = false;
        enemyLocked = false;
        attackPressedThisTurn = false;
        actionTracked = false;

        resolvingTurn = false;
        usePressedLock = false;
        StopEnemyAttackRoutineIfAny();

        inventory.Clear();

        AddCard(ActionId.Apple, 5);
        AddCard(ActionId.Bread, 5);
        AddCard(ActionId.Escape, 5);
        AddCard(ActionId.Fire, 5);
        AddCard(ActionId.Water, 5);
        AddCard(ActionId.Rock, 5);
        AddCard(ActionId.Thunder, 5);

        AddCard(ActionId.Coin, 0);
        AddCard(ActionId.Coins, 0);
        AddCard(ActionId.MultiCoins, 0);

        SetEnemyUnknownUI();

        // Reset de progresso do run (vitória ao derrotar os 4 inimigos)
        GameProgressManager.Instance?.ResetProgress();
    }

    public void TriggerVictory()
    {
        // Entra no estado de vitória e encerra a run
        EnterState(GameState.Victory);
    }


    // =========================
    // PUBLIC GATES (ImageTargets usa isso)
    // =========================
    public bool CanAcceptEnemyTracking()
    {
        // Durante quiz/loja/turno do inimigo, não aceita tracking
        if (state == GameState.Shop || state == GameState.Intermission || state == GameState.Quiz || state == GameState.EnemyTurn) return false;
        return true;
    }

    public bool CanAcceptActionTracking()
    {
        // =========================
        // INTERMISSION: permitir rastrear itens/moedas "usáveis" (Apple/Bread/Coin/Coins/MultiCoins)
        // =========================
        if (state == GameState.Intermission || state == GameState.Shop)
            return itemsManager != null && itemsManager.CanAcceptIntermissionUseTracking();

        // Durante quiz/turno do inimigo, não aceita rastreio

        // Durante quiz/turno do inimigo, não aceita rastreio
        if (state == GameState.Quiz || state == GameState.EnemyTurn) return false;

        // =========================
        // BATALHA: rastrear cartas de ação (exceto moedas)
        // =========================
        if (!enemyTracked) return false;
        if (!attackPressedThisTurn) return false;

        // pode rastrear carta enquanto espera carta OU já com carta pronta (para trocar)
        return (state == GameState.AwaitActionTracking || state == GameState.CardReady);
    }

    // =========================
    // STATE
    // =========================
    
    private void EnsureMusicController()
    {
        if (musicController == null)
        {
            musicController = FindObjectOfType<MusicController>();
        }
    }

    private void UpdateMusicForState(GameState st)
    {
        EnsureMusicController();
        if (musicController == null) return;

        // Quiz pode acontecer tanto na batalha quanto na lojinha/intermission.
        // Então decidimos a trilha com base no "contexto" (flags de uso fora da batalha).
        if (st == GameState.Quiz)
        {
            bool quizIsOutOfBattle = shopCurrencyMode || shopItemMode || intermissionUseMode ||
                                    shopReturnState == GameState.Shop || shopReturnState == GameState.Intermission;

            if (quizIsOutOfBattle) musicController.PlayShop();
            else musicController.PlayBattle();
            return;
        }

        // Estados de combate: toca a música de batalha
        if (st == GameState.AwaitEnemyTracking ||
            st == GameState.BattleReady ||
            st == GameState.AwaitActionTracking ||
            st == GameState.CardReady ||
            st == GameState.EnemyTurn)
        {
            musicController.PlayBattle();
            return;
        }

        // Intervalo/lojinha
        if (st == GameState.Intermission || st == GameState.Shop)
        {
            musicController.PlayShop();
            return;
        }

        // Outros (Victory/GameOver): mantém a última, ou se quiser, pode parar.
    }

private void EnterState(GameState newState)
    {
        state = newState;
        UpdateMusicForState(state);

        if (shopManager != null)
        {
            // A Store só deve ser interativa quando estivermos no período entre batalhas (Intermission)
            // ou quando explicitamente entrarmos no estado Shop.
            shopManager.SetStoreLocked(state != GameState.Shop && state != GameState.Intermission);
        }

        // A Bag/Itens só deve ser usada no intervalo entre batalhas
        itemsManager?.SetBagLocked(state != GameState.Intermission && state != GameState.Shop);

        SetActive(quizPanel, false);
        SetActive(shopPanel, false);
        SetActive(shopModelObject, false);

        SetActive(attackButtonObject, false);
        SetActive(passTurnButtonObject, false);
        SetActive(useButtonObject, false);
        SetActive(challengeButtonObject, false);

        switch (state)
        {
            case GameState.AwaitEnemyTracking:
                SetMessage("Rastreando");
                SetEnemyUnknownUI();
                break;

            case GameState.BattleReady:
                SetMessage("Encontrado");
                SetActive(attackButtonObject, true);
                SetActive(passTurnButtonObject, true);
                break;

            case GameState.AwaitActionTracking:
                // Depois de clicar em ATACAR: rastrear uma Action
                SetMessage("Rastreando");
                SetActive(passTurnButtonObject, true);
                break;

            case GameState.CardReady:
                // Carta encontrada: mostre o NOME da carta (o jogador precisa ver o que foi rastreado)
                SetMessage(GetDisplayName(selectedAction));
                SetActive(useButtonObject, true);
                SetActive(passTurnButtonObject, true);
                break;

            case GameState.Quiz:
                // Durante o quiz, o feedback (Correto!/Errado!) aparece no banner de tracking.
                // Aqui colocamos um rótulo neutro.
                SetMessage("Responda");
                SetActive(quizPanel, true);
                break;

            case GameState.EnemyTurn:
                SetMessage("Inimigo");
                // nenhum botão aqui
                break;

            case GameState.Intermission:
                // Intervalo pós-batalha: aqui entram Bag/Itens e (opcionalmente) o painel de compra
                // "Lojinha" deve ficar no cabeçalho (enemyNameText), não no texto de tracking.
                // O texto de tracking (messageText) fica livre para mostrar ações como "Rastreando"/"Encontrado".
                // NÃO zere o banner aqui: ele é usado para feedback (Correto!/Errado!) e também para
                // o ItemsManager/ShopManager mostrarem seleção/quantidade.
                // Se quiser limpar, faça explicitamente a partir do fluxo de UI.
                SetActive(shopPanel, true);
                SetActive(shopModelObject, true);
                SetActive(challengeButtonObject, true);
                break;

            case GameState.Shop:
                // Mesmo comportamento da Intermission.
                // NÃO zere o banner aqui (mesmo motivo do Intermission)
                SetActive(shopPanel, true);
                SetActive(shopModelObject, true);
                SetActive(challengeButtonObject, true);

                break;

            case GameState.Victory:
                SetMessage("Vitória!");
                GameSession.Instance.StopRun();
                SceneManager.LoadScene(gameOverSceneIndex);
                break;

            case GameState.GameOver:
                SetMessage("Game Over!");
                GameSession.Instance.StopRun();
                SceneManager.LoadScene(gameOverSceneIndex);
                break;
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (playerHpText != null) playerHpText.text = $"HP:{playerHp:D3}";
        if (coinsText != null) coinsText.text = $"$:{money}";

        RefreshTopLabelUI();
    }

    /// <summary>
    /// O texto de cima (enemyNameText/enemyHpText) funciona como "cabeçalho".
    /// - Em batalha: mostra o inimigo (nome + HP)
    /// - Na lojinha/intervalo: mostra "Lojinha" e limpa o HP
    /// </summary>
    private void RefreshTopLabelUI()
    {
        // Lojinha / Intermission: cabeçalho fixo
        if (state == GameState.Shop || state == GameState.Intermission)
        {
            if (enemyNameText != null) enemyNameText.text = "Lojinha";
            // Wendell: na lojinha também queremos ver o HP do inimigo como 000
            if (enemyHpText != null) enemyHpText.text = "HP:000";
            return;
        }

        // Batalha / outros estados: mostra inimigo se existir
        if (enemyTracked)
        {
            if (enemyNameText != null) enemyNameText.text = currentEnemyId.ToString();
            if (enemyHpText != null) enemyHpText.text = $"HP:{currentEnemyHp:D3}";
        }
        else
        {
            SetEnemyUnknownUI();
        }
    }

    private void SetEnemyUnknownUI()
    {
        if (enemyNameText != null) enemyNameText.text = "Inimigo";
        if (enemyHpText != null) enemyHpText.text = "HP:000";
    }

    private void SetMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = ToSentenceCasePtBr(msg);
    }

    // Nome amigável para mostrar no banner (evita aparecer "Water" etc.)
    private string GetDisplayName(ActionId id)
    {
        switch (id)
        {
            case ActionId.Apple: return "Maçã";
            case ActionId.Bread: return "Pão";
            case ActionId.Escape: return "Escape";
            case ActionId.Water: return "Água";
            case ActionId.Fire: return "Fogo";
            case ActionId.Rock: return "Pedra";
            case ActionId.Thunder: return "Raio";
            case ActionId.Coin: return "Moeda";
            case ActionId.Coins: return "Moedas";
            case ActionId.MultiCoins: return "Multi Moedas";
            default: return id.ToString();
        }
    }

    private string ToSentenceCasePtBr(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        char first = char.ToUpper(s[0]);
        if (s.Length == 1) return first.ToString();
        return first + s.Substring(1);
    }

    private void SetActive(GameObject go, bool value)
    {
        if (go != null) go.SetActive(value);
    }

    // =========================
    // ENEMY DATA
    // =========================
    private void LoadEnemy(EnemyId id)
    {
        currentEnemyId = id;

        switch (id)
        {
            case EnemyId.Goblin:
                currentEnemyMaxHp = 30;
                currentEnemyAttackDamage = 5;
                break;
            case EnemyId.Orc:
                currentEnemyMaxHp = 70;
                currentEnemyAttackDamage = 8;
                break;
            case EnemyId.Minotaur:
                currentEnemyMaxHp = 100;
                currentEnemyAttackDamage = 10;
                break;
            case EnemyId.Dragon:
                currentEnemyMaxHp = 200;
                currentEnemyAttackDamage = 30;
                break;
        }

        currentEnemyHp = currentEnemyMaxHp;
    }

    // =========================
    // TRACKING HOOKS (Vuforia)
    // =========================
    public void OnEnemyTracked(EnemyId enemy)
    {
        if (!CanAcceptEnemyTracking()) return;

        if (Time.time - lastEnemyTrackTime < trackDebounceSeconds) return;
        lastEnemyTrackTime = Time.time;

        if (enemyLocked && enemyTracked && enemy != currentEnemyId)
            return;

        if (!enemyTracked)
        {
            LoadEnemy(enemy);
            enemyTracked = true;

            // novo inimigo = novo turno
            attackPressedThisTurn = false;
            actionTracked = false;

            // reseta guards
            resolvingTurn = false;
            usePressedLock = false;
            StopEnemyAttackRoutineIfAny();

            if (lockEnemyUntilBattleEnds)
                enemyLocked = true;
        }

        EnterState(GameState.BattleReady);
    }

    public void OnEnemyTrackingLost()
    {
        if (enemyLocked) return;

        enemyTracked = false;

        if (state != GameState.Shop && state != GameState.Intermission && state != GameState.Quiz && state != GameState.EnemyTurn)
            EnterState(GameState.AwaitEnemyTracking);
    }

    // Você pode trocar de carta livremente: sempre atualiza selectedAction
    public void OnActionTracked(ActionId action)
    {
        actionTrackedNow = true;
        lastTrackedActionId = action;
        if (!CanAcceptActionTracking()) return;

        if (Time.time - lastActionTrackTime < trackDebounceSeconds) return;
        lastActionTrackTime = Time.time;

        // =========================
        // INTERMISSION: capturar item/moeda apresentado na câmera para "Use" (ItemsManager)
        // =========================
        if (state == GameState.Intermission || state == GameState.Shop)
        {
            if (!IsIntermissionUsable(action))
            {
                SetMessage("Inválido");
                return;
            }

            if (enforceInventory && GetCardCount(action) <= 0)
            {
                SetMessage($"Sem {action}");
                return;
            }

            itemsManager?.OnIntermissionUseTargetTracked(action);
            return;
        }

        // =========================
        // BATALHA: não aceitar moedas como carta de ação
        // =========================
        if (IsCurrency(action))
        {
            SetMessage("Inválido");
            return;
        }

        if (enforceInventory && GetCardCount(action) <= 0)
        {
            SetMessage($"Sem {action}");
            return;
        }

        actionTracked = true;
        selectedAction = action;

        EnterState(GameState.CardReady);
    }

    public void OnActionTrackingLost()
    {
        actionTrackedNow = false;
        lastTrackedActionId = ActionId.None;
        if (state == GameState.Intermission || state == GameState.Shop)
        {
            itemsManager?.OnIntermissionUseTargetLost();
            return;
        }

        if (state == GameState.Quiz || state == GameState.EnemyTurn) return;

        EnterState(attackPressedThisTurn ? GameState.AwaitActionTracking : GameState.BattleReady);
    }

    // =========================
    // BUTTONS
    // =========================
    

// =========================
// UI BUTTON DELAY (GLOBAL)
// =========================
private bool uiDelayBusy = false;

private IEnumerator DelayedUIButton(System.Action action)
{
    if (uiDelayBusy) yield break;
    uiDelayBusy = true;

    yield return new WaitForSecondsRealtime(uiButtonDelay);

    action?.Invoke();
    uiDelayBusy = false;
}

public void OnChallengePressed()
    {
        StartCoroutine(DelayedUIButton(OnChallengePressed_Internal));
    }

    private void OnChallengePressed_Internal()
    {
        
        if (shopManager != null)
            shopManager.CancelPendingPurchase();

        enemyTracked = false;
        enemyLocked = false;
        attackPressedThisTurn = false;
        actionTracked = false;

        resolvingTurn = false;
        usePressedLock = false;
        StopEnemyAttackRoutineIfAny();

        EnterState(GameState.AwaitEnemyTracking);
    
    }


    public void OnAttackPressed()
    {
        StartCoroutine(DelayedUIButton(OnAttackPressed_Internal));
    }

    private void OnAttackPressed_Internal()
    {
        if (state != GameState.BattleReady) return;
        if (!enemyTracked)
        {
            EnterState(GameState.AwaitEnemyTracking);
            return;
        }

        // Fecha overlays (Store/Bag) para não sobrepor a batalha
        shopManager?.ForceCloseStore();
        itemsManager?.ForceCloseBag();

        attackPressedThisTurn = true;
        actionTracked = false;

        resolvingTurn = false;
        usePressedLock = false;
        StopEnemyAttackRoutineIfAny();

        EnterState(GameState.AwaitActionTracking);
    
    }


    public void OnPassTurnPressed()
    {
        StartCoroutine(DelayedUIButton(OnPassTurnPressed_Internal));
    }

    private void OnPassTurnPressed_Internal()
    {
        if (!enemyTracked) return;

        // Pass deve funcionar em:
        // - BattleReady (passa sem atacar)
        // - AwaitActionTracking (desiste de escolher carta)
        // - CardReady (cancelar carta e passar)
        if (state != GameState.BattleReady &&
            state != GameState.AwaitActionTracking &&
            state != GameState.CardReady)
            return;

        // cancela carta se tinha
        actionTracked = false;

        // turno do inimigo
        EnterState(GameState.EnemyTurn);
        StartEnemyAttackAfterDelay(endTurnAfter: true);
    
    }


    public void OnUsePressed()
    {
        StartCoroutine(DelayedUIButton(OnUsePressed_Internal));
    }

    private void OnUsePressed_Internal()
    {
        if (state != GameState.CardReady) return;
        if (!enemyTracked)
        {
            EnterState(GameState.AwaitEnemyTracking);
            return;
        }
        if (!actionTracked)
        {
            EnterState(GameState.AwaitActionTracking);
            return;
        }


        // Fecha overlays (Store/Bag) antes do Quiz
        shopManager?.ForceCloseStore();
        itemsManager?.ForceCloseBag();
        // trava clique duplo
        if (usePressedLock) return;
        usePressedLock = true;

        StartQuiz(selectedAction);
    
    }


    // =========================
    // SHOP: USAR MOEDAS (sem inimigo)
    // =========================
    /// <summary>
    /// Inicia o quiz para converter cartas de moeda em dinheiro, enquanto estiver na lojinha.
    /// Você chama isso a partir do UI (por exemplo, ao apertar "Use" depois de selecionar a moeda e a quantidade).
    /// </summary>
    public void BeginShopCurrencyQuiz(ActionId currencyId, int qty)
    {
                if (!IsCurrency(currencyId)) return;
        if (qty <= 0) return;

        int have = GetCardCount(currencyId);
        if (have < qty)
        {
            SetMessage($"Sem {currencyId}");
            return;
        }

        // arma o modo "moeda na lojinha" e reaproveita o mesmo quiz de Libras
        shopCurrencyMode = true;
        pendingCurrencyId = currencyId;
        pendingCurrencyQty = qty;


        shopReturnState = state;
        // Fecha overlays (Store/Bag) antes do Quiz
        shopManager?.ForceCloseStore();
        itemsManager?.ForceCloseBag();

        // usa selectedAction para o LibrasQuestionManager saber qual carta é
        selectedAction = currencyId;
        StartQuiz(currencyId);
    }

    /// <summary>
    /// Usa itens de cura (Apple/Bread) dentro da lojinha, exigindo o mesmo quiz de Libras.
    /// </summary>
    public void BeginShopItemQuiz(ActionId itemId, int qty = 1)
    {
        if (state != GameState.Shop) return;
        if (itemId != ActionId.Apple && itemId != ActionId.Bread) return;
        if (qty <= 0) return;

        int have = GetCardCount(itemId);
        if (have < qty)
        {
            SetMessage($"Sem {itemId}");
            return;
        }

        shopItemMode = true;
        pendingShopItemId = itemId;
        pendingShopItemQty = qty;


        shopReturnState = state;
        // Fecha overlays (Store/Bag) antes do Quiz
        shopManager?.ForceCloseStore();
        itemsManager?.ForceCloseBag();

        selectedAction = itemId;
        StartQuiz(itemId);
    }

    // =========================
    // INTERMISSION: USAR ITENS/MOEDAS (Apple/Bread/Coin/Coins/MultiCoins)
    // =========================
    public void BeginIntermissionUseQuiz(ActionId itemId, int qty)
    {
        if (state != GameState.Intermission) return;
        if (!IsIntermissionUsable(itemId)) return;
        if (qty <= 0) return;

        int have = GetCardCount(itemId);
        if (have < qty)
        {
            SetMessage($"Sem {itemId}");
            return;
        }

        intermissionUseMode = true;
        pendingIntermissionId = itemId;
        pendingIntermissionQty = qty;

        // Fecha overlays antes do Quiz
        shopManager?.ForceCloseStore();
        itemsManager?.ForceCloseBag();

        selectedAction = itemId;
        StartQuiz(itemId);
    }

    public void ChooseOptionA() => ResolveQuiz(choiceIsA: true);
    public void ChooseOptionB() => ResolveQuiz(choiceIsA: false);

    // =========================
    // QUIZ
    // =========================
    private void StartQuiz(ActionId action)
    {
        quizStartTime = Time.time;

        if (librasQuestionManager != null)
            correctIsA = librasQuestionManager.StartQuestion(action);
        else
            correctIsA = Random.value > 0.5f;

        EnterState(GameState.Quiz);
    }

    private void ResolveQuiz(bool choiceIsA)
    {
        if (state != GameState.Quiz) return;

        // impede resolver 2x
        if (resolvingTurn) return;
        resolvingTurn = true;

        // fecha quiz visual
        if (librasQuestionManager != null)
            librasQuestionManager.StopQuestion();

        bool correct = (choiceIsA == correctIsA);
        float answerTime = Time.time - quizStartTime;
        bool fastAnswer = answerTime <= fastAnswerSeconds;

        // IMPORTANTE:
        // Em Shop/Intermission nós voltamos de estado sem passar por EndTurn().
        // Então precisamos destravar guards aqui, senão o 2º uso trava (o que você reportou).
        void ResetQuizGuards()
        {
            resolvingTurn = false;
            usePressedLock = false;
        }

        // Se for uso de itens/moedas fora da batalha (Intermission ou Shop), não entra em EnemyTurn
        if (!shopCurrencyMode && !shopItemMode && !intermissionUseMode)
            EnterState(GameState.EnemyTurn);

        if (correct)
        {
            SetMessage("Correto!");
            // SFX: libras correct + card use
            sfxManager?.PlayLibrasResult(true);
            sfxManager?.PlayCardUse(selectedAction);
            // no shop (itens/moedas) não existe ataque do monstro
            monsterNextAttackCanMiss = (shopCurrencyMode || shopItemMode) ? false : fastAnswer;

            // =========================
            // SHOP: converter moedas em dinheiro
            // =========================
            if (shopCurrencyMode)
            {
                int qty = Mathf.Max(0, pendingCurrencyQty);
                var id = pendingCurrencyId;

                if (qty <= 0)
                {
                    shopCurrencyMode = false;
                    pendingCurrencyQty = 0;
                    ResetQuizGuards();
                    EnterState(shopReturnState);
                    return;
                }

                // consome as cartas
                if (enforceInventory && !ConsumeCard(id, qty))
                {
                    SetMessage($"Sem {id}");
                    shopCurrencyMode = false;
                    pendingCurrencyQty = 0;
                    ResetQuizGuards();
                    EnterState(shopReturnState);
                    return;
                }

                // converte para dinheiro
                int valuePerCard = (id == ActionId.Coin) ? 1 : (id == ActionId.Coins) ? 10 : 100;
                AddMoney(valuePerCard * qty);

                shopCurrencyMode = false;
                pendingCurrencyQty = 0;

                // volta para a lojinha (sem ataque)
                ResetQuizGuards();
                EnterState(shopReturnState);
                return;
            }

            // =========================
            // SHOP: usar Apple/Bread (cura) sem ataque
            // =========================
            if (shopItemMode)
            {
                int qty = Mathf.Max(0, pendingShopItemQty);
                var id = pendingShopItemId;

                if (qty <= 0)
                {
                    shopItemMode = false;
                    pendingShopItemQty = 0;
                    ResetQuizGuards();
                    EnterState(shopReturnState);
                    return;
                }

                if (enforceInventory && !ConsumeCard(id, qty))
                {
                    SetMessage($"Sem {id}");
                    shopItemMode = false;
                    pendingShopItemQty = 0;
                    ResetQuizGuards();
                    EnterState(shopReturnState);
                    return;
                }

                // aplica o efeito do item (cura)
                // Obs: ApplyAction já cuida de Apple/Bread
                for (int i = 0; i < qty; i++)
                    ApplyAction(id);

                shopItemMode = false;
                pendingShopItemQty = 0;

                ResetQuizGuards();
                EnterState(shopReturnState);
                return;
            }

            // =========================
            // INTERMISSION: usar Apple/Bread e converter moedas (sem ataque)
            // =========================
            if (intermissionUseMode)
            {
                int qty = Mathf.Max(0, pendingIntermissionQty);
                var id = pendingIntermissionId;

                if (qty <= 0)
                {
                    intermissionUseMode = false;
                    pendingIntermissionQty = 0;
                    ResetQuizGuards();
                    EnterState(GameState.Intermission);
                    itemsManager?.OnIntermissionUseResolved(success: true, id, 0);
                    return;
                }

                // consome as cartas
                if (enforceInventory && !ConsumeCard(id, qty))
                {
                    SetMessage($"Sem {id}");
                    intermissionUseMode = false;
                    pendingIntermissionQty = 0;
                    ResetQuizGuards();
                    EnterState(GameState.Intermission);
                    itemsManager?.OnIntermissionUseResolved(success: false, id, qty);
                    return;
                }

                if (IsCurrency(id))
                {
                    int valuePerCard = (id == ActionId.Coin) ? 1 : (id == ActionId.Coins) ? 10 : 100;
                    AddMoney(valuePerCard * qty);
                }
                else
                {
                    for (int i = 0; i < qty; i++)
                        ApplyAction(id);
                }

                intermissionUseMode = false;
                pendingIntermissionQty = 0;

                ResetQuizGuards();
                EnterState(GameState.Intermission);
                itemsManager?.OnIntermissionUseResolved(success: true, id, qty);
                return;
            }

            if (enforceInventory && !ConsumeCard(selectedAction, 1))
            {
                SetMessage($"Sem {selectedAction}");
                StartEnemyAttackAfterDelay(endTurnAfter: true);
                return;
            }

            ApplyAction(selectedAction);

            // Se a carta te levou para um estado fora da batalha, não continua o fluxo de turno.
            // (Ex.: Escape leva para Intermission; Shop/Victory/GameOver também interrompem o turno.)
            if (state == GameState.Shop || state == GameState.Intermission || state == GameState.Victory || state == GameState.GameOver)
                return;

            // Se inimigo ainda vivo, ele ataca. Se morreu, EndTurn (mas aqui geralmente vai pra loja)
            if (enemyTracked && currentEnemyHp > 0)
                StartEnemyAttackAfterDelay(endTurnAfter: true);
            else
                EndTurn();
        }
        else
        {
            SetMessage("Errado!");
            // SFX: libras wrong
            sfxManager?.PlayLibrasResult(false);
            monsterNextAttackCanMiss = false;

            if (shopCurrencyMode)
            {
                // errou na loja: só volta para Shop
                shopCurrencyMode = false;
                pendingCurrencyQty = 0;
                EnterState(shopReturnState);
                return;
            }

            if (shopItemMode)
            {
                // errou na loja: só volta para Shop
                shopItemMode = false;
                pendingShopItemQty = 0;
                EnterState(shopReturnState);
                return;
            }

            if (intermissionUseMode)
            {
                int qty = Mathf.Max(0, pendingIntermissionQty);
                var id = pendingIntermissionId;

                // Errou: perde as cartas e não aplica efeito
                if (qty > 0)
                {
                    if (enforceInventory)
                        ConsumeCard(id, qty);
                }

                intermissionUseMode = false;
                pendingIntermissionQty = 0;

                EnterState(GameState.Intermission);
                itemsManager?.OnIntermissionUseResolved(success: false, id, qty);
                return;
            }

            StartEnemyAttackAfterDelay(endTurnAfter: true);
        }
    }

    private void EndTurn()
    {
        // libera locks do turno
        resolvingTurn = false;
        usePressedLock = false;

        attackPressedThisTurn = false;
        actionTracked = false;

        if (enemyTracked)
            EnterState(GameState.BattleReady);
        else
            EnterState(GameState.AwaitEnemyTracking);
    }

    // =========================
    // ACTIONS
    // =========================
    private void ApplyAction(ActionId action)
    {
        switch (action)
        {
            case ActionId.Water: DealDamageToEnemy(7); break;
            case ActionId.Fire: DealDamageToEnemy(10); break;
            case ActionId.Rock: DealDamageToEnemy(6); break;
            case ActionId.Thunder: DealDamageToEnemy(8); break;

            case ActionId.Apple: HealPlayer(10); break;
            case ActionId.Bread: HealPlayer(5); break;

            case ActionId.Escape:
                EnterIntermissionAndReset();
                return;
        }

        RefreshUI();
    }

    private void DealDamageToEnemy(int dmg)
    {
        if (!enemyTracked) return;

        currentEnemyHp -= dmg;
        if (currentEnemyHp < 0) currentEnemyHp = 0;

        RefreshUI();

        if (currentEnemyHp <= 0)
        {
            // SFX: enemy defeated
            sfxManager?.PlayEnemyDefeated();
            if (GameSession.Instance != null && scoreByEnemy.TryGetValue(currentEnemyId, out int points))
                GameSession.Instance.AddScore(points);

            GiveMinCoinDrop(currentEnemyId);

            // Progresso: só conta se o inimigo foi derrotado (Escape não conta)
            if (GameProgressManager.Instance != null)
            {
                bool triggeredVictory = GameProgressManager.Instance.RegisterEnemyDefeated(currentEnemyId);
                if (triggeredVictory) return;
            }

            EnterIntermissionAndReset();
        }
    }

    private void HealPlayer(int amount)
    {
        playerHp = Mathf.Clamp(playerHp + amount, 0, playerMaxHp);
        RefreshUI();
    }

    // =========================
    // ENEMY ATTACK (SAFE)
    // =========================
    private void StartEnemyAttackAfterDelay(bool endTurnAfter)
    {
        StopEnemyAttackRoutineIfAny();
        enemyAttackRoutine = StartCoroutine(EnemyAttackAfterDelay(endTurnAfter));
    }

    private void StopEnemyAttackRoutineIfAny()
    {
        if (enemyAttackRoutine != null)
        {
            StopCoroutine(enemyAttackRoutine);
            enemyAttackRoutine = null;
        }
    }

    private IEnumerator EnemyAttackAfterDelay(bool endTurnAfter)
    {
        yield return new WaitForSeconds(enemyAttackDelaySeconds);

        enemyAttackRoutine = null;

        if (!enemyTracked) yield break;

        // Em Intermission/Shop/Quiz não ataca. (EnemyTurn é o estado correto pra atacar!)
        if (state == GameState.Intermission || state == GameState.Shop || state == GameState.Quiz) yield break;

        if (monsterNextAttackCanMiss && Random.value < monsterMissChanceIfFast)
        {
            SetMessage("Falhou!");
            monsterNextAttackCanMiss = false;

            if (endTurnAfter)
                EndTurn();

            yield break;
        }

        monsterNextAttackCanMiss = false;

        // SFX: enemy attack
        sfxManager?.PlayEnemyAttack(currentEnemyId);
        playerHp -= currentEnemyAttackDamage;
        if (playerHp < 0) playerHp = 0;

        RefreshUI();

        if (playerHp <= 0)
        {
            // SFX: player defeated
            sfxManager?.PlayPlayerDefeated();
            EnterState(GameState.GameOver);
            yield break;
        }

        if (endTurnAfter)
            EndTurn();
    }

    private void EnterIntermissionAndReset()
    {
        enemyTracked = false;
        enemyLocked = false;
        attackPressedThisTurn = false;
        actionTracked = false;

        resolvingTurn = false;
        usePressedLock = false;
        StopEnemyAttackRoutineIfAny();
        // SFX: stop enemy idle loop
        sfxManager?.StopEnemyIdleLoop();
        sfxManager?.ResetDragonMusicTrigger();
        // SFX: permitir tocar "Enemy Appear" de novo no próximo combate
        sfxManager?.ResetEnemyAppearMemory();

        EnterState(GameState.Intermission);
    }

    private bool IsCurrency(ActionId id)
        => id == ActionId.Coin || id == ActionId.Coins || id == ActionId.MultiCoins;

    bool IsShopUsable(ActionId id)
        => id == ActionId.Apple || id == ActionId.Bread || IsCurrency(id);

    bool IsIntermissionUsable(ActionId id)
        => id == ActionId.Apple || id == ActionId.Bread || IsCurrency(id);

    // =========================
    // DROPS (mínimos)
    // =========================
    private void GiveMinCoinDrop(EnemyId enemy)
    {
        switch (enemy)
        {
            case EnemyId.Goblin:
                AddCard(ActionId.Coin, 5);
                AddCard(ActionId.Coins, 3);
                break;

            case EnemyId.Orc:
                AddCard(ActionId.Coin, 5);
                AddCard(ActionId.Coins, 7);
                break;

            case EnemyId.Minotaur:
                AddCard(ActionId.Coin, 5);
                AddCard(ActionId.Coins, 2);
                AddCard(ActionId.MultiCoins, 1);
                break;

            case EnemyId.Dragon:
                AddCard(ActionId.Coin, 10);
                AddCard(ActionId.Coins, 4);
                // FIX: Dragão dá 3 MultiCoins
                AddCard(ActionId.MultiCoins, 3);
                break;
        }
    }

    // =========================
    // PUBLIC API (ShopManager)
    // =========================
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (money < amount) return false;
        money -= amount;
        RefreshUI();
        return true;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        money += amount;
        RefreshUI();
    }

    public int GetMoney() => money;

    public void AddCard(ActionId id, int qty = 1)
    {
        if (qty <= 0) return;
        if (!inventory.ContainsKey(id)) inventory[id] = 0;
        inventory[id] += qty;
    }

    public int GetCardCount(ActionId id)
        => inventory.TryGetValue(id, out int v) ? v : 0;

    public bool ConsumeCard(ActionId id, int qty = 1)
    {
        if (qty <= 0) return true;
        int have = GetCardCount(id);
        if (have < qty) return false;
        inventory[id] = have - qty;
        return true;
    }

    // =========================
    // HELPERS PÚBLICOS (ShopManager)
    // =========================
    public void SetShopMessage(string msg) => SetMessage(msg);
    public void RefreshUI_Public() => RefreshUI();

    // === FIX: Explicitly enter Shop state when store opens ===
    public void EnterShop()
    {
        EnterState(GameState.Shop);
    }
}