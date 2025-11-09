using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("Health Settings")]
    public float MaxHP = 100f;
    public float CurrentHP = 100f;

    public bool IsDead => CurrentHP <= 0f;

    // Simple damage method
    public void TakeDamage(float amount)
    {
        if (IsDead) return;  // don't take further damage once dead
        CurrentHP -= amount;
        if (CurrentHP <= 0f)
        {
            CurrentHP = 0f;
            // Comment out any death/destroy logic to keep object healable
            // gameObject.SetActive(false);
            // Destroy(gameObject);
        }
    }

    // Simple heal method for compatibility with HealerAgent
    public float ApplyHeal(float amount)
    {
        if (amount <= 0f) return 0f;
        float prev = CurrentHP;
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        return CurrentHP - prev; // returns actual HP restored
    }

    // Optional reset if needed by HealerArena
    public void ResetHealth()
    {
        CurrentHP = MaxHP;
        gameObject.SetActive(true);
    }
}
