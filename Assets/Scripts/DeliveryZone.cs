using UnityEngine;

public class DeliveryZone : MonoBehaviour
{
    [Header("Visuels du Garage")]
    public GameObject mapMarker; // Glisse ici ton icône de minimap !
    public GameObject deliveryHologram; // Glisse ici un cylindre transparent (optionnel)

    private void Start()
    {
        // On cache le garage au début
        if (mapMarker != null) mapMarker.SetActive(false);
        if (deliveryHologram != null) deliveryHologram.SetActive(false);
    }

    private void Update()
    {
        // On allume le point sur la carte UNIQUEMENT si la quête de livraison est active !
        bool isQuestActive = QuestManager.Instance != null &&
                             QuestManager.Instance.hasActiveQuest &&
                             QuestManager.Instance.currentQuestType == QuestManager.QuestObjectiveType.LivrerVoiture;

        if (mapMarker != null && mapMarker.activeSelf != isQuestActive) mapMarker.SetActive(isQuestActive);
        if (deliveryHologram != null && deliveryHologram.activeSelf != isQuestActive) deliveryHologram.SetActive(isQuestActive);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (QuestManager.Instance == null || !QuestManager.Instance.hasActiveQuest) return;
        if (QuestManager.Instance.currentQuestType != QuestManager.QuestObjectiveType.LivrerVoiture) return;

        // On vérifie si c'est bien une voiture conduite par le joueur
        CarController car = other.GetComponentInParent<CarController>();
        if (car != null && car.isDrivenByPlayer)
        {
            int progressBefore = QuestManager.Instance.currentProgress; // On note le score avant

            // On tente de valider la livraison en envoyant le nom du modèle !
            QuestManager.Instance.RegisterAction(QuestManager.QuestObjectiveType.LivrerVoiture, 1, car.carModelName);

            // Si le manager a accepté la voiture, le score a augmenté
            if (QuestManager.Instance.currentProgress > progressBefore)
            {
                if (UIManager.Instance != null) UIManager.Instance.ShowNotification("<color=green>Véhicule réceptionné !</color>");

                // On sort le joueur de force et on détruit la voiture (Vendue !)
                CarInteraction interact = car.GetComponent<CarInteraction>();
                if (interact != null) interact.ExitCar();
                Destroy(car.gameObject, 0.5f);
            }
            else
            {
                // Si la voiture n'est pas le bon modèle
                if (UIManager.Instance != null)
                    UIManager.Instance.ShowNotification($"<color=orange>On m'a demandé une {QuestManager.Instance.targetObjectName}, pas cette poubelle !</color>");
            }
        }
    }
}