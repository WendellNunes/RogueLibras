using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
public class ItemsManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;


    // Configura o delay padrão dos botões (ajustável no Inspector)
    [Header("UI - Button Delay")]
    [SerializeField] private float uiButtonDelay = 0.2f;
    [Header("Bag Button / Items Panel")]
    [Tooltip("Botão 'Bag' que abre/fecha a mochila.")]
    [SerializeField] private Button bagButton;

    [Tooltip("Painel raiz da mochila (tudo que deve aparecer/sumir quando abrir/fechar).")]
    [SerializeField] private GameObject bagPanel;

    [Tooltip("Se marcado, ao abrir a Bag ela vem para frente (sobrepõe Store etc.).\nEm AR isso costuma TAPAR o modelo 3D/carta. Recomendo deixar DESMARCADO.")]
    [SerializeField] private bool bringToFrontOnOpen = false;


    // Sons do botão Bag ao abrir/fechar (caso particular)
    [Header("Bag Button SFX")]
    [SerializeField] private AudioClip bagOpenSfx;
    [SerializeField] private AudioClip bagCloseSfx;
    [Range(0f, 1f)]
    [SerializeField] private float bagSfxVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float lockedAlpha = 0.35f;

    private CanvasGroup bagCanvasGroup;
    private bool bagLocked = false;

// =========================
// INTERMISSION: Use/Cancel + Quantidade (Apple/Bread/Coin/Coins/MultiCoins)
// =========================
[Header("Intermission - Use/Cancel + Quantity (assign in inspector)")]
[SerializeField] private Button useButton;
[SerializeField] private Button cancelButton;

[Tooltip("RIGHT: aumenta a quantidade (x1 -> x2 -> x3...), limitada ao que você tem na Bag.")]
[SerializeField] private Button rightQtyButton;

[Tooltip("LEFT: diminui a quantidade (x3 -> x2 -> x1).")]
[SerializeField] private Button leftQtyButton;
private bool hasPendingIntermission;
private GameManager.ActionId pendingIntermissionId;
private int selectedQty = 1;

    [Header("Bag Pages (optional)")]
    [Tooltip("Root da Página 1 (itens básicos + Coin). Se não usar páginas, deixe nulo.")]
    [SerializeField] private GameObject page1Root;

    [Tooltip("Root da Página 2 (Coins + MultiCoins). Se não usar páginas, deixe nulo.")]
    [SerializeField] private GameObject page2Root;

    [Tooltip("Seta para ir para a página anterior (Arrow Left).")]
    [SerializeField] private Button pageLeftButton;

    [Tooltip("Seta para ir para a próxima página (Arrow Right).")]
    [SerializeField] private Button pageRightButton;

    private int currentPage = 0; // 0 = page1, 1 = page2

    [Header("UI - Item Buttons (optional)")]
    [Tooltip("Botões das cartas dentro da Bag. Se você não usar algum, pode deixar nulo.")]
    [SerializeField] private Button itemEscapeButton;
    [SerializeField] private Button itemWaterButton;
    [SerializeField] private Button itemFireButton;
    [SerializeField] private Button itemRockButton;
    [SerializeField] private Button itemThunderButton;
    [SerializeField] private Button itemAppleButton;
    [SerializeField] private Button itemBreadButton;

    [Header("UI - Currency Buttons (optional)")]
    [SerializeField] private Button itemCoinButton;       // página 1
    [SerializeField] private Button itemCoinsButton;      // página 2
    [SerializeField] private Button itemMultiCoinsButton; // página 2

    [Header("UI - Summary Text (optional)")]
    [Tooltip("Texto grande (TextMeshProUGUI) listando quantas cartas você tem.")]
    [SerializeField] private TextMeshProUGUI inventorySummaryText;

    [Header("UI - Per Item Count Texts (optional)")]
    [Tooltip("Textos pequenos tipo 'x3' ao lado de cada item. Se não tiver, deixe nulo.")]
    [SerializeField] private TextMeshProUGUI countEscapeText;
    [SerializeField] private TextMeshProUGUI countWaterText;
    [SerializeField] private TextMeshProUGUI countFireText;
    [SerializeField] private TextMeshProUGUI countRockText;
    [SerializeField] private TextMeshProUGUI countThunderText;
    [SerializeField] private TextMeshProUGUI countAppleText;
    [SerializeField] private TextMeshProUGUI countBreadText;

    [Header("UI - Currency Count Texts (optional)")]
    [SerializeField] private TextMeshProUGUI countCoinText;
    [SerializeField] private TextMeshProUGUI countCoinsText;
    [SerializeField] private TextMeshProUGUI countMultiCoinsText;

    [Header("UI - Optional Selected Text")]
    [Tooltip("Opcional: texto que mostra qual item você clicou por último dentro da Bag.")]
    [SerializeField] private TextMeshProUGUI selectedItemText;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (bagButton != null)
        {
            bagCanvasGroup = bagButton.GetComponent<CanvasGroup>();
            if (bagCanvasGroup == null)
                bagCanvasGroup = bagButton.gameObject.AddComponent<CanvasGroup>();

            bagButton.onClick.RemoveListener(OnBagButtonClicked);
            bagButton.onClick.AddListener(OnBagButtonClicked);
        }

        if (bagPanel != null)
            bagPanel.SetActive(false);

        // paginação (se estiver usando)
        if (pageLeftButton) { pageLeftButton.onClick.RemoveAllListeners(); pageLeftButton.onClick.AddListener(() => RunButtonDelayed(PrevPage)); }
        if (pageRightButton) { pageRightButton.onClick.RemoveAllListeners(); pageRightButton.onClick.AddListener(() => RunButtonDelayed(NextPage)); }
        ApplyPageVisibility(0);

