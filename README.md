--
---------------------------
---- WORK IN PROGRESS ----
---------------------------


OVERVIEW:
----------------------------------

Home Editor (Unity)

I made a Unity prototype tool for creating simple floor plans by placing walls and automatically detecting rooms. The project focuses on geometry, graph traversal, and editor tooling rather than visuals.

Walls are represented as edges in a graph. Rooms are detected by walking this graph clockwise to find closed loops and converting those loops into polygonal room shapes. 

FEATURES:
----------------------------------

Wall placement and connection system

Graph-based representation of wall endpoints

Automatic room detection from closed wall loops

Clockwise edge traversal using signed angles

Duplicate room filtering with tolerance checks

Door and wall components

Debug visualization of wall graphs and detected rooms

Custom Unity editor tools for faster iteration

HOW IT WORKS:
-----------------------------------------

Each wall contributes two endpoints to a graph structure (point -> connected points).

The system selects an edge and walks the graph clockwise.

At each vertex, the next edge is chosen based on the smallest clockwise angle.

If the walk returns to the starting point, a closed polygon (room) is formed.

Invalid or duplicate polygons are discarded.

The remaining polygons are stored as Room objects and can be visualized for debugging.

Main Functions:

WallManager (graph + room detection)

WalkRoom (builds polygon loops)

NextClockwiseEdge (chooses next edge by angle)

ArePolygonsEquivalent (filters duplicates)

<img width="1103" height="496" alt="Chief Architect Project - Project - Windows, Mac, Linux - Unity 6 (6000 0 38f1)_ _DX11_ 2_9_2026 1_32_02 PM" src="https://github.com/user-attachments/assets/b98d3d9b-157a-4c76-afd3-7387b59c1bff" />
