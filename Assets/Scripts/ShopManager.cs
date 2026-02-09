using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;
using System.Collections;

public class ShopManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Store Button / Shop Panel")]
    [Tooltip("Botão 'Store' que abre/fecha a loja.")]
    [SerializeField] private Button storeButton;

    [Tooltip("Painel raiz da loja (tudo que deve aparecer/sumir quando abrir/fechar).")]
    [SerializeField] private GameObject shopPanel;

    // SFX do botão Store (abrir/fechar)
    [Header("Store Button SFX")]
    [SerializeField] private AudioClip storeOpenSfx;
    [SerializeField] private AudioClip storeCloseSfx;

    [Range(0f, 1f)]
    [SerializeField] private float storeSfxVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float lockedAlpha = 0.35f;


    // Delay padrão antes de executar ações de botões
    [Header("UI - Button Delay")]
    [SerializeField] private float uiButtonDelay = 0.2f;
    private CanvasGroup storeCanvasGroup;
    private bool storeLocked = true;

    // =========================
    // SHOP USE MODE (Apple/Bread/Coin/Coins/MultiCoins)
    // =========================
    private bool shopUseModeActive = false;
    private GameManager.ActionId pendingUseId;
    private int pendingUseQty = 0;

    [Header("UI - Buttons")]
    [SerializeField] private Button buyButton;

    [Tooltip("Botão 'Cancel' que cancela todas as seleções antes do Buy. Só aparece quando há itens selecionados.")]
    [SerializeField] private Button cancelButton;

    [Tooltip("Botão 'Battle/Challenge' (opcional). Fica visível quando NÃO há itens selecionados.")]
    [SerializeField] private Button battleButton;

    [Header("UI - Quantity Arrows")]
    [SerializeField] private Button arrowLeftButton;
    [SerializeField] private Button arrowRightButton;

    // Último item selecionado (as setas alteram apenas este)
    private GameManager.ActionId? lastSelectedId = null;

    [Header("UI - Select Buttons")]
    [SerializeField] private Button selectEscapeButton;
    [SerializeField] private Button selectWaterButton;
    [SerializeField] private Button selectFireButton;
    [SerializeField] private Button selectRockButton;
    [SerializeField] private Button selectThunderButton;
    [SerializeField] private Button selectAppleButton;
    [SerializeField] private Button selectBreadButton;

    [Header("UI - Optional texts")]
    [SerializeField] private TextMeshProUGUI selectedText;
    [SerializeField] private TextMeshProUGUI totalText;

    [Header("Stock UI (optional)")]
    [SerializeField] private TextMeshProUGUI stockEscapeText;
    [SerializeField] private TextMeshProUGUI stockWaterText;
    [SerializeField] private TextMeshProUGUI stockFireText;
    [SerializeField] private TextMeshProUGUI stockRockText;
    [SerializeField] private TextMeshProUGUI stockThunderText;
    [SerializeField] private TextMeshProUGUI stockAppleText;
    [SerializeField] private TextMeshProUGUI stockBreadText;

    [Header("Selection Visual")]
    [Tooltip("Cor para cartas selecionadas (hex #666666). Alpha 255 = totalmente opaco.")]
    [SerializeField] private Color selectedColor = new Color32(0x66, 0x66, 0x66, 0xFF);

    // PRICES
    private const int PRICE_ESCAPE = 100;
    private const int PRICE_ATTACKS = 50;
    private const int PRICE_APPLE = 30;
    private const int PRICE_BREAD = 15;

    private const int INITIAL_STOCK = 5;

    private readonly Dictionary<GameManager.ActionId, int> stock = new Dictionary<GameManager.ActionId, int>();

    // Carrinho (seleções)
    private readonly Dictionary<GameManager.ActionId, int> cart = new Dictionary<GameManager.ActionId, int>();

    // Quanto já foi “reservado” (descontado) enquanto seleciona
    private int reservedTotal = 0;

    // Mapa ActionId -> Botão da UI (para manter o visual selecionado em múltiplas cartas)
    private readonly Dictionary<GameManager.ActionId, Button> actionButtons = new Dictionary<GameManager.ActionId, Button>();

    // Guardar a cor original do targetGraphic de cada botão, para restaurar no cancel/compra
    private readonly Dictionary<Button, Color> originalButtonColors = new Dictionary<Button, Color>();

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        // Configura o botão Store (abre/fecha o shopPanel)
        if (storeButton != null)
        {
            storeCanvasGroup = storeButton.GetComponent<CanvasGroup>();
            if (storeCanvasGroup == null)
                storeCanvasGroup = storeButton.gameObject.AddComponent<CanvasGroup>();

            storeButton.onClick.RemoveListener(OnStoreButtonClicked);
            storeButton.onClick.AddListener(OnStoreButtonClicked);
        }

        // Por segurança, começa com a loja fechada
        if (shopPanel != null)
            shopPanel.SetActive(false);

        // Por padrão, começa travado (ex.: durante batalha)
        SetStoreLocked(true);

        // Heurística para evitar "não aparece nada" quando alguém esquece de atribuir os textos.
        if (shopPanel != null)
        {
            if (selectedText == null || totalText == null)
            {
                var tmps = shopPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    var n = t.gameObject.name.ToLowerInvariant();
                    if (selectedText == null)
                    {
                        // NÃO pode pegar textos de botões (ex: Cancelar) por engano
                        if (n.Contains("cancel") || n.Contains("cancelar") || n.Contains("buy") || n.Contains("comprar"))
                            continue;
                        if (n.Contains("selected") || n.Contains("sele") || n.Contains("selectedtext") || n.Contains("item"))
                            selectedText = t;
                    }
                    if (totalText == null)
                    {
                        if (n.Contains("cancel") || n.Contains("cancelar"))
                            continue;
                        if (n.Contains("total") || n.Contains("price") || n.Contains("valor"))
                            totalText = t;
                    }
                }
            }
        }

        InitStock();
        BuildActionButtonMap();
    }

    private void OnEnable()
    {
        if (selectEscapeButton)  selectEscapeButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Escape)));
        if (selectWaterButton)   selectWaterButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Water)));
        if (selectFireButton)    selectFireButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Fire)));
        if (selectRockButton)    selectRockButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Rock)));
        if (selectThunderButton) selectThunderButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Thunder)));
        if (selectAppleButton)   selectAppleButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Apple)));
        if (selectBreadButton)   selectBreadButton.onClick.AddListener(() => RunButtonDelayed(() => ToggleSelect(GameManager.ActionId.Bread)));

        if (buyButton)    buyButton.onClick.AddListener(() => RunButtonDelayed(ConfirmBuyCart));
        if (cancelButton) cancelButton.onClick.AddListener(() => RunButtonDelayed(CancelAllSelections));

        if (arrowLeftButton)  arrowLeftButton.onClick.AddListener(() => RunButtonDelayed(DecreaseSelectedQty));
        if (arrowRightButton) arrowRightButton.onClick.AddListener(() => RunButtonDelayed(IncreaseSelectedQty));

        RefreshStockUI();
        RefreshCartUI();
        RefreshArrowButtons();
        RefreshBuyCancelBattleVisibility();
        RefreshAllSelectedVisuals();
    }

