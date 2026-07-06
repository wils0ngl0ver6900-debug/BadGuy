using UnityEngine;
using TMPro;

public class TransactionUI : MonoBehaviour
{
    public TextMeshProUGUI descText;
    public TextMeshProUGUI amountText;
    public TextMeshProUGUI timeText;

    public void Setup(string desc, int amount, string time)
    {
        descText.text = desc;
        timeText.text = time;

        if (amount >= 0)
        {
            amountText.text = $"+ {amount}$";
            amountText.color = new Color(0.1f, 0.8f, 0.1f); // Vert pro
        }
        else
        {
            amountText.text = $"- {Mathf.Abs(amount)}$";
            amountText.color = new Color(0.9f, 0.2f, 0.2f); // Rouge
        }
    }
}