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

    [Header("Optimisation (À désactiver)")]
    public GameObject environnement3D; // <-- NOUVEAU : Glisse ton objet Environnement_3D ici

    [Header("Configuration")]
    public string gameSceneName = "OutdoorsScene";
    public float minimumLoadingTime = 6f;

    [Header("Écran de Chargement (UI)")]
    public Slider progressBar;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI hintsText;

    [Header("Astuces de Jeu")]
    public float timeBetweenHints = 4f;
    public string[] gameHints = new string[]
    {
        "Astuce : Les flics vous arrêteront plus vite si vous êtes à pied.",
        "Astuce : Cachez-vous hors de la ligne de vue pour perdre vos étoiles de recherche.",
        "Astuce : Contrôlez 100% d'un territoire pour recruter des membres de gang.",
        "Info : L'argent sale doit être blanchi avant de pouvoir être utilisé légalement.",
        "Astuce : Evitez les coins controlés par les gangs, ils pourraient vous poser quelques problèmes."
    };

    void Start()
    {
        Time.timeScale = 1f;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (environnement3D != null) environnement3D.SetActive(true); // On s'assure qu'il est allumé au lancement
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
        // 1. On cache le menu
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

        // 2. On éteint toute la 3D pour libérer la carte graphique et le processeur !
        if (environnement3D != null) environnement3D.SetActive(false);

        // 3. On affiche l'écran noir de chargement
        if (loadingPanel != null) loadingPanel.SetActive(true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(gameSceneName);
        operation.allowSceneActivation = false;

        float elapsedTime = 0f;

        while (elapsedTime < minimumLoadingTime || operation.progress < 0.9f)
        {
            elapsedTime += Time.deltaTime;
            float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
            float timeProgress = Mathf.Clamp01(elapsedTime / minimumLoadingTime);
            float displayProgress = Mathf.Min(sceneProgress, timeProgress);

            if (progressBar != null) progressBar.value = displayProgress;
            if (progressText != null) progressText.text = Mathf.RoundToInt(displayProgress * 100f) + "%";

            yield return null;
        }

        if (progressBar != null) progressBar.value = 1f;
        if (progressText != null) progressText.text = "100%";

        yield return new WaitForSeconds(0.5f);
        operation.allowSceneActivation = true;
    }

    private IEnumerator AnimateHints()
    {
        if (hintsText == null || gameHints.Length == 0) yield break;

        int hintIndex = 0;
        while (true)
        {
            hintsText.text = gameHints[hintIndex];
            hintIndex++;
            if (hintIndex >= gameHints.Length) hintIndex = 0;

            yield return new WaitForSeconds(timeBetweenHints);
        }
    }
}