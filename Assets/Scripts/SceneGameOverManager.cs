using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Controla ações da tela de Game Over
public class SceneGameOverManager : MonoBehaviour
{
    // Tempo de espera antes de trocar de cena (permite animação e som do botão)
    [SerializeField] private float delay = 0.2f;

    // Chamado pelo OnClick do botão (ex: Voltar ao Menu)
    public void GoToMenu()
    {
        StartCoroutine(LoadMenuWithDelay());
    }

    // Aguarda o delay e carrega a cena do menu
    private IEnumerator LoadMenuWithDelay()
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(0); // índice da cena do Menu
    }
}