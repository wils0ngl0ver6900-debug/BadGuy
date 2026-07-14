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

    [Header("Système de Stack (Quantité) 📦")]
    public bool isStackable = false;
    public int maxStackSize = 99;

    [Header("Paramètres d'Équipement")]
    public bool isEquippable;
    public bool isConsumable;

    [Header("Consommables Standard 🍔")]
    public int healAmount;
    public float speedBoostMultiplier;
    public float buffDuration;

    [Header("Effets de Drogue Spéciaux 😵")]
    public bool isDrugWithComedown;
    public float comedownDuration;
    public float comedownSpeedMultiplier = 0.5f;
    public bool invertControlsDuringComedown = true;

    public enum Rarity { Basique, PeuCourant, Rare, Legendaire }
    public Rarity rarity;

    [Header("Système de Vêtements / RPG 👕")]
    public bool isClothing;

    public enum ClothingSlot { Tete, Torse, Jambes, Pieds }
    public ClothingSlot clothingSlot;

    public int armorBonus;
    public float speedBonus;

    [Header("Système d'Infiltration 🥷")]
    public bool isMask;

    [Header("Système d'Armes 🔫")]
    public bool isWeapon;
    public int damage;
    public int maxAmmo = 12;
    public float fireRate = 0.5f;
    public GameObject weaponPrefab;
    public GameObject bulletPrefab;
}