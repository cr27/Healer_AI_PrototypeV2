using UnityEngine;

public class HealerArena : MonoBehaviour
{
    [Header("References")]
    public HealerAgent healerAgent;
    public Health healerHealth;
    public Health tankHealth;
    public DamageTicker damageTicker;

    [Header("Spawn Settings")]
    public Transform healerSpawn;
    public Transform tankSpawn;

    void Awake()
    {
        if (healerAgent != null) healerAgent.arena = this;
        if (damageTicker != null && tankHealth != null) damageTicker.target = tankHealth;
    }

    public void ResetArena()
    {
        // Reset health
        if (healerHealth != null) healerHealth.ResetHealth();
        if (tankHealth != null) tankHealth.ResetHealth();

        // Respawn positions & rotations
        if (healerAgent != null && healerSpawn != null)
        {
            healerAgent.transform.SetPositionAndRotation(healerSpawn.position, healerSpawn.rotation);
        }
        if (tankHealth != null && tankSpawn != null)
        {
            tankHealth.transform.SetPositionAndRotation(tankSpawn.position, tankSpawn.rotation);
        }

        // Ensure DamageTicker points at the tank
        if (damageTicker != null && tankHealth != null) damageTicker.target = tankHealth;
    }
}