// Intermission Use UI
if (useButton != null)
{
    useButton.onClick.RemoveAllListeners();
    useButton.onClick.AddListener(() => RunButtonDelayed(OnClickUse));
}
if (cancelButton != null)
{
    cancelButton.onClick.RemoveAllListeners();
    cancelButton.onClick.AddListener(() => RunButtonDelayed(OnClickCancel));
}
if (rightQtyButton != null)
{
    rightQtyButton.onClick.RemoveAllListeners();
    rightQtyButton.onClick.AddListener(() => RunButtonDelayed(OnClickQtyUp));
}
if (leftQtyButton != null)
{
    leftQtyButton.onClick.RemoveAllListeners();
    leftQtyButton.onClick.AddListener(() => RunButtonDelayed(OnClickQtyDown));
}

        SetBagLocked(false);
        RefreshBagUI();
    }

    private void OnEnable()
    {
if (itemEscapeButton) itemEscapeButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Escape)));
        if (itemWaterButton) itemWaterButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Water)));
        if (itemFireButton) itemFireButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Fire)));
        if (itemRockButton) itemRockButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Rock)));
        if (itemThunderButton) itemThunderButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Thunder)));
        if (itemAppleButton) itemAppleButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Apple)));
        if (itemBreadButton) itemBreadButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Bread)));

        // moedas
        if (itemCoinButton) itemCoinButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Coin)));
        if (itemCoinsButton) itemCoinsButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.Coins)));
        if (itemMultiCoinsButton) itemMultiCoinsButton.onClick.AddListener(() => RunButtonDelayed(() => OnClickItem(GameManager.ActionId.MultiCoins)));

        if (pageLeftButton) { pageLeftButton.onClick.RemoveAllListeners(); pageLeftButton.onClick.AddListener(() => RunButtonDelayed(PrevPage)); }
        if (pageRightButton) { pageRightButton.onClick.RemoveAllListeners(); pageRightButton.onClick.AddListener(() => RunButtonDelayed(NextPage)); }

        RefreshBagUI();
    }

    private void OnDisable()
    {
        if (itemEscapeButton) itemEscapeButton.onClick.RemoveAllListeners();
        if (itemWaterButton) itemWaterButton.onClick.RemoveAllListeners();
        if (itemFireButton) itemFireButton.onClick.RemoveAllListeners();
        if (itemRockButton) itemRockButton.onClick.RemoveAllListeners();
        if (itemThunderButton) itemThunderButton.onClick.RemoveAllListeners();
        if (itemAppleButton) itemAppleButton.onClick.RemoveAllListeners();
        if (itemBreadButton) itemBreadButton.onClick.RemoveAllListeners();

        if (itemCoinButton) itemCoinButton.onClick.RemoveAllListeners();
        if (itemCoinsButton) itemCoinsButton.onClick.RemoveAllListeners();
        if (itemMultiCoinsButton) itemMultiCoinsButton.onClick.RemoveAllListeners();

        if (pageLeftButton) pageLeftButton.onClick.RemoveAllListeners();
        if (pageRightButton) pageRightButton.onClick.RemoveAllListeners();

        if (bagButton) bagButton.onClick.RemoveAllListeners();
        if (bagButton) bagButton.onClick.AddListener(OnBagButtonClicked);
    }

    // =========================
    // BAG BUTTON (OPEN/CLOSE)
    // =========================
