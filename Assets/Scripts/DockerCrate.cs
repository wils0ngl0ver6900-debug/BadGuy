using UnityEngine;

public class DockerCrate : Interactable
{
    [Header("Type de Caisse")]
    public bool isGangCrate = false;

    public override void Interact()
    {
        // On empêche de ramasser si le job n'a pas commencé !
        if (DockerJobManager.Instance != null && !DockerJobManager.Instance.isJobActive)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Parlez au contremaître pour commencer à travailler.");
            return;
        }

        if (DockerJobManager.Instance != null && !DockerJobManager.Instance.isCarryingCrate)
        {
            DockerJobManager.Instance.PickupCrate(isGangCrate);
            Destroy(gameObject);
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowNotification("Vous portez déjà une caisse !");
        }
    }
}