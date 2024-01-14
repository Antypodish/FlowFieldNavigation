using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ControlUIManager : MonoBehaviour
{
    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] AgentSelectionController _agentSelectionController;
    [SerializeField] UIDocument _debugUIDoc;

    Label _agentCountLabel;

    int _agentCount = 0;
    private void Start()
    {
        VisualElement root = _debugUIDoc.rootVisualElement;
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
    }
}
