using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class JobPathfinder : MonoBehaviour
{
    public static JobPathfinder Instance;

    [Header("Configuration")]
    public Transform playerTransform;
    public float pathHeightOffset = 0.3f;

    [Header("Génération des Flèches")]
    public GameObject arrowPrefab;
    public float arrowSpacing = 2f;
    public float scrollSpeed = 3f;
    public float arrowScale = 1.5f;

    public Vector3 arrowRotationOffset = new Vector3(90, 0, 0);

    private NavMeshPath path;

    // NOUVEAU : Double Cible
    private Transform target1;
    private Transform target2;

    private List<GameObject> activeArrows = new List<GameObject>();
    private float scrollOffset = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        path = new NavMeshPath();
        HidePath();
    }

    // Fonction modifiée pour accepter 1 ou 2 cibles !
    public void SetTargets(Transform primaryTarget, Transform secondaryTarget = null)
    {
        target1 = primaryTarget;
        target2 = secondaryTarget;
    }

    public void HidePath()
    {
        target1 = null;
        target2 = null;
        ClearArrows();
    }

    private void Update()
    {
        if ((target1 != null || target2 != null) && playerTransform != null)
        {
            scrollOffset += scrollSpeed * Time.deltaTime;
            if (scrollOffset > arrowSpacing) scrollOffset -= arrowSpacing;

            DrawPathWithArrows();
        }
        else
        {
            ClearArrows();
        }
    }

    private void DrawPathWithArrows()
    {
        int arrowIndex = 0;

        // Dessine la première ligne
        if (target1 != null)
            arrowIndex = DrawSinglePath(target1, arrowIndex);

        // Dessine la deuxième ligne (s'il y en a une)
        if (target2 != null)
            arrowIndex = DrawSinglePath(target2, arrowIndex);

        // Désactive le surplus de flèches en mémoire
        for (int i = arrowIndex; i < activeArrows.Count; i++)
        {
            activeArrows[i].SetActive(false);
        }
    }

    private int DrawSinglePath(Transform target, int startIndex)
    {
        NavMesh.CalculatePath(playerTransform.position, target.position, NavMesh.AllAreas, path);

        if (path.corners.Length < 2) return startIndex;

        int currentIndex = startIndex;

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            Vector3 startPoint = path.corners[i];
            Vector3 endPoint = path.corners[i + 1];

            startPoint.y += pathHeightOffset;
            endPoint.y += pathHeightOffset;

            float segmentLength = Vector3.Distance(startPoint, endPoint);
            Vector3 direction = (endPoint - startPoint).normalized;

            int arrowsInSegment = Mathf.FloorToInt(segmentLength / arrowSpacing);

            for (int j = 0; j < arrowsInSegment; j++)
            {
                float distanceAlongSegment = (j * arrowSpacing) + scrollOffset;
                if (distanceAlongSegment > segmentLength) continue;

                Vector3 position = startPoint + direction * distanceAlongSegment;

                GameObject arrow;
                if (currentIndex < activeArrows.Count)
                {
                    arrow = activeArrows[currentIndex];
                    arrow.SetActive(true);
                }
                else
                {
                    arrow = Instantiate(arrowPrefab, transform);
                    activeArrows.Add(arrow);
                }

                arrow.transform.position = position;
                arrow.transform.localScale = Vector3.one * arrowScale;

                if (direction != Vector3.zero)
                {
                    Quaternion baseRotation = Quaternion.LookRotation(direction);
                    arrow.transform.rotation = baseRotation * Quaternion.Euler(arrowRotationOffset);
                }

                currentIndex++;
            }
        }
        return currentIndex;
    }

    private void ClearArrows()
    {
        foreach (var arrow in activeArrows)
        {
            if (arrow != null) arrow.SetActive(false);
        }
    }
}