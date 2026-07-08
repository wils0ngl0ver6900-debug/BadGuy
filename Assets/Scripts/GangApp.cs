using UnityEngine;
using TMPro;

public class GangApp : MonoBehaviour
{
    public static GangApp Instance;

    [Header("UI App Gang")]
    public GameObject appPanel;
    public Transform contentParent;
    public GameObject memberLinePrefab;
    public TextMeshProUGUI statusText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void OpenApp()
    {
        appPanel.SetActive(true);
        RefreshUI();
    }

    public void CloseApp()
    {
        appPanel.SetActive(false);
    }

    public void RefreshUI()
    {
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        PlayerGang gang = PlayerGang.Instance;
        if (gang == null) return;

        if (statusText != null)
        {
            statusText.text = $"Membres actifs : {gang.currentRecruits.Count} / {gang.maxRecruits}";
        }

        for (int i = 0; i < gang.currentRecruits.Count; i++)
        {
            GameObject newLine = Instantiate(memberLinePrefab, contentParent);
            GangMemberLineUI uiScript = newLine.GetComponent<GangMemberLineUI>();

            if (uiScript != null)
            {
                uiScript.Setup(gang.currentRecruits[i], i + 1);
            }
        }
    }

    public void DisbandAll()
    {
        if (PlayerGang.Instance != null)
        {
            PlayerGang.Instance.DisbandGang();
        }
    }
}