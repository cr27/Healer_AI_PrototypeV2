using UnityEngine;

public class DamageTicker : MonoBehaviour
{
    [Header("Damage Ticker")]
    public Health target;        // set to TankAlly's Health
    public float dps = 5f;       // damage per second
    public bool enabledOnStart = true;

    void Start()
    {
        enabled = enabledOnStart;
    }

    void Update()
    {
        if (!enabled || target == null) return;
        target.TakeDamage(dps * Time.deltaTime);
    }
}
