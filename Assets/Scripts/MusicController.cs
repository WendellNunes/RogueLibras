using UnityEngine;

// Controla qual música de fundo toca conforme o estado do jogo
public class MusicController : MonoBehaviour
{
    // Define a música inicial ao iniciar o jogo
    public enum InitialMusic
    {
        None,
        Battle,
        Shop
    }

    [Header("Music Clips")]
    // Música da batalha
    [SerializeField] private AudioClip battleMusic;

    // Música da loja
    [SerializeField] private AudioClip shopMusic;

    [Header("Settings")]
    // Volume da música
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;

    [Header("Start Behavior")]
    // Música que toca ao iniciar o jogo
    [SerializeField] private InitialMusic initialMusic = InitialMusic.Battle;

    // Cache da música atual para evitar reinício
    private AudioClip _current;

    // Define a música inicial ao dar Play
    private void Start()
    {
        switch (initialMusic)
        {
            case InitialMusic.Battle:
                PlayBattle();
                break;

            case InitialMusic.Shop:
                PlayShop();
                break;

            case InitialMusic.None:
            default:
                // Não toca música ao iniciar
                break;
        }
    }

    // Solicita a música de batalha
    public void PlayBattle()
    {
        PlayIfDifferent(battleMusic);
    }

    // Solicita a música da loja
    public void PlayShop()
    {
        PlayIfDifferent(shopMusic);
    }

    // Toca a música apenas se for diferente da atual
    private void PlayIfDifferent(AudioClip clip)
    {
        if (clip == null) return;
        if (_current == clip) return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("AudioManager.Instance não encontrado. Música não pode tocar ainda.");
            return;
        }

        _current = clip;
        AudioManager.Instance.PlayMusic(clip, volume);
    }
}