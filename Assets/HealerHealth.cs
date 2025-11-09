using UnityEngine;

/// <summary>
/// Simple helper to allow manual healing of the Healer (for testing),
/// and to keep naming consistent with the new Health API.
/// </summary>
public class HealerHealth : MonoBehaviour
{
    public Health health;
    public float testHealAmount = 10f;

    void Reset()
    {
        if (health == null) health = GetComponent<Health>();
    }

    // Optional manual test: press J to heal this object
    void Update()
    {
        if (health != null && Input.GetKeyDown(KeyCode.J))
        {
            health.ApplyHeal(testHealAmount);
        }
    }
}
