﻿using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngineInternal;

public class AgentSelectionController : MonoBehaviour
{
    [HideInInspector] public List<FlowFieldAgent> SelectedAgents;
    [HideInInspector] public FlowFieldAgent DebuggableAgent;

    [SerializeField] int _startingAgentCount;
    [SerializeField] GameObject _agentPrefab;
    [SerializeField] PathfindingManager _pathfindingManager;
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
        _agentFactory = new AgentFactory(_agentPrefab, _pathfindingManager);
        _costEditController = new CostEditController(_pathfindingManager);
        _state = ControllerState.SingleSelection;
    }
    private void Update()
    {
        for (int i = 0; i < _agentsToCreate; i++)
        {
            _agentFactory.AddAgent(new Vector3(10, 10, 10));
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
        if (_state == ControllerState.SingleSelection)
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
                _costEditController.SetUnwalkable();
            }
        }
        else if (_state == ControllerState.CostEditWalkable)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _costEditController.SetWalkable();
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
            
            _pathfindingManager.SetDestination(agents, destination);
            
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
        SingleSelection,
        MultiSelection,
        AgentAddition,
        CostEditUnwalkable,
        CostEditWalkable,
    };
}