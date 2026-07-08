using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GangMemberLineUI : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI hpText;
    public Button dismissButton;

    private NPCBrain linkedNPC;

    public void Setup(NPCBrain npc, int index)
    {
        linkedNPC = npc;
        nameText.text = $"Garde du corps #{index}";

        TargetHealth th = npc.GetComponent<TargetHealth>();
        if (th != null)
        {
            string colorTag = "<color=green>";
            if (th.currentHealth < 50) colorTag = "<color=orange>";
            if (th.currentHealth < 20) colorTag = "<color=red>";

            hpText.text = $"Santé : {colorTag}{th.currentHealth}</color>";
        }
        else
        {
            hpText.text = "Santé : ?";
        }

        dismissButton.onClick.RemoveAllListeners();
        dismissButton.onClick.AddListener(DismissAction);
    }

    private void DismissAction()
    {
        if (PlayerGang.Instance != null && linkedNPC != null)
        {
            PlayerGang.Instance.DismissMember(linkedNPC);
        }
    }
}