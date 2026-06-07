using UnityEngine;

public class MapManager : MonoBehaviour
{
    [Header("Interface de la Carte")]
    public GameObject mapPanel;

    private bool isMapOpen = false;

    void Start()
    {
        // On s'assure que la map est cachée au lancement
        if (mapPanel != null) mapPanel.SetActive(false);
    }

    void Update()
    {
        // Touche M pour ouvrir/fermer
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMap();
        }
    }

    public void ToggleMap()
    {
        if (mapPanel == null) return;

        isMapOpen = !isMapOpen;
        mapPanel.SetActive(isMapOpen);

        if (isMapOpen)
        {
            // Pause le jeu et libère la souris
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Reprend le jeu et cache la souris
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}