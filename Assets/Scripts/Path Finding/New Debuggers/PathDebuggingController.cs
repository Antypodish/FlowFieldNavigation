using UnityEngine;
using UnityEngine.UIElements;

public class PathDebuggingController : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] UIDocument _debugUIDoc;
    [SerializeField] Material _costFieldDebugMaterial;
    FlowFieldAgent _agentToDebug;

    Toggle _costFieldToggle;
    Toggle _flowFieldToggle;
    Toggle _integrationFieldToggle;
    Toggle _portalSequenceToggle;

    CostFieldDebugger _costDebugger;
    private void Start()
    {
        VisualElement root = _debugUIDoc.rootVisualElement;
        _costFieldToggle = root.Q<Toggle>("CostFieldToggle");
        _flowFieldToggle = root.Q<Toggle>("FlowFieldToggle");
        _integrationFieldToggle = root.Q<Toggle>("IntegrationFieldToggle");
        _portalSequenceToggle = root.Q<Toggle>("PortalSequenceToggle");

        _costDebugger = new CostFieldDebugger(_pathfindingManager, _costFieldDebugMaterial);
    }
    private void Update()
    {
        if (_costFieldToggle.value) { _costDebugger.Debug(_agentToDebug); }
    }
    public void SetAgentToDebug(FlowFieldAgent agent)
    {
        _agentToDebug = agent;
    }
}