// Botão Bag com delay e SFX diferente para abrir/fechar
    private void OnBagButtonClicked()
    {
        if (bagPanel == null) return;
        bool willOpen = !bagPanel.activeSelf;
        PlayBagToggleSfx(willOpen);
        RunButtonDelayed(ToggleBag);
    }

    public void SetBagLocked(bool locked)
    {
        // "locked" aqui significa: bloquear o USO EMPILHADO/Intermission (Use/Cancel/Quantidade),
        // NÃO bloquear a visualização da Bag. O jogador pode abrir a Bag em qualquer estado para consultar itens.
        bagLocked = locked;

        // Mantém o botão da Bag sempre utilizável (apenas consulta).
        if (bagButton != null)
            bagButton.interactable = true;

        // Se você quiser "dim" a bag inteira fora da Intermission, mantenha a alpha,
        // mas não bloqueie raycasts para permitir navegação/consulta.
        if (bagCanvasGroup != null)
        {
            bagCanvasGroup.alpha = 1f; // Bag button should never be dimmed/transparent
            bagCanvasGroup.blocksRaycasts = true;
        }

        // Controles específicos do uso em Intermission (Use/Cancel/Qty) são ligados/desligados abaixo
        RefreshIntermissionUseUIInteractable();
    }

    public void ToggleBag()
    {
        if (bagPanel == null) return;

        bool willOpen = !bagPanel.activeSelf;
        bagPanel.SetActive(willOpen);

        if (willOpen)
        {
            // Se em algum momento ativamos a Bag em "modo reduzido" (workaround),
            // aqui garantimos que o conteúdo volte a aparecer quando o jogador abre manualmente.
            if (inventorySummaryText != null) inventorySummaryText.gameObject.SetActive(true);
            if (selectedItemText != null) selectedItemText.gameObject.SetActive(true);

            if (bringToFrontOnOpen)
                bagPanel.transform.SetAsLastSibling();

            // ao abrir, sempre começa na página 1 (se existir)
            currentPage = 0;
            ApplyPageVisibility(currentPage);

            RefreshBagUI();
        }
    }

    private void PrevPage()
    {
        if (page1Root == null || page2Root == null) return;
        currentPage = Mathf.Max(0, currentPage - 1);
        ApplyPageVisibility(currentPage);
        RefreshBagUI();
    }

    private void NextPage()
    {
        if (page1Root == null || page2Root == null) return;
        currentPage = Mathf.Min(1, currentPage + 1);
        ApplyPageVisibility(currentPage);
        RefreshBagUI();
    }

    private void ApplyPageVisibility(int page)
    {
        if (page1Root != null) page1Root.SetActive(page == 0);
        if (page2Root != null) page2Root.SetActive(page == 1);

        if (pageLeftButton) pageLeftButton.interactable = (page > 0);
        if (pageRightButton) pageRightButton.interactable = (page < 1);
    }

    // =========================
    // UI UPDATE
    // =========================
    public void RefreshBagUI()
    {
        if (gameManager == null) return;

        int escape = gameManager.GetCardCount(GameManager.ActionId.Escape);
        int water  = gameManager.GetCardCount(GameManager.ActionId.Water);
        int fire   = gameManager.GetCardCount(GameManager.ActionId.Fire);
        int rock   = gameManager.GetCardCount(GameManager.ActionId.Rock);
        int thunder= gameManager.GetCardCount(GameManager.ActionId.Thunder);
        int apple  = gameManager.GetCardCount(GameManager.ActionId.Apple);
        int bread  = gameManager.GetCardCount(GameManager.ActionId.Bread);

        int coin       = gameManager.GetCardCount(GameManager.ActionId.Coin);
        int coins      = gameManager.GetCardCount(GameManager.ActionId.Coins);
        int multiCoins = gameManager.GetCardCount(GameManager.ActionId.MultiCoins);


        if (inventorySummaryText != null)
        {
            inventorySummaryText.text =
                $"Escape x{escape}\n" +
                $"Water x{water}\n" +
                $"Fire x{fire}\n" +
                $"Rock x{rock}\n" +
                $"Thunder x{thunder}\n" +
                $"Apple x{apple}\n" +
                $"Bread x{bread}\n" +
                $"Coin x{coin}\n" +
                $"Coins x{coins}\n" +
                $"MultiCoins x{multiCoins}";
        }

        if (countEscapeText)  countEscapeText.text = $"x{escape}";
        if (countWaterText)   countWaterText.text = $"x{water}";
        if (countFireText)    countFireText.text = $"x{fire}";
        if (countRockText)    countRockText.text = $"x{rock}";
        if (countThunderText) countThunderText.text = $"x{thunder}";
        if (countAppleText)   countAppleText.text = $"x{apple}";
        if (countBreadText)   countBreadText.text = $"x{bread}";

        if (countCoinText)       countCoinText.text = $"x{coin}";
        if (countCoinsText)      countCoinsText.text = $"x{coins}";
        if (countMultiCoinsText) countMultiCoinsText.text = $"x{multiCoins}";

        SetInteractableAndDim(itemEscapeButton, escape);
        SetInteractableAndDim(itemWaterButton, water);
        SetInteractableAndDim(itemFireButton, fire);
        SetInteractableAndDim(itemRockButton, rock);
        SetInteractableAndDim(itemThunderButton, thunder);
        SetInteractableAndDim(itemAppleButton, apple);
        SetInteractableAndDim(itemBreadButton, bread);

        // moedas (página 1 e 2)
        SetInteractableAndDim(itemCoinButton, coin);
        SetInteractableAndDim(itemCoinsButton, coins);
        SetInteractableAndDim(itemMultiCoinsButton, multiCoins);

        // páginas
        if (page1Root != null && page2Root != null)
            ApplyPageVisibility(currentPage);
    }

    private void SetInteractableAndDim(Button btn, int count)
    {
        if (btn == null) return;
        bool has = (count > 0);
        btn.interactable = has;

        // deixa visualmente "apagado" quando não tem (sem depender do Transition do Unity)
        var cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = has ? 1f : lockedAlpha;
        cg.blocksRaycasts = has;
    }

