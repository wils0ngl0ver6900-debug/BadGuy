using UnityEngine;

public class ValetParkingZone : MonoBehaviour
{
    [Header("UI & Détection")]
    public GameObject parkPromptUI;

    private ValetVehicle currentVehicle;
    private bool canValidate = false;
    private GameObject selectionMarker;

    private void Start()
    {
        if (parkPromptUI != null) parkPromptUI.SetActive(false);
        CreateSelectionMarker();
    }

    private void CreateSelectionMarker()
    {
        selectionMarker = new GameObject("SelectionMarker_Parking");
        selectionMarker.transform.SetParent(transform);
        selectionMarker.transform.localPosition = Vector3.zero;
        selectionMarker.transform.localRotation = Quaternion.identity;

        LineRenderer lr = selectionMarker.AddComponent<LineRenderer>();
        lr.startWidth = 0.2f;
        lr.endWidth = 0.2f;
        lr.positionCount = 5;
        lr.useWorldSpace = false;

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0f, 0.8f, 1f, 0.8f);
        lr.endColor = new Color(0f, 0.8f, 1f, 0.8f);

        float halfWidth = 1.8f;
        float halfLength = 3.2f;
        float yOffset = 0.1f;

        lr.SetPosition(0, new Vector3(-halfWidth, yOffset, -halfLength));
        lr.SetPosition(1, new Vector3(-halfWidth, yOffset, halfLength));
        lr.SetPosition(2, new Vector3(halfWidth, yOffset, halfLength));
        lr.SetPosition(3, new Vector3(halfWidth, yOffset, -halfLength));
        lr.SetPosition(4, new Vector3(-halfWidth, yOffset, -halfLength));

        selectionMarker.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        ValetVehicle vehicle = other.GetComponentInParent<ValetVehicle>();

        if (vehicle != null)
        {
            currentVehicle = vehicle;
            canValidate = true;
            if (parkPromptUI != null) parkPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ValetVehicle vehicle = other.GetComponentInParent<ValetVehicle>();

        if (vehicle != null && vehicle == currentVehicle)
        {
            currentVehicle = null;
            canValidate = false;
            if (parkPromptUI != null) parkPromptUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (selectionMarker != null)
        {
            bool isJobActive = ValetJobManager.Instance != null && ValetJobManager.Instance.isJobActive;
            if (selectionMarker.activeSelf != isJobActive)
            {
                selectionMarker.SetActive(isJobActive);
            }
        }

        if (canValidate && currentVehicle != null && Input.GetKeyDown(KeyCode.Space))
        {
            ValidateParking();
        }
    }

    private void ValidateParking()
    {
        canValidate = false;
        if (parkPromptUI != null) parkPromptUI.SetActive(false);

        float angle = Vector3.Angle(transform.forward, currentVehicle.transform.forward);
        if (angle > 90f) angle = 180f - angle;
        int angleError = Mathf.FloorToInt(angle / 5f);

        Vector3 zonePos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 carPos = new Vector3(currentVehicle.transform.position.x, 0, currentVehicle.transform.position.z);

        float distance = Vector3.Distance(zonePos, carPos);
        int distanceError = Mathf.FloorToInt(distance * 2f);

        int totalAlignmentError = angleError + distanceError;
        int finalDamage = currentVehicle.GetAccumulatedDamage();

        if (ValetJobManager.Instance != null)
        {
            ValetJobManager.Instance.SubmitParkingValidation(finalDamage, totalAlignmentError);
        }

        // Le joueur reste dans le véhicule, c'est le Manager qui le détruira pendant le fondu au noir !
        currentVehicle = null;
    }
}