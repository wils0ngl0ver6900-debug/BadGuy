using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panneaux d'Interface")]
    public GameObject mainMenuPanel;
    public GameObject loadingPanel;

    [Header("Configuration")]
    public string gameSceneName = "SampleScene";

    [Header("Écran de Chargement (UI)")]
    public Slider progressBar;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI hintsText;

    [Header("Astuces de Jeu 💡")]
    public float timeBetweenHints = 4f;
    public string[] gameHints = new string[]
    {
        "Astuce : Les flics vous arrêteront plus vite si vous êtes à pied.",
        "Astuce : Cachez-vous hors de la ligne de vue pour perdre vos étoiles de recherche.",
        "Astuce : Contrôlez 100% d'un territoire pour recruter des membres de gang.",
        "Info : L'argent sale doit être blanchi avant de pouvoir être utilisé légalement.",
        "Astuce : Evitez les coins controllés par les gangs, ils pourraient vous poser quelques problèmes."
    };

    void Start()
    {
        Time.timeScale = 1f; // Sécurité pour éviter un menu figé
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    public void PlayGame()
    {
        StartCoroutine(LoadGameSceneAsynchronously());
        StartCoroutine(AnimateHints());
    }

    public void QuitGame()
    {
        Debug.Log("Fermeture du jeu...");
        Application.Quit();
    }

    private IEnumerator LoadGameSceneAsynchronously()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(gameSceneName);

        // Empêche la scène de s'activer instantanément si elle charge trop vite
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            // Le chargement d'Unity va de 0 à 0.9. On le convertit pour aller de 0 à 1 (0% à 100%)
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (progressBar != null) progressBar.value = progress;
            if (progressText != null) progressText.text = Mathf.RoundToInt(progress * 100f) + "%";

            // Si le chargement est techniquement fini (0.9), on attend 1 seconde pour l'effet visuel, puis on lance
            if (operation.progress >= 0.9f)
            {
                if (progressBar != null) progressBar.value = 1f;
                if (progressText != null) progressText.text = "100%";

                yield return new WaitForSeconds(1f); // Petit délai dramatique
                operation.allowSceneActivation = true;
            }

            yield return null;
        }
    }

    private IEnumerator AnimateHints()
    {
        if (hintsText == null || gameHints.Length == 0) yield break;

        int hintIndex = 0;
        while (true) // Tourne en boucle tant que la scène n'est pas changée
        {
            hintsText.text = gameHints[hintIndex];
            hintIndex++;
            if (hintIndex >= gameHints.Length) hintIndex = 0; // Reboucle au début

            yield return new WaitForSeconds(timeBetweenHints);
        }
    }
}