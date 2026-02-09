using UnityEngine;

// Controla a música tocada durante a tela de créditos
public class CreditsMusic : MonoBehaviour
{
    // Música que será tocada nos créditos
    public AudioClip creditsMusic;

    // AudioSource usado para reproduzir a música
    private AudioSource audioSource;

    // Cria e configura o AudioSource ao iniciar o objeto
    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = creditsMusic;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    // Inicia a reprodução da música
    void Start()
    {
        audioSource.Play();
    }

    // Interrompe a música ao destruir o objeto
    void OnDestroy()
    {
        audioSource.Stop();
    }
}