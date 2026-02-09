using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class LibrasQuestionManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button buttonA;
    [SerializeField] private Button buttonB;
    [SerializeField] private RawImage rawImageA;
    [SerializeField] private RawImage rawImageB;

    [Header("Video Players (cada um com sua RenderTexture)")]
    [SerializeField] private VideoPlayer videoA; // TargetTexture = RT_A (1080x1920)
    [SerializeField] private VideoPlayer videoB; // TargetTexture = RT_B (1080x1920)

    [Header("UI - Button Delay")]
    [SerializeField] private float uiButtonDelay = 0.2f;

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [System.Serializable]
    public class ActionVideoEntry
    {
        public GameManager.ActionId action;
        public VideoClip correctClip;
        public VideoClip[] wrongPool; // falsos possíveis para esse action
    }

    [Header("Action -> Clips (setar no Inspector)")]
    [SerializeField] private ActionVideoEntry[] entries;

    // Opcional: você disse que Coin/Coins/MultiCoins são “mesmo objeto”
    // Então você pode apontar todos eles para o mesmo entry (ou usar o mesmo pool).
    // Se quiser forçar isso por código:
    [Header("Treat these as the same pool (optional)")]
    [SerializeField] private bool unifyCoinPools = true;

    private Dictionary<GameManager.ActionId, ActionVideoEntry> _map;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        BuildMap();
    }

    private void OnEnable() => HookButtons();
    private void OnDisable() => UnhookButtons();

    private void BuildMap()
    {
        _map = new Dictionary<GameManager.ActionId, ActionVideoEntry>();
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null) continue;
            _map[e.action] = e;
        }

        // Se você quiser que Coin/Coins/MultiCoins usem exatamente o mesmo cadastro:
        if (unifyCoinPools)
        {
            if (_map.TryGetValue(GameManager.ActionId.Coin, out var coinEntry))
            {
                _map[GameManager.ActionId.Coins] = coinEntry;
                _map[GameManager.ActionId.MultiCoins] = coinEntry;
            }
        }
    }

    private void HookButtons()
    {
        if (buttonA) buttonA.onClick.AddListener(OnButtonA);
        if (buttonB) buttonB.onClick.AddListener(OnButtonB);

        RemoveClick(rawImageA);
        RemoveClick(rawImageB);

        AddClick(rawImageA, OnClickA);
        AddClick(rawImageB, OnClickB);
    }

    private void UnhookButtons()
    {
        if (buttonA) buttonA.onClick.RemoveListener(OnButtonA);
        if (buttonB) buttonB.onClick.RemoveListener(OnButtonB);

        RemoveClick(rawImageA);
        RemoveClick(rawImageB);
    }

    private void OnButtonA() => StartCoroutine(DelayedAnswer(true));
    private void OnButtonB() => StartCoroutine(DelayedAnswer(false));
    private void OnClickA() => OnAnswer(true);
    private void OnClickB() => OnAnswer(false);

    private IEnumerator DelayedAnswer(bool isA)
    {
        yield return new WaitForSecondsRealtime(uiButtonDelay);
        OnAnswer(isA);
    }

    private void OnAnswer(bool isA)
    {
        if (gameManager == null) return;

        if (isA) gameManager.ChooseOptionA();
        else     gameManager.ChooseOptionB();
    }

    private void AddClick(RawImage img, UnityEngine.Events.UnityAction action)
    {
        if (img == null) return;

        var trigger = img.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = img.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(_ => action.Invoke());
        trigger.triggers.Add(entry);
    }

    private void RemoveClick(RawImage img)
    {
        if (img == null) return;
        var trigger = img.GetComponent<EventTrigger>();
        if (trigger == null) return;
        trigger.triggers.Clear();
    }

    // =========================
    // PUBLIC API (CALLED BY GAMEMANAGER)
    // =========================
    public bool StartQuestion(GameManager.ActionId action)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (_map == null || _map.Count == 0) BuildMap();

        // Decide aleatoriamente qual lado é o correto
        bool correctIsA = Random.value > 0.5f;

        // Pega entry do action
        if (!_map.TryGetValue(action, out var entry) || entry.correctClip == null)
        {
            // fallback: não quebra o fluxo do GameManager
            return correctIsA;
        }

        VideoClip correct = entry.correctClip;
        VideoClip wrong = PickWrong(entry, correct);

        // Configura A/B
        VideoClip clipA = correctIsA ? correct : wrong;
        VideoClip clipB = correctIsA ? wrong : correct;

        // Toca de verdade (Prepare + Play)
        StartCoroutine(PlayVideo(videoA, clipA));
        StartCoroutine(PlayVideo(videoB, clipB));

        return correctIsA;
    }

    private VideoClip PickWrong(ActionVideoEntry entry, VideoClip correct)
    {
        if (entry.wrongPool == null || entry.wrongPool.Length == 0)
            return null;

        // tenta evitar escolher o mesmo do correto (caso esteja no pool)
        for (int i = 0; i < 10; i++)
        {
            var pick = entry.wrongPool[Random.Range(0, entry.wrongPool.Length)];
            if (pick != null && pick != correct) return pick;
        }

        return entry.wrongPool[0];
    }

    private IEnumerator PlayVideo(VideoPlayer vp, VideoClip clip)
    {
        if (vp == null) yield break;

        vp.Stop();
        vp.clip = clip;

        // se não tiver clip, limpa a RT e sai
        if (vp.clip == null) yield break;

        vp.waitForFirstFrame = true;
        vp.isLooping = true; // se você não quiser loop, mude pra false

        bool prepared = false;
        vp.prepareCompleted += OnPrepared;
        vp.Prepare();

        float timeout = 3f;
        while (!prepared && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        vp.prepareCompleted -= OnPrepared;
        vp.Play();

        void OnPrepared(VideoPlayer _) => prepared = true;
    }

    public void StopQuestion()
    {
        if (videoA) videoA.Stop();
        if (videoB) videoB.Stop();

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}