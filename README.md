# FlowFieldNavigation
My navigation system based on Flow Field pathfinding and boid-like local avoidance techniques
Some games using these techniques include: Supreme Commander 2, Age of Empires 4

VERY IMPORTANT NOTE! System only works on Unity 2022 at the moment due to changes on packages such as Unit.Collections.

Aim of the project:
- Supporting high agent counts (Tens of thousands, if not hundereds of thousands)
- Efficient pathfinding for multiple agents with same goal
- Efficient walkability editing
- Local avoidance suited for crowd simulations (RTS games, swarm ai and stuff) in terms of gameplay

What you can do currently:
API is currently in development. So, user experience is not very convenient in some parts.
If you want to try stuff, I strongly suggest you to run the test scene inside "Assets/FlowFieldNaviagtion/Scenes".

What you can do in the test scene?
- You can add/remove agents
- You can assign paths for the agents
- You can also assign "Dynamic Paths" by right clickin on any agent. Selected agents will start to follow that agent.
- You can add obstacles in runtime. Paths are updated as you place obstacles.
- You can make agents "Stop" or "Hold Ground".
- You can change size, speed, and land offset of your agents in edit mode.

Performance notes:
- System currently supports 10's of thousands of agents. (Ryzen 5 5600)
- For lower end systems, system still support thousands of agents (I5 6500U)
- System designed multithreading in mind
- System uses Unity.Jobs, Unity.Collections, Unity.Mathematics, and Unity.Burst extensively.
- ALMOST ZERO GARBAGE COLLECTION

What's in development schedule? (Not in order)
- Better MonoBehaviour API (More functionality, and some performance improvements)
- New Non-MonoBehaviour API (A more performance oriented API)
- New Entity Component System API
- Contunious collisions for walls and agents
- Perfomance improvements on Movement System
- Polising on agent movement (Collision between stopeed agents and avoidance behaviour)
- Performance improvements on walkability editing (Especially on Island Field Reconstruction)
