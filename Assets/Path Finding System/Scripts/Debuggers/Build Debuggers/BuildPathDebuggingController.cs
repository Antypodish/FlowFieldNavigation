using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildPathDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentSelectionController _agentSelectionController;
    [SerializeField] UIDocument _debugUIDoc;

    Toggle _debugModeToggle;
    Toggle _costFieldToggle;
    Toggle _flowFieldToggle;
    Toggle _integrationFieldToggle;
    Toggle _portalSequenceToggle;
    Toggle _losPassToggle;

    Label _agentCountLabel;

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
    }
    private void Update()
    {
        int newAgentCount = _pathfindingManager.GetAgentCount();
        if(newAgentCount != _agentCount)
        {
            _agentCount = newAgentCount;
            _agentCountLabel.text = newAgentCount.ToString();
        }
        

        FlowFieldUtilities.DebugMode = _debugModeToggle.value;
        if (!FlowFieldUtilities.DebugMode) { return; }
    }
}
