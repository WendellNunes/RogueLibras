using UnityEngine;

// Gerencia músicas e efeitos sonoros do jogo
public class AudioManager : MonoBehaviour
{
    // Instância única global do AudioManager
    public static AudioManager Instance { get; private set; }

    // AudioSource responsável pelas músicas
    [SerializeField] private AudioSource musicSource;

    // AudioSource responsável pelos efeitos sonoros
    [SerializeField] private AudioSource sfxSource;

    // Garante uma única instância e mantém o objeto entre cenas
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Toca uma música em loop com volume configurável
    public void PlayMusic(AudioClip clip, float volume = 1f, float fade = 0f)
    {
        musicSource.clip = clip;
        musicSource.volume = volume;
        musicSource.loop = true;
        musicSource.Play();
    }

    // Toca um efeito sonoro sem interromper outros
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        sfxSource.PlayOneShot(clip, volume);
    }
}