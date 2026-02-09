using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controla ações do menu relacionadas à troca de cena
public class SceneMenuManager : MonoBehaviour
{
    // Tempo de espera antes de carregar a cena (permite animação e som do botão)
    [SerializeField] private float delay = 0.2f;

    // Chamado pelo OnClick do botão Start
    public void StartGame()
    {
        StartCoroutine(LoadSceneWithDelay());
    }

    // Aguarda o delay e carrega a cena do jogo
    private IEnumerator LoadSceneWithDelay()
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(1);
    }
}