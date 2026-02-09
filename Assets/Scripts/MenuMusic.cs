using UnityEngine;

// Controla a música tocada no menu principal
public class MenuMusic : MonoBehaviour
{
    // Música do menu
    public AudioClip menuMusic;

    // AudioSource usado para reproduzir a música
    private AudioSource audioSource;

    // Cria e configura o AudioSource
    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = menuMusic;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    // Inicia a música ao entrar no menu
    void Start()
    {
        audioSource.Play();
    }

    // Para a música ao destruir o objeto
    void OnDestroy()
    {
        audioSource.Stop();
    }
}