private void OnDisable()
    {
        if (selectEscapeButton) selectEscapeButton.onClick.RemoveAllListeners();
        if (selectWaterButton) selectWaterButton.onClick.RemoveAllListeners();
        if (selectFireButton) selectFireButton.onClick.RemoveAllListeners();
        if (selectRockButton) selectRockButton.onClick.RemoveAllListeners();
        if (selectThunderButton) selectThunderButton.onClick.RemoveAllListeners();
        if (selectAppleButton) selectAppleButton.onClick.RemoveAllListeners();
        if (selectBreadButton) selectBreadButton.onClick.RemoveAllListeners();

        if (buyButton) buyButton.onClick.RemoveAllListeners();
        if (cancelButton) cancelButton.onClick.RemoveAllListeners();

        if (arrowLeftButton) arrowLeftButton.onClick.RemoveAllListeners();
        if (arrowRightButton) arrowRightButton.onClick.RemoveAllListeners();

        // Segurança: se painel desabilitar sem confirmar, devolve
        CancelPendingPurchase();
    }

    private void BuildActionButtonMap()
    {
        actionButtons.Clear();
        if (selectEscapeButton) actionButtons[GameManager.ActionId.Escape] = selectEscapeButton;
        if (selectWaterButton) actionButtons[GameManager.ActionId.Water] = selectWaterButton;
        if (selectFireButton) actionButtons[GameManager.ActionId.Fire] = selectFireButton;
        if (selectRockButton) actionButtons[GameManager.ActionId.Rock] = selectRockButton;
        if (selectThunderButton) actionButtons[GameManager.ActionId.Thunder] = selectThunderButton;
        if (selectAppleButton) actionButtons[GameManager.ActionId.Apple] = selectAppleButton;
        if (selectBreadButton) actionButtons[GameManager.ActionId.Bread] = selectBreadButton;

        // captura cores originais
        foreach (var kv in actionButtons)
        {
            CacheOriginalButtonColor(kv.Value);
        }
    }

    private void CacheOriginalButtonColor(Button b)
    {
        if (b == null) return;
        var g = b.targetGraphic;
        if (g == null) return;
        if (!originalButtonColors.ContainsKey(b))
            originalButtonColors[b] = g.color;
    }

    private void SetButtonSelectedVisual(GameManager.ActionId id, bool selected)
    {
        if (!actionButtons.TryGetValue(id, out var b) || b == null) return;
        CacheOriginalButtonColor(b);

        var g = b.targetGraphic;
        if (g == null) return;

        g.color = selected ? selectedColor : originalButtonColors[b];
    }

    private void RefreshAllSelectedVisuals()
    {
        // Primeiro restaura tudo
        foreach (var kv in actionButtons)
        {
            SetButtonSelectedVisual(kv.Key, false);
        }

        // Depois aplica selected em tudo que está no carrinho
        foreach (var kv in cart)
        {
            SetButtonSelectedVisual(kv.Key, kv.Value > 0);
        }

        // IMPORTANTE:
        // O "modo de uso" (apresentar Apple/Bread/Coin/Coins/MultiCoins para aparecer o botão USE)
        // NÃO deve alterar o visual de seleção das cartas da loja.
        // Isso evita a sensação de que a carta foi "auto selecionada" para compra.
    }

    private void RefreshBuyCancelBattleVisibility()
    {
        // MODO DE USO: sempre mostra Use/Cancel e esconde Battle
        if (shopUseModeActive)
        {
            if (buyButton) buyButton.gameObject.SetActive(true);
            if (cancelButton) cancelButton.gameObject.SetActive(true);
            if (battleButton) battleButton.gameObject.SetActive(false);
            return;
        }

        bool hasSelection = cart.Count > 0;

        if (buyButton) buyButton.gameObject.SetActive(hasSelection);
        if (cancelButton) cancelButton.gameObject.SetActive(hasSelection);

        // battle/challenge só aparece se NÃO houver seleção (opcional)
        if (battleButton) battleButton.gameObject.SetActive(!hasSelection);
    }

    // =========================
    // STORE BUTTON (OPEN/CLOSE)
    // =========================
    // Clique do botão Store com SFX específico e delay
    private void OnStoreButtonClicked()
    {
        if (storeLocked) return;
        if (shopPanel == null) return;

        bool willOpen = !shopPanel.activeSelf;
        PlayStoreSfx(willOpen);
        RunButtonDelayed(ToggleStore);
    }

    // Toca o SFX de abrir/fechar do botão Store
    private void PlayStoreSfx(bool willOpen)
    {
        var clip = willOpen ? storeOpenSfx : storeCloseSfx;
        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip, storeSfxVolume);
            return;
        }

        if (storeButton == null) return;
        var src = storeButton.GetComponent<AudioSource>();
        if (src == null) src = storeButton.gameObject.AddComponent<AudioSource>();
        src.PlayOneShot(clip, storeSfxVolume);
    }

    public void SetStoreLocked(bool locked)
    {
        storeLocked = locked;

        if (storeButton != null)
            storeButton.interactable = !locked;

        if (storeCanvasGroup != null)
        {
            storeCanvasGroup.alpha = locked ? lockedAlpha : 1f;
            storeCanvasGroup.blocksRaycasts = !locked;
        }

        if (locked)
        {
            if (shopPanel != null)
                shopPanel.SetActive(false);

            // se o jogador estava com itens reservados, devolve
            CancelPendingPurchase();
        }
    }

    public void ToggleStore()
    {
        if (storeLocked) return;
        if (shopPanel == null) return;

        bool willOpen = !shopPanel.activeSelf;

        if (willOpen)
            shopPanel.transform.SetAsLastSibling();

        shopPanel.SetActive(willOpen);

        if (willOpen && gameManager != null)
            gameManager.EnterShop();

        if (!willOpen)
            CancelPendingPurchase();
    }

    private void InitStock()
    {
        stock.Clear();
        stock[GameManager.ActionId.Escape] = INITIAL_STOCK;
        stock[GameManager.ActionId.Water] = INITIAL_STOCK;
        stock[GameManager.ActionId.Fire] = INITIAL_STOCK;
        stock[GameManager.ActionId.Rock] = INITIAL_STOCK;
        stock[GameManager.ActionId.Thunder] = INITIAL_STOCK;
        stock[GameManager.ActionId.Apple] = INITIAL_STOCK;
        stock[GameManager.ActionId.Bread] = INITIAL_STOCK;
    }

    // =========================
    // SELECT (define último item selecionado; adiciona 1x se ainda não estiver no carrinho)
    // =========================
    private void ToggleSelect(GameManager.ActionId id)
    {
        if (gameManager == null) return;

        // sempre que clicar num item, ele vira o "último selecionado"
        lastSelectedId = id;

        int stockQty = stock.TryGetValue(id, out int s) ? s : 0;
        int currentInCart = cart.TryGetValue(id, out int c) ? c : 0;
        int price = GetPrice(id);

        // Se já está selecionado, apenas foca nele (não remove automaticamente).
        // Para diminuir, use a seta esquerda.
        if (currentInCart > 0)
        {
            UpdateShopMessageForSelection(id);
            RefreshCartUI();
            RefreshArrowButtons();
            RefreshBuyCancelBattleVisibility();
            // visual já deve estar aplicado; mas garante
            SetButtonSelectedVisual(id, true);
            return;
        }

        if (stockQty <= 0)
        {
            gameManager.SetShopMessage("0x");
            RefreshArrowButtons();
            return;
        }

        if (!gameManager.TrySpend(price))
        {
            // sem dinheiro para 1 unidade
            gameManager.SetShopMessage("Sem $");
            RefreshArrowButtons();
            return;
        }

        reservedTotal += price;
        cart[id] = 1;

        // visual persistente
        SetButtonSelectedVisual(id, true);

        UpdateShopMessageForSelection(id);
        RefreshCartUI();
        RefreshArrowButtons();
        RefreshBuyCancelBattleVisibility();
    }

    // =========================
    // QUANTITY ARROWS
    // =========================
    private void IncreaseSelectedQty()
    {
        if (gameManager == null) return;

        if (shopUseModeActive)
        {
            AdjustPendingUseQty(1);
            return;
        }
        if (lastSelectedId == null) { RefreshArrowButtons(); return; }

        var id = lastSelectedId.Value;

        int current = cart.TryGetValue(id, out int c) ? c : 0;
        if (current <= 0) { RefreshArrowButtons(); return; }

        int haveStock = stock.TryGetValue(id, out int s) ? s : 0;
        if (current >= haveStock)
        {
            RefreshArrowButtons();
            return;
        }

        int price = GetPrice(id);

        if (!gameManager.TrySpend(price))
        {
            RefreshArrowButtons();
            return;
        }

        cart[id] = current + 1;
        reservedTotal += price;

        // visual continua selecionado
        SetButtonSelectedVisual(id, true);

        UpdateShopMessageForSelection(id);
        RefreshCartUI();
        RefreshArrowButtons();
        RefreshBuyCancelBattleVisibility();
    }

    private void DecreaseSelectedQty()
    {
        if (gameManager == null) return;

        if (shopUseModeActive)
        {
            AdjustPendingUseQty(-1);
            return;
        }
        if (lastSelectedId == null) { RefreshArrowButtons(); return; }

        var id = lastSelectedId.Value;

        int current = cart.TryGetValue(id, out int c) ? c : 0;
        if (current <= 0) { RefreshArrowButtons(); return; }

        int price = GetPrice(id);

        // ✅ Se está em 1x e apertou Left → cancela (vira 0x)
        if (current == 1)
        {
            cart.Remove(id);
            reservedTotal -= price;
            gameManager.AddMoney(price);

            // remove visual
            SetButtonSelectedVisual(id, false);

            gameManager.SetShopMessage("Cancelado");

            // Se não tem mais nada no carrinho, limpa seleção
            if (cart.Count == 0)
                lastSelectedId = null;

            RefreshCartUI();
            RefreshArrowButtons();
            RefreshBuyCancelBattleVisibility();
            return;
        }

        // ✅ Se está >=2, apenas diminui
        cart[id] = current - 1;
        reservedTotal -= price;
        gameManager.AddMoney(price);

        UpdateShopMessageForSelection(id);
        RefreshCartUI();
        RefreshArrowButtons();
        RefreshBuyCancelBattleVisibility();
    }

    private void UpdateShopMessageForSelection(GameManager.ActionId id)
    {
        int qty = cart.TryGetValue(id, out int c) ? c : 0;
        if (qty < 0) qty = 0;

        // Ex.: "Apple 1x"
        gameManager.SetShopMessage($"{id} {qty}x");
    }

    private void RefreshArrowButtons()
    {
        if (arrowLeftButton == null && arrowRightButton == null)
            return;

        // =========================
        // MODO DE USO: setas ajustam quantidade do item apresentado na câmera
        // =========================
        if (shopUseModeActive && gameManager != null)
        {
            int have = gameManager.GetCardCount(pendingUseId);
            bool canDec = pendingUseQty > 1;
            bool canInc = pendingUseQty < have;

            if (arrowLeftButton) arrowLeftButton.interactable = canDec;
            if (arrowRightButton) arrowRightButton.interactable = canInc;
            return;
        }

        // =========================
        // MODO DE COMPRA: setas ajustam a última carta selecionada no carrinho
        // =========================
        int current = 0;

        bool hasSelection =
            lastSelectedId != null &&
            cart.TryGetValue(lastSelectedId.Value, out current) &&
            current > 0;

        if (!hasSelection)
        {
            if (arrowLeftButton) arrowLeftButton.interactable = false;
            if (arrowRightButton) arrowRightButton.interactable = false;
            return;
        }

        var id = lastSelectedId.Value;

        // limites: mínimo 1, máximo estoque e dinheiro disponível (via reservedTotal)
        int haveStock = stock.TryGetValue(id, out int s) ? s : 0;

        if (arrowLeftButton) arrowLeftButton.interactable = (current > 1);
        if (arrowRightButton) arrowRightButton.interactable = (current < haveStock);
    }

    // =========================
    // BUY CONFIRM
    // =========================
    private void ConfirmBuyCart()
    {
        if (gameManager == null) return;

        // =========================
        // MODO DE USO (educativo)
        // =========================
        if (shopUseModeActive)
        {
            ConfirmShopUse();
            return;
        }


        if (cart.Count == 0)
        {
            gameManager.SetShopMessage("Selecione");
            return;
        }

        // Valida estoque de tudo no carrinho (proteção)
        foreach (var kv in cart)
        {
            var id = kv.Key;
            int qtyWanted = kv.Value;

            int haveStock = stock.TryGetValue(id, out int s) ? s : 0;
            if (haveStock < qtyWanted)
            {
                gameManager.SetShopMessage("0x");
                return;
            }
        }

        // Entrega cartas e debita estoque
        foreach (var kv in cart)
        {
            var id = kv.Key;
            int qty = kv.Value;

            stock[id] -= qty;
            gameManager.AddCard(id, qty);
        }

        // Compra confirmada
        cart.Clear();
        reservedTotal = 0;
        lastSelectedId = null;

        // visual: nada selecionado após compra (opcional, mas faz sentido)
        RefreshAllSelectedVisuals();
        RefreshBuyCancelBattleVisibility();

        gameManager.SetShopMessage("$$$");
        RefreshStockUI();
        RefreshCartUI();
        gameManager.RefreshUI_Public();
        var itemsManager = FindObjectOfType<ItemsManager>();
        if (itemsManager != null)
            itemsManager.NotifyInventoryChanged();
    }

    // =========================
    // CANCEL / REFUND
    // =========================
    public void CancelPendingPurchase()
    {
        if (gameManager == null) return;
        if (reservedTotal <= 0 && cart.Count == 0) return;

        // devolve tudo reservado
        if (reservedTotal > 0)
            gameManager.AddMoney(reservedTotal);

        reservedTotal = 0;
        cart.Clear();
        lastSelectedId = null;

        // visual / botões
        RefreshAllSelectedVisuals();
        RefreshBuyCancelBattleVisibility();

        RefreshCartUI();
        RefreshArrowButtons();
    }

    // =========================
    // EXTERNAL: fechar overlay imediatamente (bugfix de sobreposição)
    // =========================
    public void ForceCloseStore()
    {
        // fecha UI
        if (shopPanel != null)
            shopPanel.SetActive(false);

        // limpa qualquer compra pendente
        CancelPendingPurchase();

        // cancela também um possível "modo de uso"
        CancelShopUseMode();
    }

    /// <summary>
    /// Permite rastrear itens na câmera enquanto estiver na lojinha.
    /// Para evitar conflito, só aceita quando NÃO há carrinho selecionado.
    /// </summary>
    public bool CanAcceptShopUseTracking()
    {
        if (storeLocked) return false;

        // precisa estar com o painel da loja aberto (senão o usuário nem está "na lojinha")
        if (shopPanel != null && !shopPanel.activeSelf) return false;

        // não misturar compra com uso educativo
        if (cart.Count > 0) return false;

        return true;
    }

    public void OnShopUseTargetTracked(GameManager.ActionId id)
    {
        if (!CanAcceptShopUseTracking()) return;

        // Só itens permitidos
        if (id != GameManager.ActionId.Apple &&
            id != GameManager.ActionId.Bread &&
            id != GameManager.ActionId.Coin &&
            id != GameManager.ActionId.Coins &&
            id != GameManager.ActionId.MultiCoins)
            return;

        int have = (gameManager != null) ? gameManager.GetCardCount(id) : 0;
        if (have <= 0) return;

        shopUseModeActive = true;
        pendingUseId = id;

        // padrão: moeda começa selecionando "tudo", comida começa em 1
        pendingUseQty = (id == GameManager.ActionId.Apple || id == GameManager.ActionId.Bread) ? 1 : have;
        pendingUseQty = Mathf.Clamp(pendingUseQty, 1, have);

        // Reaproveita UI existente: Buy vira "Use" e Cancel vira "Cancel"
        RefreshCartUI();
        RefreshArrowButtons();
        RefreshBuyCancelBattleVisibility();
        RefreshAllSelectedVisuals();
    }

    public void OnShopUseTargetLost()
    {
        // Por padrão, NÃO cancelamos automaticamente ao perder o target,
        // porque o usuário pode tirar a carta da câmera e ainda querer clicar "Use".
        // Se quiser cancelar automaticamente, descomente a linha abaixo.
        // CancelShopUseMode();
    }



    /// <summary>
    /// Botão CANCEL: cancela tudo que estiver selecionado (antes do Buy).
    /// Obs.: depois do Buy o carrinho já zera, então não tem como "voltar atrás".
    /// </summary>
    private void CancelAllSelections()
    {
        if (shopUseModeActive)
        {
            CancelShopUseMode();
            if (gameManager != null)
                gameManager.SetShopMessage("Cancelado");
            return;
        }

        CancelPendingPurchase();
        if (gameManager != null)
            gameManager.SetShopMessage("Cancelado");
    }

    private int GetPrice(GameManager.ActionId id)
    {
        switch (id)
        {
            case GameManager.ActionId.Escape: return PRICE_ESCAPE;
            case GameManager.ActionId.Apple: return PRICE_APPLE;
            case GameManager.ActionId.Bread: return PRICE_BREAD;
            default: return PRICE_ATTACKS;
        }
    }

    // =========================
    // UI
    // =========================
    private void RefreshCartUI()
    {
        // =========================
        // MODO DE USO (educativo)
        // =========================
        if (shopUseModeActive && gameManager != null)
        {
            int have = gameManager.GetCardCount(pendingUseId);

            if (selectedText != null)
                selectedText.text = $"Usar: {pendingUseId} x{pendingUseQty} (tem {have})";

            if (totalText != null)
            {
                if (pendingUseId == GameManager.ActionId.Coin ||
                    pendingUseId == GameManager.ActionId.Coins ||
                    pendingUseId == GameManager.ActionId.MultiCoins)
                {
                    int valuePer = (pendingUseId == GameManager.ActionId.Coin) ? 1 :
                                   (pendingUseId == GameManager.ActionId.Coins) ? 10 : 100;
                    totalText.text = $"Converter: +{valuePer * pendingUseQty} dinheiro";
                }
                else
                {
                    totalText.text = $"Cura: +{pendingUseQty} uso(s)";
                }
            }

            return;
        }

        // =========================
        // MODO DE COMPRA (carrinho)
        // =========================
        if (selectedText != null)
        {
            if (cart.Count == 0) selectedText.text = "Selecionado: -";
            else
            {
                string s = "Selecionado: ";
                bool first = true;
                foreach (var kv in cart)
                {
                    if (!first) s += ", ";
                    s += $"{kv.Key} {kv.Value}x";
                    first = false;
                }
                selectedText.text = s;
            }
        }

        if (totalText != null)
        {
            if (reservedTotal <= 0) totalText.text = "Total: 0";
            else totalText.text = $"Total: {reservedTotal}";
        }
    }

    private void RefreshStockUI()
    {
        SetStock(stockEscapeText, GameManager.ActionId.Escape);
        SetStock(stockWaterText, GameManager.ActionId.Water);
        SetStock(stockFireText, GameManager.ActionId.Fire);
        SetStock(stockRockText, GameManager.ActionId.Rock);
        SetStock(stockThunderText, GameManager.ActionId.Thunder);
        SetStock(stockAppleText, GameManager.ActionId.Apple);
        SetStock(stockBreadText, GameManager.ActionId.Bread);

        // deixa carta "apagada" quando o estoque é 0
        ApplyStockVisual(selectEscapeButton, GameManager.ActionId.Escape);
        ApplyStockVisual(selectWaterButton, GameManager.ActionId.Water);
        ApplyStockVisual(selectFireButton, GameManager.ActionId.Fire);
        ApplyStockVisual(selectRockButton, GameManager.ActionId.Rock);
        ApplyStockVisual(selectThunderButton, GameManager.ActionId.Thunder);
        ApplyStockVisual(selectAppleButton, GameManager.ActionId.Apple);
        ApplyStockVisual(selectBreadButton, GameManager.ActionId.Bread);
    }

    private void ApplyStockVisual(Button btn, GameManager.ActionId id)
    {
        if (btn == null) return;
        int v = stock.TryGetValue(id, out int qty) ? qty : 0;
        bool has = v > 0;
        btn.interactable = has;

        var cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = has ? 1f : lockedAlpha;
        cg.blocksRaycasts = has;
    }

    private void SetStock(TextMeshProUGUI t, GameManager.ActionId id)
    {
        if (t == null) return;
        int v = stock.TryGetValue(id, out int qty) ? qty : 0;
        t.text = $"x{v}";
    }

    // =========================
    // SHOP USE HELPERS
    // =========================
    private void AdjustPendingUseQty(int delta)
    {
        if (gameManager == null) return;

        int have = gameManager.GetCardCount(pendingUseId);
        if (have <= 0)
        {
            CancelShopUseMode();
            return;
        }

        pendingUseQty = Mathf.Clamp(pendingUseQty + delta, 1, have);

        RefreshCartUI();
        RefreshArrowButtons();
    }

    private void ConfirmShopUse()
    {
        if (gameManager == null) return;

        int have = gameManager.GetCardCount(pendingUseId);
        if (have <= 0)
        {
            CancelShopUseMode();
            gameManager.SetShopMessage("Sem item");
            return;
        }

        pendingUseQty = Mathf.Clamp(pendingUseQty, 1, have);

        // Dispara o quiz no GameManager (ele já cuida de fechar overlays e voltar para Shop)
        if (pendingUseId == GameManager.ActionId.Coin ||
            pendingUseId == GameManager.ActionId.Coins ||
            pendingUseId == GameManager.ActionId.MultiCoins)
        {
            gameManager.BeginShopCurrencyQuiz(pendingUseId, pendingUseQty);
        }
        else
        {
            // Apple/Bread
            gameManager.BeginShopItemQuiz(pendingUseId, pendingUseQty);
        }

        // mantém o modo ativo? preferimos desligar aqui para o UI não ficar travado
        CancelShopUseMode(silent: true);
    }

    private void CancelShopUseMode(bool silent = false)
    {
        if (!shopUseModeActive) return;

        shopUseModeActive = false;
        pendingUseQty = 0;

        RefreshCartUI();
        RefreshArrowButtons();
        RefreshBuyCancelBattleVisibility();
        RefreshAllSelectedVisuals();

        if (!silent && gameManager != null)
            gameManager.SetShopMessage("Lojinha");
    }

    // Executa uma ação de botão após um pequeno delay configurável
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

}