using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
public class ProceduralTerrainDebugger : MonoBehaviour
{
    [SerializeField] TerrainGenerator _terrainGenerator;
    [SerializeField] bool _debugTiles;
    [SerializeField] bool _debugUnwalkableTiles;
    
    WalkabilityData _walkabilityData;
    //debuggers
    UnwalkableTileDebugger _unwalkableTileDebugger;
    TileDebugger _tileDebugger;

    private void Start()
    {
        _walkabilityData = _terrainGenerator.WalkabilityData;

        //innitialize debuggers
        _unwalkableTileDebugger = new UnwalkableTileDebugger(_walkabilityData, _terrainGenerator);
        _unwalkableTileDebugger.CreateMesh();
        _tileDebugger = new TileDebugger(_terrainGenerator);

    }
    private void OnDrawGizmos()
    {
        if (_debugTiles && _tileDebugger!=null) { _tileDebugger.DebugTiles(); }
        if (_debugUnwalkableTiles && _unwalkableTileDebugger != null) { _unwalkableTileDebugger.DebugUnwalkableTiles(); }
    }
    
}