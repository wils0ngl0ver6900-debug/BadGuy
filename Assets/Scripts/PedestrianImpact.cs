using UnityEngine;

// Calcule la force d'éjection à appliquer à un piéton percuté par un véhicule, calibrée
// pour viser une vraie distance au sol (pas juste "une force qui a l'air forte") — via la
// formule de portée d'un projectile : distance = v² × sin(2θ) / g.
// Utilisé par CarController et DrugClientNPC (et tout autre script qui percute un piéton).
public static class PedestrianImpact
{
    // Vitesse du véhicule (m/s) en dessous de laquelle on vise la distance minimale, et
    // au-dessus de laquelle on vise la distance maximale. Ajuste selon le ressenti voulu —
    // 30 m/s correspond à peu près à la vitesse max typique de ces voitures.
    private const float minSpeedForEjection = 3f;
    private const float maxSpeedForEjection = 30f;

    private const float minEjectionDistance = 2f;
    private const float maxEjectionDistance = 5f;

    // Angle de lancer. Un angle plus élevé donne un piéton qui décolle plus haut avant de
    // retomber (plus spectaculaire), un angle plus bas le fait davantage glisser au sol.
    private const float launchAngleDegrees = 40f;

    // vehicleSpeed : magnitude de la vitesse du véhicule au moment de l'impact (m/s).
    // horizontalDirection : direction horizontale de l'éjection (généralement la vitesse
    // du véhicule au moment de l'impact, normalisée).
    public static Vector3 CalculateEjectionVelocity(float vehicleSpeed, Vector3 horizontalDirection)
    {
        float speedFactor = Mathf.InverseLerp(minSpeedForEjection, maxSpeedForEjection, vehicleSpeed);
        float targetDistance = Mathf.Lerp(minEjectionDistance, maxEjectionDistance, speedFactor);

        float angleRad = launchAngleDegrees * Mathf.Deg2Rad;
        float gravity = Mathf.Abs(Physics.gravity.y);

        // v = racine(distance × g / sin(2θ))
        float launchSpeed = Mathf.Sqrt(targetDistance * gravity / Mathf.Sin(2f * angleRad));

        Vector3 dir = horizontalDirection.sqrMagnitude > 0.001f ? horizontalDirection.normalized : Vector3.forward;
        Vector3 horizontalPart = dir * launchSpeed * Mathf.Cos(angleRad);
        Vector3 verticalPart = Vector3.up * launchSpeed * Mathf.Sin(angleRad);

        return horizontalPart + verticalPart;
    }

    // Note : TargetHealth.TemporaryRagdoll applique ce vecteur en ForceMode.Impulse sur
    // chaque Rigidbody d'os (masse propre à chacun) — la distance réelle parcourue variera
    // donc un peu selon la masse des os et le terrain, cette formule donne la bonne
    // TENDANCE (plus la voiture va vite, plus loin le piéton part) plutôt qu'un mètre exact
    // garanti à chaque impact.
}