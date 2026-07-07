using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SafehouseBed : Interactable
{
    [Header("Paramètres du dodo")]
    public float fadeDuration = 1.0f;
    public float sleepScreenDuration = 2.0f;

    public override void Interact()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            StartCoroutine(SleepRoutine(player));
        }
    }

    private IEnumerator SleepRoutine(PlayerController player)
    {
        player.isDoingQTE = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Vous allez vous coucher...");
        }

        GameObject fadeCanvasObj = new GameObject("FadeCanvasTemporaire");
        Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        GameObject fadeImgObj = new GameObject("ImageNoire");
        fadeImgObj.transform.SetParent(fadeCanvasObj.transform, false);
        Image fadeImage = fadeImgObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);

        RectTransform rect = fadeImgObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(0f, 1f, elapsed / fadeDuration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1f);

        yield return new WaitForSeconds(sleepScreenDuration);

        player.Heal(player.maxHealth);
        player.currentShield = player.maxShield;

        if (ShopManager.Instance != null) ShopManager.Instance.RecoverMarket();
        if (ChopShop.Instance != null) ChopShop.Instance.ResetDailyLimits();

        // ---> LES NOUVEAUTÉS SONT ICI <---

        // 1. On avance réellement l'horloge du jeu de 24 Heures (1440 minutes) !
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.currentTimeOfDay += 1440f;
        }

        // 2. On fait fluctuer les marchés financiers pendant qu'on dort !
        if (StockMarketManager.Instance != null) StockMarketManager.Instance.UpdateMarketDaily();
        if (CryptoMarketManager.Instance != null) CryptoMarketManager.Instance.UpdateMarketDaily();


        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealthDisplay(player.currentHealth, player.maxHealth);
            UIManager.Instance.ShowNotification("Un nouveau jour se lève !");
        }

        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(1f, 0f, elapsed / fadeDuration));
            yield return null;
        }

        Destroy(fadeCanvasObj);
        player.isDoingQTE = false;
    }
}