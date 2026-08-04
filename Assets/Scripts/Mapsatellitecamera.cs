using UnityEngine;

// Réutilise DIRECTEMENT ta caméra de minimap existante (celle avec MinimapFollow dessus)
// au lieu d'en créer une nouvelle — pas besoin de dupliquer quoi que ce soit. Quand la carte
// plein écran s'ouvre, on coupe temporairement le suivi du joueur et on bascule la caméra en
// vue statique de toute la ville (calquée sur les coordonnées de UIMapPlayerIcon) ; à la
// fermeture, tout redevient exactement comme avant pour que la minimap continue de fonctionner.
public class MapSatelliteCamera : MonoBehaviour
{
    [Header("Calibration — MÊMES valeurs que sur UIMapPlayerIcon")]
    [Tooltip("Coordonnées (X, Z) du point tout en BAS À GAUCHE de ta ville 3D")]
    public Vector2 worldBottomLeft = new Vector2(-500f, -500f);
    [Tooltip("Coordonnées (X, Z) du point tout en HAUT À DROITE de ta ville 3D")]
    public Vector2 worldTopRight = new Vector2(500f, 500f);
    public float heightAboveWorld = 300f;
    [Tooltip("Si les bâtiments/rues apparaissent tournés (ex: horizontal au lieu de vertical), ajuste ça par pas de 90 jusqu'à ce que ça s'aligne.")]
    public float cameraYaw = 0f;

    [Header("Calques visibles en vue satellite (rues/bâtiments SEULEMENT)")]
    [Tooltip("Coche uniquement les calques que tu veux voir sur la carte plein écran — décoche tout ce qui est PNJ/icônes tactiques/effets, réservés à la minimap rapprochée.")]
    public LayerMask satelliteViewCullingMask = ~0;

    private Camera minimapCamera;
    private MinimapFollow minimapFollow;

    // Sauvegarde de l'état normal de la minimap, pour tout restaurer à la fermeture
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private bool savedOrthographic;
    private float savedOrthoSizeOrFOV;
    private int savedCullingMask;

    void Start()
    {
        minimapFollow = MinimapFollow.Instance;
        if (minimapFollow != null) minimapCamera = minimapFollow.GetComponent<Camera>();

        if (minimapCamera == null)
        {
            Debug.LogWarning("[MapSatelliteCamera] Impossible de trouver la caméra de MinimapFollow — vérifie que MinimapFollow.Instance existe bien dans la scène.");
        }
    }

    public void ActivateMap()
    {
        if (minimapCamera == null || minimapFollow == null)
        {
            Debug.LogWarning($"[MapSatelliteCamera] ActivateMap() annulée — minimapCamera null: {minimapCamera == null}, minimapFollow null: {minimapFollow == null}");
            return;
        }

        Debug.Log($"[MapSatelliteCamera] ActivateMap() sur la caméra '{minimapCamera.gameObject.name}', cameraYaw={cameraYaw}");

        // Sauvegarde l'état actuel de la minimap pour pouvoir tout remettre en place après
        savedPosition = minimapCamera.transform.position;
        savedRotation = minimapCamera.transform.rotation;
        savedOrthographic = minimapCamera.orthographic;
        savedOrthoSizeOrFOV = minimapCamera.orthographic ? minimapCamera.orthographicSize : minimapCamera.fieldOfView;
        savedCullingMask = minimapCamera.cullingMask;

        minimapFollow.enabled = false; // On coupe le suivi du joueur pendant que la carte plein écran est ouverte

        Vector2 center = (worldBottomLeft + worldTopRight) * 0.5f;
        float halfHeight = Mathf.Abs(worldTopRight.y - worldBottomLeft.y) * 0.5f;

        minimapCamera.orthographic = true;
        minimapCamera.transform.position = new Vector3(center.x, heightAboveWorld, center.y);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, cameraYaw);
        Debug.Log($"[MapSatelliteCamera] Rotation appliquée : {minimapCamera.transform.rotation.eulerAngles}, position : {minimapCamera.transform.position}, orthoSize : {minimapCamera.orthographicSize}");
        minimapCamera.orthographicSize = halfHeight;
        minimapCamera.cullingMask = satelliteViewCullingMask;
    }

    public void DeactivateMap()
    {
        if (minimapCamera == null || minimapFollow == null) return;

        // On remet tout exactement comme avant pour que la minimap reprenne normalement
        minimapCamera.transform.position = savedPosition;
        minimapCamera.transform.rotation = savedRotation;
        minimapCamera.orthographic = savedOrthographic;
        if (savedOrthographic) minimapCamera.orthographicSize = savedOrthoSizeOrFOV;
        else minimapCamera.fieldOfView = savedOrthoSizeOrFOV;
        minimapCamera.cullingMask = savedCullingMask;

        minimapFollow.enabled = true;
    }
}