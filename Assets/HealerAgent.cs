using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class HealerAgent : Agent
{
    [HideInInspector] public HealerArena arena;

    [Header("Scene Links")]
    [SerializeField] private Transform healerXform;
    [SerializeField] private Transform tankXform;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Rigidbody tankRb;
    [SerializeField] private Health healerHealth;
    [SerializeField] private Health tankHealth;

    [Header("Navigation / Follow")]
    [SerializeField] private NavMeshAgent nav;
    [SerializeField] private float repathInterval = 0.12f;
    [SerializeField] private float navMaxSpeed = 3.5f;
    [SerializeField] private float idealMin = 2.5f;
    [SerializeField] private float idealMax = 4.0f;

    [Header("Modes")]
    public bool Phase1SimpleFollow = false;     // rule-based follow only
    public bool Phase2RLPositioning = true;     // Branch0: 0 hold, 1 closer, 2 back
    public bool Phase3RLHealing = true;         // Branch1: 0 none, 1 heal

    [Header("Healing")]
    [SerializeField] private float healRange = 2.5f;
    [SerializeField] private float healAmount = 15f;
    [SerializeField] private float healCooldown = 1.0f;
    private float nextHealTime;

    [Header("Line of Sight")]
    [SerializeField] private LayerMask losBlockers = 0; // set to Nothing for open arena
    [SerializeField] private float eyeHeight = 1.6f;

    float nextRepath;
    bool pendingDone;                 // <-- defer EndEpisode() to decision step
    float pendingFinalReward;

    public override void Initialize()
    {
        if (healerXform == null) healerXform = transform;
        if (rb == null) rb = GetComponent<Rigidbody>();

        nav = GetComponent<NavMeshAgent>() ?? gameObject.AddComponent<NavMeshAgent>();
        nav.speed = navMaxSpeed;
        nav.stoppingDistance = idealMin;
        nav.angularSpeed = 720f;
        nav.acceleration = 16f;
        nav.updateRotation = true;
        nav.autoBraking = true;

        if (tankXform != null && tankRb == null) tankRb = tankXform.GetComponent<Rigidbody>();

        nextHealTime = Time.time;
        pendingDone = false;
        pendingFinalReward = 0f;
    }

    public override void OnEpisodeBegin()
    {
        pendingDone = false;
        pendingFinalReward = 0f;
        nextHealTime = Time.time;

        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (nav != null) { nav.isStopped = false; nav.ResetPath(); nav.stoppingDistance = idealMin; }
    }

    void Update()
    {
        // Phase 1 simple follow (no ML)
        if (Phase1SimpleFollow && !Phase2RLPositioning && tankXform && nav)
        {
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                float d = HorizontalDistance(healerXform.position, tankXform.position);
                if (d > Mathf.Max(0.1f, nav.stoppingDistance * 0.95f)) { nav.isStopped = false; nav.SetDestination(tankXform.position); }
                else nav.isStopped = true;
            }
        }

        // DO NOT call EndEpisode() here. Flag it; finish on decision step.
        if (!pendingDone && tankHealth && tankHealth.CurrentHP <= 0f)
        {
            pendingDone = true;
            pendingFinalReward = -1.0f; // ally died
        }
    }

    // 12-float observation (stack 3–4): hp%, dx,dz, dist, relSpeed, LoS, inRange, nav path info, low flags
    public override void CollectObservations(VectorSensor s)
    {
        float tMax = Mathf.Max(1f, SafeMax(tankHealth));
        float hMax = Mathf.Max(1f, SafeMax(healerHealth));

        Vector3 hPos = healerXform ? healerXform.position : Vector3.zero;
        Vector3 tPos = tankXform ? tankXform.position : Vector3.zero;
        Vector3 d = tPos - hPos;

        float dx = Mathf.Clamp(d.x, -10f, 10f) / 10f;
        float dz = Mathf.Clamp(d.z, -10f, 10f) / 10f;
        float dist = new Vector2(d.x, d.z).magnitude;
        float distN = Mathf.Clamp01(dist / 10f);

        float relSpeedN = 0f;
        Vector2 dir2 = new Vector2(d.x, d.z);
        if (dir2.sqrMagnitude > 1e-6f)
        {
            Vector2 dirU = dir2.normalized;
            Vector2 hv = rb ? new Vector2(rb.velocity.x, rb.velocity.z) : Vector2.zero;
            Vector2 tv = tankRb ? new Vector2(tankRb.velocity.x, tankRb.velocity.z) : Vector2.zero;
            float closing = Vector2.Dot((tv - hv), dirU);
            relSpeedN = Mathf.Clamp(closing, -5f, 5f) / 5f;
        }

        bool los = HasLoS(hPos, tPos);
        bool inRange = dist <= healRange + 1e-3f;

        bool hasPath = nav && (nav.hasPath || (!nav.pathPending && nav.pathStatus != NavMeshPathStatus.PathInvalid));
        float pathLen = hasPath ? PathLen(nav) : 20f;
        float pathN = Mathf.Clamp01(pathLen / 20f);

        float tHpN = Mathf.Clamp01(SafeHP(tankHealth) / tMax);
        float hHpN = Mathf.Clamp01(SafeHP(healerHealth) / hMax);
        int allyLow = tHpN <= 0.70f ? 1 : 0;
        int selfLow = hHpN <= 0.40f ? 1 : 0;

        s.AddObservation(tHpN); s.AddObservation(hHpN);
        s.AddObservation(dx); s.AddObservation(dz);
        s.AddObservation(distN); s.AddObservation(relSpeedN);
        s.AddObservation(los ? 1f : 0f);
        s.AddObservation(inRange ? 1f : 0f);
        s.AddObservation(hasPath ? 1f : 0f);
        s.AddObservation(pathN);
        s.AddObservation(allyLow);
        s.AddObservation(selfLow);
    }

    // Branch0: 0 hold,1 closer,2 back | Branch1: 0 none,1 heal
    public override void OnActionReceived(ActionBuffers a)
    {
        // Finish episode on decision step (fixes GAE shapes)
        if (pendingDone)
        {
            if (pendingFinalReward != 0f) AddReward(pendingFinalReward);
            pendingDone = false; pendingFinalReward = 0f;
            EndEpisode();
            return;
        }

        if (tankXform == null || nav == null) return;

        if (Phase2RLPositioning && !Phase1SimpleFollow)
            ApplyMoveIntent(a.DiscreteActions[0]);

        if (Phase3RLHealing && a.DiscreteActions.Length > 1 && a.DiscreteActions[1] == 1)
            TryHeal();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        d[0] = Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? 2 : 0;
        if (d.Length > 1) d[1] = Input.GetKey(KeyCode.H) ? 1 : 0;
    }

    void ApplyMoveIntent(int intent)
    {
        Vector3 h = healerXform.position, t = tankXform.position;
        Vector3 flat = new Vector3(t.x - h.x, 0f, t.z - h.z);
        float dist = flat.magnitude;
        Vector3 dir = dist > 1e-4f ? flat / dist : Vector3.zero;

        switch (intent)
        {
            case 0:
                if (dist >= idealMin && dist <= idealMax) { nav.isStopped = true; }
                else
                {
                    nav.isStopped = false;
                    Vector3 hold = dist < idealMin ? h - dir * (idealMin - dist + 0.1f)
                                                   : h + dir * (dist - idealMax + 0.1f);
                    Repath(hold);
                }
                break;
            case 1:
                nav.isStopped = false;
                Repath(t - dir * Mathf.Lerp(idealMin, idealMax, 0.3f));
                break;
            case 2:
                nav.isStopped = false;
                Repath(h - dir * Mathf.Max(0.5f, (idealMax - dist) + 0.5f));
                break;
        }
    }

    void TryHeal()
    {
        if (Time.time < nextHealTime || tankHealth == null) return;

        float dist = HorizontalDistance(healerXform.position, tankXform.position);
        if (dist > healRange + 1e-3f || !HasLoS(healerXform.position, tankXform.position))
        { AddReward(-0.02f); nextHealTime = Time.time + 0.15f; return; }

        float healed = ApplyHealTo(tankHealth, healAmount);
        AddReward(healed > 0.001f ? +0.1f * (healed / healAmount) : -0.01f);
        nextHealTime = Time.time + healCooldown;
    }

    // helpers
    static float SafeHP(Health h) => h ? h.CurrentHP : 0f;
    static float SafeMax(Health h) => h ? h.MaxHP : 1f;
    bool HasLoS(Vector3 a, Vector3 b)
    {
        if (losBlockers == 0) return true;
        return !Physics.Linecast(a + Vector3.up * eyeHeight, b + Vector3.up * eyeHeight, losBlockers, QueryTriggerInteraction.Ignore);
    }
    void Repath(Vector3 target)
    {
        if (Time.time < nextRepath) return; nextRepath = Time.time + repathInterval;
        if (NavMesh.SamplePosition(target, out var hit, 2f, NavMesh.AllAreas)) nav.SetDestination(hit.position);
        else nav.SetDestination(target);
    }
    static float PathLen(NavMeshAgent agent)
    {
        if (!agent || agent.path == null || agent.path.corners == null || agent.path.corners.Length < 2) return 0f;
        float len = 0f; var cs = agent.path.corners; for (int i = 1; i < cs.Length; i++) len += Vector3.Distance(cs[i - 1], cs[i]); return len;
    }
    static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    static float ApplyHealTo(Health h, float amount)
    {
        if (!h || amount <= 0f) return 0f;
        var t = h.GetType();
        var m = t.GetMethod("ApplyHeal") ?? t.GetMethod("AddHP") ?? t.GetMethod("Heal");
        if (m != null && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(float))
        { var res = m.Invoke(h, new object[] { amount }); if (res is float f) return Mathf.Max(0f, f); }
        var pC = t.GetProperty("CurrentHP"); var pM = t.GetProperty("MaxHP");
        if (pC != null && pC.CanRead && pM != null && pM.CanRead)
        {
            float cur = Mathf.Clamp((float)pC.GetValue(h), 0f, (float)pM.GetValue(h));
            float max = (float)pM.GetValue(h); float nv = Mathf.Min(max, cur + amount); float delta = Mathf.Max(0f, nv - cur);
            if (pC.CanWrite) pC.SetValue(h, nv); return delta;
        }
        Debug.LogWarning("[HealerAgent] Could not apply heal: Health type lacks supported API.");
        return 0f;
    }
}
