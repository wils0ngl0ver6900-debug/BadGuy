using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    public static MinimapFollow Instance;

    [Header("Cible à suivre")]
    public Transform target;
    public float height = 20f; // Hauteur de la caméra

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void LateUpdate()
    {
        if (target != null)
        {
            // On suit la position X et Z de la cible, mais on garde notre hauteur (Y)
            transform.position = new Vector3(target.position.x, height, target.position.z);
        }
    }
}