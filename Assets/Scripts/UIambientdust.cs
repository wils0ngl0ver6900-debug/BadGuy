using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Ajoute une pluie de petites particules qui dérivent doucement (poussière en suspension,
// cendres...) par-dessus un fond d'UI statique, pour lui donner un peu de vie. Marche sur
// n'importe quel panel (weed, héro, coke...) avec le MÊME script : pose-le une fois par
// panel, pas besoin de rien dupliquer/adapter à la main.
//
// Fonctionne en pur UI (Image), pas en Particle System classique : un ParticleSystem
// Shuriken ne s'affiche pas correctement dans un Canvas Screen Space - Overlay sans
// configuration spéciale, alors que des Image toutes simples marchent partout, sans
// prérequis.
//
// Mise en place : pose ce script sur ton panel (ou un enfant vide dédié, ex: "DustLayer",
// avec un RectTransform qui recouvre la zone où tu veux l'effet), assigne un sprite rond
// et doux dans "Dust Sprite", ajuste le nombre de particules et la vitesse à ton goût.
public class UIAmbientDust : MonoBehaviour
{
    [Header("Apparence")]
    public Sprite dustSprite;
    [Range(1, 60)] public int particleCount = 20;
    public Color dustColor = new Color(1f, 1f, 1f, 0.35f);
    public Vector2 sizeRange = new Vector2(4f, 10f);
    [Tooltip("Fait légèrement varier l'opacité de chaque particule pour casser la régularité.")]
    [Range(0f, 1f)] public float opacityVariation = 0.4f;

    [Header("Mouvement")]
    [Tooltip("Direction de dérive générale. (0,1) = vers le haut, (1,0) = vers la droite, etc.")]
    public Vector2 driftDirection = new Vector2(0f, 1f);
    public Vector2 speedRange = new Vector2(5f, 15f);
    [Tooltip("Amplitude du léger mouvement de balancier gauche-droite, en pixels.")]
    public float swayAmplitude = 15f;
    public float swaySpeed = 1f;

    [Header("Zone d'effet")]
    [Tooltip("Taille de la zone (en pixels UI) dans laquelle les particules vivent et bouclent. Mets la taille de ton panel.")]
    public Vector2 spawnAreaSize = new Vector2(800f, 450f);

    private class DustParticle
    {
        public RectTransform rect;
        public Image image;
        public float baseX;
        public float swayOffset;
        public float speed;
        public float baseAlpha;
    }

    private readonly List<DustParticle> particles = new List<DustParticle>();

    void Start()
    {
        for (int i = 0; i < particleCount; i++)
        {
            CreateParticle(randomStartPos: true);
        }
    }

    private void CreateParticle(bool randomStartPos)
    {
        if (dustSprite == null) return;

        GameObject go = new GameObject("DustParticle", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = dustSprite;
        img.raycastTarget = false; // Ne doit jamais bloquer un clic ou un drag en dessous

        float alpha = dustColor.a * Random.Range(1f - opacityVariation, 1f);
        img.color = new Color(dustColor.r, dustColor.g, dustColor.b, alpha);

        RectTransform rt = go.GetComponent<RectTransform>();
        float size = Random.Range(sizeRange.x, sizeRange.y);
        rt.sizeDelta = new Vector2(size, size);

        Vector2 half = spawnAreaSize / 2f;
        float x = Random.Range(-half.x, half.x);
        float y = randomStartPos ? Random.Range(-half.y, half.y) : -half.y;
        rt.anchoredPosition = new Vector2(x, y);

        particles.Add(new DustParticle
        {
            rect = rt,
            image = img,
            baseX = x,
            swayOffset = Random.Range(0f, Mathf.PI * 2f),
            speed = Random.Range(speedRange.x, speedRange.y),
            baseAlpha = alpha
        });
    }

    void Update()
    {
        if (particles.Count == 0) return;

        Vector2 dir = driftDirection.sqrMagnitude > 0.0001f ? driftDirection.normalized : Vector2.up;
        Vector2 half = spawnAreaSize / 2f;

        foreach (DustParticle p in particles)
        {
            Vector2 pos = p.rect.anchoredPosition;
            pos += dir * p.speed * Time.deltaTime;

            // Petit balancier gauche-droite autour de la trajectoire de base.
            float sway = Mathf.Sin(Time.time * swaySpeed + p.swayOffset) * swayAmplitude;
            pos.x = p.baseX + sway;

            // Boucle : dès qu'une particule sort de la zone dans le sens de la dérive,
            // on la replace de l'autre côté avec une nouvelle position aléatoire.
            bool wrapped = false;
            if (dir.y > 0.01f && pos.y > half.y) { pos.y = -half.y; wrapped = true; }
            else if (dir.y < -0.01f && pos.y < -half.y) { pos.y = half.y; wrapped = true; }
            if (dir.x > 0.01f && pos.x > half.x) { pos.x = -half.x; wrapped = true; }
            else if (dir.x < -0.01f && pos.x < -half.x) { pos.x = half.x; wrapped = true; }

            if (wrapped) p.baseX = Random.Range(-half.x, half.x);

            p.rect.anchoredPosition = pos;
        }
    }
}