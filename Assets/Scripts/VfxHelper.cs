using UnityEngine;

// Utilitaire partagé pour générer des effets de particules à la volée (sang, étincelles,
// fumée) sans dépendre d'un asset externe — mais chaque méthode accepte un prefab optionnel
// en override si tu veux brancher un effet plus travaillé plus tard.
// Centralisé ici plutôt que dupliqué dans Bullet.cs ET CarExplosionImproved.cs.
public static class VFXHelper
{
    public static void SpawnBloodSplatter(Vector3 position, Vector3 hitDirection, GameObject customPrefab = null)
    {
        if (customPrefab != null)
        {
            Object.Instantiate(customPrefab, position, Quaternion.LookRotation(hitDirection));
            return;
        }

        GameObject go = new GameObject("BloodSplatter_VFX");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new Color(0.45f, 0.02f, 0.02f); // Rouge sang foncé
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, (short)Random.Range(8, 14))
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.05f;
        ps.transform.rotation = Quaternion.LookRotation(hitDirection);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.5f, 0.03f, 0.03f), 0f), new GradientColorKey(new Color(0.2f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        Renderer rend = ps.GetComponent<Renderer>();
        rend.material = GetHDRPUnlitMaterial(new Color(0.5f, 0.03f, 0.03f));

        Object.Destroy(go, 1.5f);
    }

    // Étincelles légères pour un impact de balle sur mur/voiture/obstacle (pas de fumée,
    // moins de particules que l'explosion — juste un flash bref au point d'impact).
    public static void SpawnImpactSparks(Vector3 position, Vector3 surfaceDirection, GameObject customPrefab = null)
    {
        if (customPrefab != null)
        {
            Object.Instantiate(customPrefab, position, Quaternion.LookRotation(surfaceDirection));
            return;
        }

        GameObject go = new GameObject("ImpactSparks_VFX");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(surfaceDirection);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new Color(1f, 0.75f, 0.2f);
        main.gravityModifier = 0.5f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)Random.Range(5, 10)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.03f;

        Renderer rend = ps.GetComponent<Renderer>();
        rend.material = GetHDRPUnlitMaterial(new Color(1f, 0.75f, 0.2f));

        Object.Destroy(go, 0.6f);
    }

    // Le projet est en HDRP — "Sprites/Default" ou les shaders URP rendent en rose/invisible
    // sous ce pipeline. HDRP/Unlit est le bon shader ici.
    private static Material GetHDRPUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("[VFXHelper] Shader 'HDRP/Unlit' introuvable — vérifie que le projet utilise bien HDRP.");
            shader = Shader.Find("Unlit/Color"); // dernier recours, probablement rose sous HDRP mais évite un crash
        }

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        return mat;
    }

    public static void SpawnSparkAndSmokeBurst(Vector3 position, GameObject customPrefab = null)
    {
        if (customPrefab != null)
        {
            Object.Instantiate(customPrefab, position, Quaternion.identity);
            return;
        }

        // --- Étincelles ---
        GameObject sparks = new GameObject("ExplosionSparks_VFX");
        sparks.transform.position = position;
        ParticleSystem sparksPs = sparks.AddComponent<ParticleSystem>();
        var sMain = sparksPs.main;
        sMain.duration = 0.5f;
        sMain.loop = false;
        sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        sMain.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        sMain.startColor = new Color(1f, 0.6f, 0.1f);
        sMain.gravityModifier = 0.8f;

        var sEmission = sparksPs.emission;
        sEmission.rateOverTime = 0f;
        sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)Random.Range(20, 35)) });

        var sShape = sparksPs.shape;
        sShape.shapeType = ParticleSystemShapeType.Sphere;
        sShape.radius = 0.3f;

        Renderer sRend = sparksPs.GetComponent<Renderer>();
        sRend.material = GetHDRPUnlitMaterial(new Color(1f, 0.6f, 0.1f));

        // --- Fumée ---
        GameObject smoke = new GameObject("ExplosionSmoke_VFX");
        smoke.transform.position = position;
        ParticleSystem smokePs = smoke.AddComponent<ParticleSystem>();
        var smMain = smokePs.main;
        smMain.duration = 1.5f;
        smMain.loop = false;
        smMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        smMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        smMain.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        smMain.startColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        smMain.gravityModifier = -0.05f; // Monte lentement

        var smEmission = smokePs.emission;
        smEmission.rateOverTime = 0f;
        smEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)Random.Range(6, 10)) });

        var smColor = smokePs.colorOverLifetime;
        smColor.enabled = true;
        Gradient smGrad = new Gradient();
        smGrad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.15f, 0.15f, 0.15f), 0f), new GradientColorKey(new Color(0.05f, 0.05f, 0.05f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        smColor.color = smGrad;

        Renderer smRend = smokePs.GetComponent<Renderer>();
        smRend.material = GetHDRPUnlitMaterial(new Color(0.15f, 0.15f, 0.15f, 0.5f));

        Object.Destroy(sparks, 1f);
        Object.Destroy(smoke, 3.5f);
    }

    // Flaque de sang au sol sous un PNJ mort, qui grandit progressivement puis se stabilise.
    // Utilise un cylindre aplati (disque plein, opaque) plutôt qu'un quad + texture alpha :
    // la transparence HDRP configurée par script est fragile selon la version du package,
    // alors qu'un matériau opaque avec juste une couleur fonctionne de façon fiable partout
    // (c'est d'ailleurs ce que le sang/les étincelles utilisent déjà, avec succès).
    // Multiplicateur global de taille de la flaque — modifie cette valeur si tu veux
    // l'ajuster plus tard sans repasser par du code (plus simple qu'un chiffre en dur).
    public static float bloodPoolSizeMultiplier = 2.5f;

    public static void SpawnGrowingBloodPool(Vector3 approximateCorpsePosition, Transform ignoreHierarchy = null, GameObject customPrefab = null)
    {
        if (customPrefab != null)
        {
            Object.Instantiate(customPrefab, approximateCorpsePosition, Quaternion.identity);
            return;
        }

        // Raycast depuis bien plus haut, et on ignore explicitement les colliders qui
        // appartiennent au cadavre lui-même (ses propres membres de ragdoll, allongés au sol,
        // peuvent sinon être touchés AVANT le vrai sol — la flaque se retrouvait alors placée/
        // orientée selon la surface du corps au lieu du sol, d'où l'effet "flaque qui passe
        // par-dessus le personnage").
        Vector3 rayOrigin = approximateCorpsePosition + Vector3.up * 2f;
        RaycastHit[] allHits = Physics.RaycastAll(rayOrigin, Vector3.down, 6f);
        System.Array.Sort(allHits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? groundHit = null;
        foreach (RaycastHit h in allHits)
        {
            if (ignoreHierarchy != null && h.collider.transform.IsChildOf(ignoreHierarchy)) continue;
            groundHit = h;
            break;
        }

        if (groundHit == null)
        {
            Debug.LogWarning("[VFXHelper] SpawnGrowingBloodPool : aucun sol détecté (hors corps) sous le corps, flaque annulée.");
            return;
        }

        RaycastHit hit = groundHit.Value;

        // Un amas de plusieurs petites taches (pas un seul gros disque parfait) : bien plus
        // organique, et ça évite qu'une grande flaque unique "avale" toute la silhouette du
        // corps vue de dessus. Un seul GameObject parent regroupe le tout pour rester propre.
        GameObject cluster = new GameObject("BloodPool_VFX");
        cluster.transform.position = hit.point + hit.normal * 0.015f;
        cluster.transform.up = hit.normal;

        int blobCount = Random.Range(3, 6);
        for (int i = 0; i < blobCount; i++)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blob.name = "Blob";
            blob.transform.SetParent(cluster.transform, false);
            Object.Destroy(blob.GetComponent<Collider>());

            Vector2 offset = (i == 0) ? Vector2.zero : Random.insideUnitCircle * (0.15f * bloodPoolSizeMultiplier);
            blob.transform.localPosition = new Vector3(offset.x, 0f, offset.y);
            blob.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            float startRadius = 0.03f;
            blob.transform.localScale = new Vector3(startRadius, 0.005f, startRadius);

            Renderer rend = blob.GetComponent<Renderer>();
            float shade = Random.Range(0.85f, 1.1f);
            rend.material = GetHDRPUnlitMaterial(new Color(0.3f * shade, 0.02f * shade, 0.02f * shade));

            BloodPoolGrower grower = blob.AddComponent<BloodPoolGrower>();
            // La tache centrale (i == 0) est la plus grosse, les autres autour sont plus petites —
            // silhouette irrégulière façon éclaboussure plutôt qu'un cercle net.
            grower.targetRadius = (i == 0)
                ? Random.Range(0.35f, 0.55f) * bloodPoolSizeMultiplier
                : Random.Range(0.12f, 0.28f) * bloodPoolSizeMultiplier;
            grower.growDuration = Random.Range(2.5f, 4f);
        }

        Object.Destroy(cluster, 30f); // La flaque ne doit pas rester indéfiniment
    }

    // Génère une texture circulaire à bords doux (alpha dégradé) — gardée disponible si tu
    // veux un jour revenir à une version transparente une fois la config HDRP confirmée,
    // mais plus utilisée par défaut pour l'instant.
    private static Texture2D GenerateSoftCircleTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxDist = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                alpha = Mathf.Pow(alpha, 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }
}

// Fait grandir la flaque de sang (rayon du disque, pas juste une échelle uniforme) puis
// s'arrête. La hauteur (Y) ne bouge jamais — seul le rayon (X/Z) grandit.
public class BloodPoolGrower : MonoBehaviour
{
    public float targetRadius = 0.8f;
    public float growDuration = 3f;

    private float elapsed = 0f;
    private float startRadius;
    private float fixedHeight;

    void Start()
    {
        startRadius = transform.localScale.x;
        fixedHeight = transform.localScale.y;
    }

    void Update()
    {
        if (elapsed >= growDuration)
        {
            enabled = false;
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / growDuration);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        float radius = Mathf.Lerp(startRadius, targetRadius, eased);
        transform.localScale = new Vector3(radius, fixedHeight, radius);
    }
}