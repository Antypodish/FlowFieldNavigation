# FlowFieldPathfinding
My Flow Field implementation to use in Unity(Monobehaviours).

Flow Field pathfinding is used for navigation of huge number of agents. Some of the videogames using this pathfinding approach is: Supreme Commander 2

Currnet Work: Rewriting the Debugging system since multithreading broke the old one, and underperformant code.

TODO's
1 - Pathfinding with moving destinations. (Easy)
2 - Local Avoidance usin Boids algorithm. (Normal)
3 - In order to make "real" walls, a 2d collision system, exclusive to pathfinding, will be developed. (Unknown)
4 - Path smoothing (Normal).
5 - Imlementing a heightmap to support 2.5d terrain.

Far Future Plans:
1 - Rewriting the system using DOTS, specifically ECS.
2 - Provide a "Performance" mode with less precise flowfields.


Known Issues:
1 - Ram usage is acceptable, but unnecesssarily high. I know the fix but it is not urgent.




