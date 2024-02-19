using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FlowFieldNavigation;
public class AgentSelectionController : MonoBehaviour
{
    List<FlowFieldAgent> _selectedAgents = new List<FlowFieldAgent>();
    [HideInInspector] public FlowFieldAgent DebuggableAgent;

    [SerializeField] int _startingAgentCount;
    [SerializeField] GameObject _agentPrefab;
    [SerializeField] FlowFieldNavigationManager _navigationManager;
    [SerializeField] EditorDebuggingController _navigationDebugger;
    [SerializeField] Material _normalAgentMaterial;
    [SerializeField] Material _selectedAgentMaterial;
    [SerializeField] Image _selectionBox;

    AgentBoundSelector _agentSelector;
    AgentFactory _agentFactory;
    CostEditController _costEditController;
    ControllerState _state;

    int _agentsToCreate = 0;
    private void Start()
    {
        _agentsToCreate = _startingAgentCount;
        _agentSelector = new AgentBoundSelector(_selectionBox);
        _agentFactory = new AgentFactory(_agentPrefab, _navigationManager);
        _costEditController = new CostEditController(_navigationManager);
        _state = ControllerState.SingleSelection;
    }
    private void Update()
    {
        _navigationDebugger.AgentToDebug = DebuggableAgent;
        float lowerLimit = 150;
        float upperLimit = 350;
        for (int i = 0; i < _agentsToCreate; i++)
        {
            _agentFactory.AddAgent(new Vector3(Random.Range(lowerLimit, upperLimit), 10, Random.Range(lowerLimit, upperLimit)));
        }
        _agentsToCreate= 0;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            _state = ControllerState.SingleSelection;
            _agentSelector.ForceStopSelection();
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            _state = ControllerState.MultiSelection;
            _agentSelector.ForceStopSelection();
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            _state = ControllerState.AgentAddition;
            _agentSelector.ForceStopSelection();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            _state = ControllerState.CostEditUnwalkable;
            _agentSelector.ForceStopSelection();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            _state = ControllerState.CostEditWalkable;
            _agentSelector.ForceStopSelection();
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            for (int i = 0; i < _selectedAgents.Count; i++)
            {
                _navigationManager.Interface.SetStopped(_selectedAgents[i]);
            }
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            for (int i = 0; i < _selectedAgents.Count; i++)
            {
                _navigationManager.Interface.SetHoldGround(_selectedAgents[i]);
            }
        }
        if (_state == ControllerState.SingleSelection)
        {
            if (Input.GetMouseButtonDown(0))
            {
                FlowFieldAgent selectedAgent = _agentSelector.GetPointedObject();
                if(selectedAgent != null)
                {
                    DeselectAllAgents();
                    _selectedAgents.Add(selectedAgent);
                    SetMaterialOfAgents(_selectedAgents, _selectedAgentMaterial);
                    DebuggableAgent = selectedAgent;
                }
                
            }
        }
        else if (_state == ControllerState.MultiSelection)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _agentSelector.StartBoxSelection(Input.mousePosition);
            }
            if (Input.GetMouseButton(0))
            {
                _agentSelector.ContinueBoxSelection(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                DeselectAllAgents();
                _agentSelector.GetAgentsInBox(Input.mousePosition, Camera.main, _navigationManager.Interface.GetAllAgents(), _selectedAgents);
                SetMaterialOfAgents(_selectedAgents, _selectedAgentMaterial);
                if(_selectedAgents.Count > 0)
                {
                    DebuggableAgent = _selectedAgents[0];
                }
            }
        }
        else if(_state == ControllerState.AgentAddition)
        {
            if (Input.GetMouseButton(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 1000, 8))
                {
                    _agentFactory.AddAgent(hit.point);
                }
            }
        }
        else if(_state == ControllerState.CostEditUnwalkable)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _costEditController.SetObstacle();
            }
        }


        if (Input.GetMouseButtonDown(1) && _selectedAgents.Count != 0)
        {
            SetDestination();
        }
        if (Input.GetMouseButton(2) && _selectedAgents.Count != 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, float.PositiveInfinity, 8))
            {
                Transform agentTransform = _selectedAgents[0].transform;
                Vector3 pos = agentTransform.position;
                Vector3 hitpos = hit.point;
                pos.x = hitpos.x;
                pos.z = hitpos.z;
                agentTransform.position = pos;
            }
        }
        if (Input.GetKeyUp(KeyCode.Delete))
        {
            for(int i = _selectedAgents.Count - 1; i >= 0; i--)
            {
                FlowFieldAgent agent = _selectedAgents[i];
                //_navigationManager.Interface.RequestUnsubscription(agent);
                _selectedAgents[i] = _selectedAgents[_selectedAgents.Count - 1];
                _selectedAgents.RemoveAt(_selectedAgents.Count - 1);
                Destroy(agent.gameObject);
            }
        }
    }
    void SetDestination()
    {
        bool forceGroundDestination = Input.GetKey(KeyCode.LeftShift);
        List<FlowFieldAgent> agents = _selectedAgents;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (forceGroundDestination)
        {
            if (Physics.Raycast(ray, out hit, float.PositiveInfinity, 8))
            {
                Vector3 destination = hit.point;
                _navigationManager.Interface.SetDestination(agents, destination);
            }
            return;
        }
        if (Physics.Raycast(ray, out hit, float.PositiveInfinity, 1 | 8))
        {
            FlowFieldAgent agent = hit.collider.gameObject.GetComponent<FlowFieldAgent>();
            if (agent == null)
            {
                    Vector3 destination = hit.point;
                _navigationManager.Interface.SetDestination(agents, destination);
                return;
            }
            _navigationManager.Interface.SetDestination(agents, agent);
        }

    }
    void DeselectAllAgents()
    {
        for(int i = 0; i < _selectedAgents.Count; i++)
        {
            _selectedAgents[i].GetComponentInChildren<MeshRenderer>().material = _normalAgentMaterial;
        }
        _selectedAgents.Clear();
    }
    void SetMaterialOfAgents(List<FlowFieldAgent> agents, Material mat)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].GetComponentInChildren<MeshRenderer>().material = mat;
        }
    }
    enum ControllerState : byte
    {
        SingleSelection,
        MultiSelection,
        AgentAddition,
        CostEditUnwalkable,
        CostEditWalkable,
    };
}