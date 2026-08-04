using UnityEngine;

// Version ULTRA SIMPLE : Gère juste la position et les calques
public class MapSatelliteCamera : MonoBehaviour
{
    [Header("Calibration")]
    public Vector2 worldBottomLeft = new Vector2(-500f, -500f);
    public Vector2 worldTopRight = new Vector2(500f, 500f);
    public float heightAboveWorld = 300f;
    public float cameraYaw = 0f;

    [Header("Calques de la Map")]
    [Tooltip("Coche 'Default' (bâtiments) ET ton nouveau calque 'OceanMap'")]
    public LayerMask satelliteViewCullingMask = ~0;

    private Camera minimapCamera;
    private MinimapFollow minimapFollow;
    private FixCameraRotation fixCameraRotation;

    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private bool savedOrthographic;
    private float savedOrthoSizeOrFOV;
    private int savedCullingMask;

    void Start()
    {
        minimapFollow = MinimapFollow.Instance;
        if (minimapFollow != null)
        {
            minimapCamera = minimapFollow.GetComponent<Camera>();
            fixCameraRotation = minimapFollow.GetComponent<FixCameraRotation>();
        }
    }

    public void ActivateMap()
    {
        if (minimapCamera == null) return;

        savedPosition = minimapCamera.transform.position;
        savedRotation = minimapCamera.transform.rotation;
        savedOrthographic = minimapCamera.orthographic;
        savedOrthoSizeOrFOV = minimapCamera.orthographic ? minimapCamera.orthographicSize : minimapCamera.fieldOfView;
        savedCullingMask = minimapCamera.cullingMask;

        if (minimapFollow != null) minimapFollow.enabled = false;
        if (fixCameraRotation != null) fixCameraRotation.enabled = false;

        Vector2 center = (worldBottomLeft + worldTopRight) * 0.5f;
        float halfHeight = Mathf.Abs(worldTopRight.y - worldBottomLeft.y) * 0.5f;

        minimapCamera.orthographic = true;
        minimapCamera.transform.position = new Vector3(center.x, heightAboveWorld, center.y);
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, cameraYaw);
        minimapCamera.orthographicSize = halfHeight;

        // On applique les calques que la map a le droit de voir
        minimapCamera.cullingMask = satelliteViewCullingMask;
    }

    public void DeactivateMap()
    {
        if (minimapCamera == null) return;

        minimapCamera.transform.position = savedPosition;
        minimapCamera.transform.rotation = savedRotation;
        minimapCamera.orthographic = savedOrthographic;
        if (savedOrthographic) minimapCamera.orthographicSize = savedOrthoSizeOrFOV;
        else minimapCamera.fieldOfView = savedOrthoSizeOrFOV;
        minimapCamera.cullingMask = savedCullingMask;

        if (minimapFollow != null) minimapFollow.enabled = true;
        if (fixCameraRotation != null) fixCameraRotation.enabled = true;
    }
}