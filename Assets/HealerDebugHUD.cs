using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

public class HealerDebugHUD : MonoBehaviour
{
    [Header("References")]
    public Agent agent;            // Drag your HealerAgent here
    public BehaviorParameters bp;  // Drag the same Agent's BehaviorParameters here
    public MonoBehaviour arena;    // (Optional) Your HealerArena if you want to show reset counts
    public Health healerHealth;    // Drag the Healer's Health
    public Health tankHealth;      // Drag the Tank's Health

    [Header("Display")]
    public bool show = true;
    public Vector2 anchor = new Vector2(10, 10);
    public int width = 360;
    public int lineHeight = 20;
    public int pad = 6;

    // rolling reward delta
    float _lastCumulative;
    float _deltaReward;
    float _deltaSMA;
    int _deltaCount;

    GUIStyle _label;

    void Awake()
    {
        if (agent == null) agent = GetComponent<Agent>();
        if (bp == null && agent != null) bp = agent.GetComponent<BehaviorParameters>();
        _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
    }

    void Update()
    {
        if (agent == null) return;

        // Compute reward delta since last frame (approx "last reward added")
        float cum = agent.GetCumulativeReward();
        _deltaReward = cum - _lastCumulative;
        _lastCumulative = cum;

        // Simple moving average of deltas
        _deltaSMA = (_deltaSMA * _deltaCount + _deltaReward) / Mathf.Max(1, _deltaCount + 1);
        _deltaCount = Mathf.Min(_deltaCount + 1, 400); // cap growth
    }

    void OnGUI()
    {
        if (!show || agent == null) return;

        int y = (int)anchor.y;
        Rect box = new Rect(anchor.x, anchor.y, width, 12 * lineHeight + pad * 2);
        GUI.Box(box, "Healer Debug HUD");

        y += lineHeight + pad;

        Draw($"Behavior: {bp?.BehaviorName ?? "(unknown)"}");
        Draw($"Episode #: {agent.CompletedEpisodes}");
        Draw($"Step: {agent.StepCount}  /  MaxStep: {agent.MaxStep}");
        Draw($"Cumulative Reward: {agent.GetCumulativeReward():0.000}");
        Draw($"Δ Reward (frame): {_deltaReward:+0.000;-0.000;+0.000}  |  SMA: {_deltaSMA:+0.000;-0.000;+0.000}");

        // Healths
        if (healerHealth != null)
            Draw($"Healer HP: {healerHealth.CurrentHP:0}/{healerHealth.MaxHP:0}");
        else
            Draw("Healer HP: (unassigned)");

        if (tankHealth != null)
            Draw($"Tank HP:   {tankHealth.CurrentHP:0}/{tankHealth.MaxHP:0}");
        else
            Draw("Tank HP: (unassigned)");

        // Vector observation size hint (from Behavior Parameters)
        if (bp != null)
        {
            var vecObsSize = bp.BrainParameters.VectorObservationSize; // available in ML-Agents 2.0.x
            Draw($"Obs Space Size (vector): {vecObsSize}");
        }

        // Arena info if you want (optional)
        if (arena != null)
            Draw($"Arena: {arena.name}");

        void Draw(string text)
        {
            GUI.Label(new Rect(anchor.x + pad, y, width - pad * 2, lineHeight), text, _label);
            y += lineHeight;
        }
    }
}
