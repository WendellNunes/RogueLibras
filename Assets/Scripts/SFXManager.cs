using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centraliza SFX do jogo (cartas, inimigos, Libras) com tudo serializável no Inspector.
/// Não muda regras do jogo: só toca sons nos eventos certos.
/// </summary>
public class SFXManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("Opcional: se você quiser que o SFXManager também toque música de boss (Dragon) sem depender do MusicController.")]
    [SerializeField] private AudioSource musicSource;

    [Header("Volumes")]
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 0.8f;

    [Header("Delays")]
    [Tooltip("Delay usado quando você quer garantir que um SFX importante tenha tempo de tocar antes de continuar (use via coroutines).")]
    [SerializeField] private float sfxDelay = 1.0f;

    // -------------------------
    // CARTAS (APARECER / USAR)
    // -------------------------
    [Header("Cards - Appear (Magic)")]
    [SerializeField] private AudioClip fireAppear;
    [SerializeField] private AudioClip waterAppear;
    [SerializeField] private AudioClip rockAppear;
    [SerializeField] private AudioClip thunderAppear;
    [SerializeField] private AudioClip escapeAppear;

    [Header("Cards - Use (Magic)")]
    [SerializeField] private AudioClip fireUse;
    [SerializeField] private AudioClip waterUse;
    [SerializeField] private AudioClip rockUse;
    [SerializeField] private AudioClip thunderUse;
    [SerializeField] private AudioClip escapeUse;

    [Header("Cards - Appear (Currency)")]
    [SerializeField] private AudioClip coinAppear;
    [SerializeField] private AudioClip coinsAppear;
    [SerializeField] private AudioClip multiCoinsAppear;

    [Header("Cards - Use (Currency)")]
    [SerializeField] private AudioClip coinUse;
    [SerializeField] private AudioClip coinsUse;
    [SerializeField] private AudioClip multiCoinsUse;

    [Header("Consumables - Use")]
    [SerializeField] private AudioClip appleUse;
    [SerializeField] private AudioClip breadUse;

    // -------------------------
    // INIMIGOS
    // -------------------------
    [Header("Enemies - Appear")]
    [SerializeField] private AudioClip goblinAppear;
    [SerializeField] private AudioClip orcAppear;
    [SerializeField] private AudioClip minotaurAppear;
    [SerializeField] private AudioClip dragonAppear;

    [Header("Enemies - Attack")]
    [SerializeField] private AudioClip goblinAttack;
    [SerializeField] private AudioClip orcAttack;
    [SerializeField] private AudioClip minotaurAttack;
    [SerializeField] private AudioClip dragonAttack;

    [Header("Enemies - Idle (Periodic Grunt)")]
    [Tooltip("Toca a cada ~1m30 enquanto o inimigo estiver ativo/rastreado.")]
    [SerializeField] private float enemyIdleIntervalSeconds = 90f;

    [SerializeField] private AudioClip goblinIdle;
    [SerializeField] private AudioClip orcIdle;
    [SerializeField] private AudioClip minotaurIdle;
    [SerializeField] private AudioClip dragonIdle;

    private Coroutine _enemyIdleRoutine;
    // Enemy appear: tocar 1x por combate (evita spam do tracking)
    private readonly HashSet<GameManager.EnemyId> _enemyAppearPlayed = new HashSet<GameManager.EnemyId>();


    // -------------------------
    // LIBRAS
    // -------------------------
    [Header("Libras - Result")]
    [SerializeField] private AudioClip librasCorrect;
    [SerializeField] private AudioClip librasWrong;

    // -------------------------
    // BATTLE (VICTORY / LOSE)
    // -------------------------
    [Header("Battle - Victory / Lose")]
    [SerializeField] private AudioClip enemyDefeated;
    [SerializeField] private AudioClip playerDefeated;

    // -------------------------
    // DRAGON (INTRO + BOSS MUSIC)
    // -------------------------
    [Header("Dragon - Intro & Boss Music (Optional)")]
    [SerializeField] private AudioClip dragonIntroSfx;
    [SerializeField] private AudioClip dragonBossMusic;
    [SerializeField] private float dragonIntroDelaySeconds = 1.0f;

    private bool _dragonBossMusicTriggered;

    private void Reset()
    {
        // tenta pegar o AudioSource do mesmo GO automaticamente
        if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
    }

    // =========================
    // PUBLIC API (GameManager)
    // =========================
    public void PlayCardAppear(GameManager.ActionId action)
    {
        // Apple/Bread: você pediu SEM som ao aparecer
        switch (action)
        {
            case GameManager.ActionId.Fire:      PlayOneShot(fireAppear); break;
            case GameManager.ActionId.Water:     PlayOneShot(waterAppear); break;
            case GameManager.ActionId.Rock:      PlayOneShot(rockAppear); break;
            case GameManager.ActionId.Thunder:   PlayOneShot(thunderAppear); break;
            case GameManager.ActionId.Escape:    PlayOneShot(escapeAppear); break;

            case GameManager.ActionId.Coin:       PlayOneShot(coinAppear); break;
            case GameManager.ActionId.Coins:      PlayOneShot(coinsAppear); break;
            case GameManager.ActionId.MultiCoins: PlayOneShot(multiCoinsAppear); break;
        }
    }

    public void PlayCardUse(GameManager.ActionId action)
    {
        switch (action)
        {
            case GameManager.ActionId.Fire:      PlayOneShot(fireUse); break;
            case GameManager.ActionId.Water:     PlayOneShot(waterUse); break;
            case GameManager.ActionId.Rock:      PlayOneShot(rockUse); break;
            case GameManager.ActionId.Thunder:   PlayOneShot(thunderUse); break;
            case GameManager.ActionId.Escape:    PlayOneShot(escapeUse); break;

            case GameManager.ActionId.Coin:       PlayOneShot(coinUse); break;
            case GameManager.ActionId.Coins:      PlayOneShot(coinsUse); break;
            case GameManager.ActionId.MultiCoins: PlayOneShot(multiCoinsUse); break;

            case GameManager.ActionId.Apple:     PlayOneShot(appleUse); break;
            case GameManager.ActionId.Bread:     PlayOneShot(breadUse); break;
        }
    }

    public void PlayEnemyAppear(GameManager.EnemyId enemy)
    {
        switch (enemy)
        {
            case GameManager.EnemyId.Goblin:   PlayOneShot(goblinAppear); break;
            case GameManager.EnemyId.Orc:      PlayOneShot(orcAppear); break;
            case GameManager.EnemyId.Minotaur: PlayOneShot(minotaurAppear); break;
            case GameManager.EnemyId.Dragon:   PlayOneShot(dragonAppear); break;
        }
    }

    // Enemy appear (1x por combate)
    public void PlayEnemyAppearOnce(GameManager.EnemyId enemy)
    {
        if (_enemyAppearPlayed.Contains(enemy)) return;
        _enemyAppearPlayed.Add(enemy);
        PlayEnemyAppear(enemy);
    }

    public void ResetEnemyAppearMemory()
    {
        _enemyAppearPlayed.Clear();
    }

    public void PlayEnemyAttack(GameManager.EnemyId enemy)
    {
        switch (enemy)
        {
            case GameManager.EnemyId.Goblin:   PlayOneShot(goblinAttack); break;
            case GameManager.EnemyId.Orc:      PlayOneShot(orcAttack); break;
            case GameManager.EnemyId.Minotaur: PlayOneShot(minotaurAttack); break;
            case GameManager.EnemyId.Dragon:   PlayOneShot(dragonAttack); break;
        }
    }

    public void StartEnemyIdleLoop(GameManager.EnemyId enemy)
    {
        StopEnemyIdleLoop();
        _enemyIdleRoutine = StartCoroutine(EnemyIdleLoop(enemy));
    }

    public void StopEnemyIdleLoop()
    {
        if (_enemyIdleRoutine != null)
        {
            StopCoroutine(_enemyIdleRoutine);
            _enemyIdleRoutine = null;
        }
    }

    public void PlayLibrasResult(bool correct)
    {
        PlayOneShot(correct ? librasCorrect : librasWrong);
    }

    // Battle result SFX
    public void PlayEnemyDefeated()
    {
        PlayOneShot(enemyDefeated);
    }

    public void PlayPlayerDefeated()
    {
        PlayOneShot(playerDefeated);
    }

    /// <summary>
    /// Opcional: intro do Dragão e depois música de boss (loop).
    /// Não depende do seu MusicController; usa musicSource se estiver setado.
    /// </summary>
    public void TriggerDragonBossSequence()
    {
        if (_dragonBossMusicTriggered) return;
        _dragonBossMusicTriggered = true;

        if (dragonIntroSfx == null && dragonBossMusic == null) return;
        StartCoroutine(DragonBossSequence());
    }

    public void ResetDragonBossSequence()
    {
        _dragonBossMusicTriggered = false;
        if (musicSource != null && musicSource.clip == dragonBossMusic)
            musicSource.Stop();
    }


    // Compat: nome antigo chamado pelo GameManager
    public void ResetDragonMusicTrigger()
    {
        ResetDragonBossSequence();
    }

    // =========================
    // HELPERS
    // =========================
    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public IEnumerator PlayAndWait(AudioClip clip, float? overrideDelaySeconds = null)
    {
        PlayOneShot(clip);
        float d = overrideDelaySeconds ?? sfxDelay;
        if (d > 0f) yield return new WaitForSeconds(d);
    }

    private IEnumerator EnemyIdleLoop(GameManager.EnemyId enemy)
    {
        // pequena espera inicial pra não colidir com o "appear"
        yield return new WaitForSeconds(2f);

        while (true)
        {
            yield return new WaitForSeconds(enemyIdleIntervalSeconds);

            AudioClip idle = null;
            switch (enemy)
            {
                case GameManager.EnemyId.Goblin: idle = goblinIdle; break;
                case GameManager.EnemyId.Orc: idle = orcIdle; break;
                case GameManager.EnemyId.Minotaur: idle = minotaurIdle; break;
                case GameManager.EnemyId.Dragon: idle = dragonIdle; break;
            }

            PlayOneShot(idle);
        }
    }

    private IEnumerator DragonBossSequence()
    {
        // Intro SFX
        PlayOneShot(dragonIntroSfx);

        if (dragonIntroDelaySeconds > 0f)
            yield return new WaitForSeconds(dragonIntroDelaySeconds);

        // Boss music
        if (musicSource != null && dragonBossMusic != null)
        {
            musicSource.clip = dragonBossMusic;
            musicSource.loop = true;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
    }
}
