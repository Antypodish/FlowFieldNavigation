using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildPathDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] UIDocument _debugUIDoc;
    [SerializeField] Material _costFieldDebugMaterial;
    [SerializeField] Material _integrationFieldDebugMaterial;
    [SerializeField] Material _portalSequenceDebugMaterial;
    [SerializeField] Material _losDebugMaterial;
    [SerializeField] Material _targetDebugMaterial;
    FlowFieldAgent _agentToDebug;

    Toggle _debugModeToggle;
    Toggle _costFieldToggle;
    Toggle _flowFieldToggle;
    Toggle _integrationFieldToggle;
    Toggle _portalSequenceToggle;
    Toggle _losPassToggle;

    BuildCostFieldDebugger _costDebugger;
    BuildIntegrationFieldDebugger _integrationFieldDebugger;
    BuildPortalSequenceDebugger _portalSequenceDebugger;
    BuildLOSDebugger _losDebugger;
    BuildTargetDebugger _targetDebugger;

    const float _costOffset = 0.001f;
    const float _targetOffset = 0.005f;
    const float _intOffset = 0.002f;
    const float _losOffset = 0.003f;
    const float _portaltOffset = 0.004f;
    const float _flowOffset = 0.004f;
    private void Start()
    {
        VisualElement root = _debugUIDoc.rootVisualElement;
        _debugModeToggle = root.Q<Toggle>("DebugModeToggle");
        _costFieldToggle = root.Q<Toggle>("CostFieldToggle");
        _flowFieldToggle = root.Q<Toggle>("FlowFieldToggle");
        _integrationFieldToggle = root.Q<Toggle>("IntegrationFieldToggle");
        _portalSequenceToggle = root.Q<Toggle>("PortalSequenceToggle");
        _losPassToggle = root.Q<Toggle>("LOSPassToggle");

        _costDebugger = new BuildCostFieldDebugger(_pathfindingManager, _costFieldDebugMaterial);
        _integrationFieldDebugger = new BuildIntegrationFieldDebugger(_pathfindingManager, _integrationFieldDebugMaterial);
        _portalSequenceDebugger = new BuildPortalSequenceDebugger(_pathfindingManager, _portalSequenceDebugMaterial);
        _losDebugger = new BuildLOSDebugger(_pathfindingManager, _losDebugMaterial);
        _targetDebugger = new BuildTargetDebugger(_pathfindingManager, _targetDebugMaterial);
    }
    private void Update()
    {
        FlowFieldUtilities.DebugMode = _debugModeToggle.value;
        if (!FlowFieldUtilities.DebugMode) { return; }
        _targetDebugger.Debug(_agentToDebug, _targetOffset);
        if (_costFieldToggle.value && !_integrationFieldToggle.value) { _costDebugger.Debug(_agentToDebug, _costOffset); }
        if (_integrationFieldToggle.value) { _integrationFieldDebugger.Debug(_agentToDebug, _intOffset); }
        if (_portalSequenceToggle.value) { _portalSequenceDebugger.Debug(_agentToDebug, _portaltOffset); }
        if(_losPassToggle.value) { _losDebugger.Debug(_agentToDebug, _losOffset); }
    }
    public void SetAgentToDebug(FlowFieldAgent agent)
    {
        _agentToDebug = agent;
    }
}
