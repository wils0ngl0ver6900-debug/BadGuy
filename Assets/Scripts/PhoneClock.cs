using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class PhoneClock : MonoBehaviour
{
    private TextMeshProUGUI clockText;

    void Awake()
    {
        clockText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        // On récupère l'heure fictive directement depuis notre TimeManager !
        if (clockText != null && TimeManager.Instance != null)
        {
            clockText.text = TimeManager.Instance.GetFormattedTime();
        }
    }
}