// =========================
// INTERMISSION API (chamado pelo GameManager)
// =========================
public bool CanAcceptIntermissionUseTracking()
{
    // Só aceitamos rastreio quando o uso empilhado estiver liberado (Intermission)
    // e não estivermos no meio de um quiz (o GameManager já bloqueia Quiz/EnemyTurn).
    return !bagLocked;
}

public void OnIntermissionUseTargetTracked(GameManager.ActionId id)
{
    // chamado quando apresentar Apple/Bread/Coin/Coins/MultiCoins na câmera durante Intermission
    hasPendingIntermission = true;
    pendingIntermissionId = id;

    int have = gameManager != null ? gameManager.GetCardCount(id) : 0;
    selectedQty = Mathf.Clamp(selectedQty, 1, Mathf.Max(1, have));
    if (selectedQty <= 0) selectedQty = 1;

    // IMPORTANTE: NÃO abrir a Bag automaticamente ao rastrear uma carta.
    // A Bag deve abrir APENAS quando o jogador clicar no botão Bag/Itens.

    UpdatePendingTexts();
    RefreshIntermissionUseUIInteractable();
}

public void OnIntermissionUseTargetLost()
{
    // regra conservadora: perdeu o target, cancelamos o pending para evitar "Use" sem carta visível.
    ClearPendingIntermission();
}

public void OnIntermissionUseResolved(bool success, GameManager.ActionId id, int qty)
{
    // chamado pelo GameManager após o quiz do Use em Intermission.
    // Independente de sucesso/erro, o inventário pode ter mudado.
    RefreshBagUI();
    ClearPendingIntermission();
}

