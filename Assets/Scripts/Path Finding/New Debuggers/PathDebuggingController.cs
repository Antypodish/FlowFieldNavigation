using UnityEngine;
using UnityEngine.UIElements;

public class PathDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] UIDocument _debugUIDoc;
    [SerializeField] Material _costFieldDebugMaterial;
    [SerializeField] Material _integrationFieldDebugMaterial;
    FlowFieldAgent _agentToDebug;

    Toggle _debugModeToggle;
    Toggle _costFieldToggle;
    Toggle _flowFieldToggle;
    Toggle _integrationFieldToggle;
    Toggle _portalSequenceToggle;

    CostFieldDebugger _costDebugger;
    IntegrationFieldDebugger _integrationFieldDebugger;
    private void Start()
    {
        VisualElement root = _debugUIDoc.rootVisualElement;
        _debugModeToggle = root.Q<Toggle>("DebugModeToggle");
        _costFieldToggle = root.Q<Toggle>("CostFieldToggle");
        _flowFieldToggle = root.Q<Toggle>("FlowFieldToggle");
        _integrationFieldToggle = root.Q<Toggle>("IntegrationFieldToggle");
        _portalSequenceToggle = root.Q<Toggle>("PortalSequenceToggle");

        _costDebugger = new CostFieldDebugger(_pathfindingManager, _costFieldDebugMaterial);
        _integrationFieldDebugger = new IntegrationFieldDebugger(_pathfindingManager, _integrationFieldDebugMaterial);
    }
    private void Update()
    {
        FlowFieldUtilities.DebugMode = _debugModeToggle.value;
        if (!FlowFieldUtilities.DebugMode) { return; }
        if (_costFieldToggle.value && !_integrationFieldToggle.value) { _costDebugger.Debug(_agentToDebug); }
        if (_integrationFieldToggle.value) { _integrationFieldDebugger.Debug(_agentToDebug); }
    }
    public void SetAgentToDebug(FlowFieldAgent agent)
    {
        _agentToDebug = agent;
    }
}
