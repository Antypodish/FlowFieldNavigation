using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildPathDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentSelectionController _agentSelectionController;
    [SerializeField] UIDocument _debugUIDoc;
    [SerializeField] Material _costFieldDebugMaterial;
    [SerializeField] Material _integrationFieldDebugMaterial;
    [SerializeField] Material _portalSequenceDebugMaterial;
    [SerializeField] Material _losDebugMaterial;
    [SerializeField] Material _targetDebugMaterial;

    Toggle _debugModeToggle;
    Toggle _costFieldToggle;
    Toggle _flowFieldToggle;
    Toggle _integrationFieldToggle;
    Toggle _portalSequenceToggle;
    Toggle _losPassToggle;

    Label _agentCountLabel;

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

    int _agentCount = 0;
    private void Start()
    {
        VisualElement root = _debugUIDoc.rootVisualElement;
        _debugModeToggle = root.Q<Toggle>("DebugModeToggle");
        _costFieldToggle = root.Q<Toggle>("CostFieldToggle");
        _flowFieldToggle = root.Q<Toggle>("FlowFieldToggle");
        _integrationFieldToggle = root.Q<Toggle>("IntegrationFieldToggle");
        _portalSequenceToggle = root.Q<Toggle>("PortalSequenceToggle");
        _losPassToggle = root.Q<Toggle>("LOSPassToggle");
        _agentCountLabel = root.Q<Label>("AgentCountNumber");

        _costDebugger = new BuildCostFieldDebugger(_pathfindingManager, _costFieldDebugMaterial);
        _integrationFieldDebugger = new BuildIntegrationFieldDebugger(_pathfindingManager, _integrationFieldDebugMaterial);
        _portalSequenceDebugger = new BuildPortalSequenceDebugger(_pathfindingManager, _portalSequenceDebugMaterial);
        _losDebugger = new BuildLOSDebugger(_pathfindingManager, _losDebugMaterial);
        _targetDebugger = new BuildTargetDebugger(_pathfindingManager, _targetDebugMaterial);
    }
    private void Update()
    {
        int newAgentCount = _pathfindingManager.GetAgentCount();
        if(newAgentCount != _agentCount)
        {
            _agentCount = newAgentCount;
            _agentCountLabel.text = newAgentCount.ToString();
        }
        

        FlowFieldAgent agentToDebug = _agentSelectionController.DebuggableAgent;
        FlowFieldUtilities.DebugMode = _debugModeToggle.value;
        if (!FlowFieldUtilities.DebugMode) { return; }
        _targetDebugger.Debug(agentToDebug, _targetOffset);
        if (_costFieldToggle.value && !_integrationFieldToggle.value) { _costDebugger.Debug(agentToDebug, _costOffset); }
        if (_integrationFieldToggle.value) { _integrationFieldDebugger.Debug(agentToDebug, _intOffset); }
        if (_portalSequenceToggle.value) { _portalSequenceDebugger.Debug(agentToDebug, _portaltOffset); }
        if(_losPassToggle.value) { _losDebugger.Debug(agentToDebug, _losOffset); }
    }
}