// =========================
// UI Handlers (Use/Cancel/Qty)
// =========================
private void OnClickUse()
{
    if (!hasPendingIntermission) return;
    if (gameManager == null) return;

    int have = gameManager.GetCardCount(pendingIntermissionId);
    int qty = Mathf.Clamp(selectedQty, 1, Mathf.Max(1, have));

    // Em Shop e Intermission usamos o mesmo fluxo de rastreio/quantidade,
    // mas o "Use" chama métodos diferentes no GameManager.
    var st = gameManager.CurrentState;

    if (st == GameManager.GameState.Shop)
    {
        // 5 cartas liberadas na lojinha: Apple, Bread, Coin, Coins, MultiCoins
        if (pendingIntermissionId == GameManager.ActionId.Apple || pendingIntermissionId == GameManager.ActionId.Bread)
            gameManager.BeginShopItemQuiz(pendingIntermissionId, qty);
        else
            gameManager.BeginShopCurrencyQuiz(pendingIntermissionId, qty);
        return;
    }

    if (st == GameManager.GameState.Intermission)
    {
        gameManager.BeginIntermissionUseQuiz(pendingIntermissionId, qty);
        return;
    }
}


private void OnClickCancel()
{
    ClearPendingIntermission();
}

private void OnClickQtyUp()
{
    if (bagLocked) return;
    if (!hasPendingIntermission) return;

    int have = gameManager != null ? gameManager.GetCardCount(pendingIntermissionId) : 0;
    selectedQty = Mathf.Clamp(selectedQty + 1, 1, Mathf.Max(1, have));
    UpdatePendingTexts();
    RefreshIntermissionUseUIInteractable();
}

private void OnClickQtyDown()
{
    if (bagLocked) return;
    if (!hasPendingIntermission) return;

    int have = gameManager != null ? gameManager.GetCardCount(pendingIntermissionId) : 0;
    selectedQty = Mathf.Clamp(selectedQty - 1, 1, Mathf.Max(1, have));
    UpdatePendingTexts();
    RefreshIntermissionUseUIInteractable();
}

private void ClearPendingIntermission()
{
    hasPendingIntermission = false;
    selectedQty = 1;
    UpdatePendingTexts();
    RefreshIntermissionUseUIInteractable();
}

private void UpdatePendingTexts()
{
    // Wendell: não precisamos de qtyText/pendingItemText aqui.
    // A mensagem padrão (banner) do GameManager é quem mostra: "Apple x1", "Coin x2", etc.
    if (gameManager == null) return;

    if (!hasPendingIntermission)
    {
        // Não forçamos limpar para não apagar mensagens importantes (Correto!/Errado!/Encontrado).
        return;
    }

    string display = GetDisplayName(pendingIntermissionId);
    gameManager.SetShopMessage($"{display} x{selectedQty}");
}


private void SetIntermissionControlsVisible(bool visible)
{
    // Liga/desliga APENAS os controles que o Wendell quer ver: Use/Cancel + setas.
    // NÃO usamos qtyText nem pendingItemText (o GameManager.messageText já mostra "Item xN").
    if (useButton != null) useButton.gameObject.SetActive(visible);
    if (cancelButton != null) cancelButton.gameObject.SetActive(visible);
    if (rightQtyButton != null) rightQtyButton.gameObject.SetActive(visible);
    if (leftQtyButton != null) leftQtyButton.gameObject.SetActive(visible);
}

