using UnityEngine;

[CreateAssetMenu(fileName = "Nouvel Objet", menuName = "Crooked Money/Objet")]
public class ItemData : ScriptableObject
{
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public int buyPrice;
    public int valueInBlackMarket;
    public bool isIllegal;

    [Header("Paramètres d'Équipement")]
    public bool isEquippable;
    public bool isConsumable;

    [Header("Consommables Standard 🍔")]
    public int healAmount;
    public float speedBoostMultiplier; // Ex: 1.5 pour +50% de vitesse
    public float buffDuration; // Durée du boost en secondes

    // --- NOUVEAU : SYSTÈME DE DROGUE ET DESCENTE ---
    [Header("Effets de Drogue Spéciaux 😵")]
    public bool isDrugWithComedown; // Cochez cette case pour activer la descente !
    public float comedownDuration; // Durée de la descente en secondes
    public float comedownSpeedMultiplier = 0.5f; // Vitesse pendant la descente (ex: 0.5 = deux fois plus lent)
    public bool invertControlsDuringComedown = true; // Est-ce que les touches s'inversent ?

    public enum Rarity { Basique, PeuCourant, Rare, Legendaire }
    public Rarity rarity;
    
    [Header("Système de Vêtements / RPG 👕")]
    public bool isClothing;

    public enum ClothingSlot { Tete, Torse, Jambes, Pieds }
    public ClothingSlot clothingSlot;

    public int armorBonus;       // Ex: +50 pour le gilet pare-balles
    public float speedBonus;     // Ex: +0.5f pour les baskets de course
   
    // --- LA NOUVELLE VARIABLE ---
    [Header("Système d'Infiltration 🥷")]
    public bool isMask; // Coche cette case pour les cagoules/masques !

    [Header("Système d'Armes 🔫")]
    public bool isWeapon;
    public int damage;
    public int maxAmmo = 12;
    public float fireRate = 0.5f;
    public GameObject weaponPrefab;
    public GameObject bulletPrefab;
}