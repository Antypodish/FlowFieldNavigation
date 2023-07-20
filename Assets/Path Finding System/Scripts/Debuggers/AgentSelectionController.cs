using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngineInternal;

public class AgentSelectionController : MonoBehaviour
{
    public List<FlowFieldAgent> SelectedAgents;
    public FlowFieldAgent DebuggableAgent;

    [SerializeField] PathfindingManager _pathfindingManager;
    [SerializeField] Material _normalAgentMaterial;
    [SerializeField] Material _selectedAgentMaterial;
    [SerializeField] Image _selectionBox;

    AgentBoundSelector _agentSelector;
    ControllerState _state;
    private void Start()
    {
        _agentSelector = new AgentBoundSelector(_selectionBox);
        _state = ControllerState.Single;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            _state = _state == ControllerState.Single ? ControllerState.Multi : ControllerState.Single;
            _agentSelector.ForceStopSelection();
        }
        if (_state == ControllerState.Single)
        {
            if (Input.GetMouseButtonDown(0))
            {
                FlowFieldAgent selectedAgent = _agentSelector.GetPointedObject();
                if(selectedAgent != null)
                {
                    DeselectAllAgents();
                    SelectedAgents.Add(selectedAgent);
                    SetMaterialOfAgents(SelectedAgents, _selectedAgentMaterial);
                    DebuggableAgent = selectedAgent;
                }
                
            }
        }
        else
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
                _agentSelector.GetAgentsInBox(Input.mousePosition, Camera.main, _pathfindingManager.GetAllAgents(), SelectedAgents);
                SetMaterialOfAgents(SelectedAgents, _selectedAgentMaterial);
                if(SelectedAgents.Count == 1)
                {
                    DebuggableAgent = SelectedAgents[0];
                }
                else
                {
                    DebuggableAgent = null;
                }
            }
        }
        if (Input.GetMouseButtonDown(1) && SelectedAgents.Count != 0)
        {
            SetDestination();
        }
        if (Input.GetMouseButtonDown(2) && SelectedAgents.Count != 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, float.PositiveInfinity, 8))
            {
                Transform agentTransform = SelectedAgents[0].transform;
                Vector3 pos = agentTransform.position;
                Vector3 hitpos = hit.point;
                pos.x = hitpos.x;
                pos.z = hitpos.z;
                agentTransform.position = pos;
            }
        }
    }
    void SetDestination()
    {
        List<FlowFieldAgent> agents = SelectedAgents;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, float.PositiveInfinity, 8))
        {
            Vector3 destination = hit.point;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            Path newPath = _pathfindingManager.SetDestination(agents, destination);
            sw.Stop();
            UnityEngine.Debug.Log(sw.Elapsed.TotalMilliseconds);
            if (newPath == null) { return; }
            for (int i = 0; i < agents.Count; i++)
            {
                agents[i].SetPath(newPath);
            }
        }
    }
    void DeselectAllAgents()
    {
        for(int i = 0; i < SelectedAgents.Count; i++)
        {
            SelectedAgents[i].GetComponent<MeshRenderer>().material = _normalAgentMaterial;
        }
        SelectedAgents.Clear();
    }
    void SetMaterialOfAgents(List<FlowFieldAgent> agents, Material mat)
    {
        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].GetComponent<MeshRenderer>().material = mat;
        }
    }
    enum ControllerState : byte
    {
        Single,
        Multi
    };
}