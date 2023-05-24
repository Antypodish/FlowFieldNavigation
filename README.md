# FlowFieldPathfinding
My Flow Field implementation to use in Unity(Monobehaviours).

Flow Field pathfinding is used for navigation of huge number of agents. Some of the videogames using this pathfinding approach is: Supreme Commander 2, Starcraft 2
System is still in development.

Capabilities:
1 - Calculates flowfield only for necessary tiles.
2 - Field is editable. (At runtime, you can switch tiles between walkable/unwalkable)
3 - Flow field is being updated upon editing a tile related to it.
4 - System is of course multithreaded.

TODO's
1 - Pathfinding with moving destinations. (Easy)
2 - Expending flowfield for agents pushed out-of-track. (Easy)
3 - Local Avoidance usin Boids algorithm. (Normal)
4 - In order to make "real" walls, a 2d collision system, exclusive to pathfinding, will be developed. (Unknown)
5 - Optimization, optimization, optimization!! (Eternal)

Far Future Plans:
1 - Rewriting the system using DOTS, specifically ECS.
2 - Provide a "Performance" mode with less precise flowfields.


Known Issues:
1 - Ram usage is acceptable, but unnecesssarily high. This issue will be solved eventually by refactoring "Field Graph" data structure.
    Specifically, most NativeArray's will be replaced with UnsafeList's in order to be able to create nested data structures.
2 - Graph traversal in first stage of the pathfinding uses BFS. This is extremely inefficient for this kind of "sparse graphs".
    Later on, it will be replaced with A*.