private void RefreshIntermissionUseUIInteractable()
{
    // Mostra/oculta completamente os controles de "Use em quantidade" fora da Intermission
    bool inIntermission = !bagLocked;

    // Se o prefab foi montado com Use/Cancel dentro do mesmo painel da Bag,
    // esses botões NÃO aparecem quando a Bag está fechada. Detectamos isso e avisamos.
    // (Ideal: colocar o bloco Use/Cancel/Qty fora do bagPanel.)
    if (inIntermission && hasPendingIntermission && bagPanel != null && !bagPanel.activeSelf)
    {
        bool controlsUnderBag = false;
        if (useButton != null && useButton.transform.IsChildOf(bagPanel.transform)) controlsUnderBag = true;
        if (cancelButton != null && cancelButton.transform.IsChildOf(bagPanel.transform)) controlsUnderBag = true;
        if (rightQtyButton != null && rightQtyButton.transform.IsChildOf(bagPanel.transform)) controlsUnderBag = true;
        if (leftQtyButton != null && leftQtyButton.transform.IsChildOf(bagPanel.transform)) controlsUnderBag = true;
        if (controlsUnderBag)
        {
            Debug.LogWarning("[ItemsManager] Use/Cancel/Qty estão dentro do bagPanel. Para aparecer sem abrir a Bag, mova esse bloco para fora do painel da mochila. (Workaround: ativando bagPanel em modo reduzido.)");

            // Workaround: ativa a Bag para liberar os botões, mas esconde o conteúdo (páginas/sumário)
            bagPanel.SetActive(true);
            if (page1Root != null) page1Root.SetActive(false);
            if (page2Root != null) page2Root.SetActive(false);
            if (inventorySummaryText != null) inventorySummaryText.gameObject.SetActive(false);
            if (selectedItemText != null) selectedItemText.gameObject.SetActive(false);
        }
    }
    // Regra do Wendell: quando NÃO há carta rastreada, a Bag pode estar aberta para consulta,
    // mas os controles (Use/Cancelar/Setas) não devem aparecer nem ficar "transparentes".
    SetIntermissionControlsVisible(inIntermission && hasPendingIntermission);

    // habilita/desabilita controles dependendo do estado e do estoque
    // inIntermission já definido acima
    bool canUse = inIntermission && hasPendingIntermission;

    int have = (canUse && gameManager != null) ? gameManager.GetCardCount(pendingIntermissionId) : 0;
    int qtyMax = Mathf.Max(1, have);

    if (!canUse) selectedQty = 1;
    else selectedQty = Mathf.Clamp(selectedQty, 1, qtyMax);

    if (useButton != null) useButton.interactable = canUse && have > 0;
    if (cancelButton != null) cancelButton.interactable = canUse;

    if (rightQtyButton != null) rightQtyButton.interactable = canUse && selectedQty < qtyMax;
    if (leftQtyButton != null) leftQtyButton.interactable = canUse && selectedQty > 1;

    // atualiza textos também
    UpdatePendingTexts();
}

    // Executa ação de botão após um delay
    private void RunButtonDelayed(Action action)
    {
        if (action == null) return;
        StartCoroutine(RunButtonDelayedRoutine(action));
    }

    private IEnumerator RunButtonDelayedRoutine(Action action)
    {
        yield return new WaitForSecondsRealtime(uiButtonDelay);
        action.Invoke();
    }

    // Toca SFX do botão Bag ao abrir/fechar
    private void PlayBagToggleSfx(bool willOpen)
    {
        var clip = willOpen ? bagOpenSfx : bagCloseSfx;
        if (clip == null) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip, bagSfxVolume);
        else
            Debug.LogWarning("[ItemsManager] AudioManager.Instance não encontrado. SFX do Bag não tocou.");
    }

// =========================
// Helpers
// =========================
private string GetDisplayName(GameManager.ActionId id)
{
    switch (id)
    {
        case GameManager.ActionId.Apple: return "Apple";
        case GameManager.ActionId.Bread: return "Bread";
        case GameManager.ActionId.Escape: return "Escape";
        case GameManager.ActionId.Water: return "Water";
        case GameManager.ActionId.Fire: return "Fire";
        case GameManager.ActionId.Rock: return "Rock";
        case GameManager.ActionId.Thunder: return "Thunder";
        case GameManager.ActionId.Coin: return "Coin";
        case GameManager.ActionId.Coins: return "Coins";
        case GameManager.ActionId.MultiCoins: return "MultiCoins";
        default: return id.ToString();
    }
}

    private void OnClickItem(GameManager.ActionId id)
    {

        int count = gameManager != null ? gameManager.GetCardCount(id) : 0;

        if (selectedItemText != null)
            selectedItemText.text = $"{GetDisplayName(id)}  (x{count})";

        RefreshBagUI();
    }

    public void NotifyInventoryChanged()
    {
        RefreshBagUI();
    }

    public void CloseBag()
    {
        if (bagPanel != null)
            bagPanel.SetActive(false);
    }

    // Alias usado pelo GameManager para garantir que o overlay suma antes de Attack/Use/Quiz
    public void ForceCloseBag() => CloseBag();
}
