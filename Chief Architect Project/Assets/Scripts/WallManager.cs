using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DoorDuplicatePolicy // used when prompted to replace doors with windows or walls when duplicating floors
{
    KeepDoor,
    ReplaceWithWall,
    ReplaceWithWindow
}


[ExecuteAlways] // <-- Unity will run this in edit and play mode
public class WallManager : MonoBehaviour
{
    // ---- WALLS ----
    private Dictionary<int, List<Wall>> wallsByStory = new(); // maps story numbers to a list of walls on that floor
    public float snapDistance = 0.5f;

    private Dictionary<Vector3, List<Vector3>> graph = new(); // stores a graph of wall endpoints which is used to figure out room detection and loops
    private GameObject floor;

    private Wall lastPlacedWall; // Used when a room is made on the first floor with no doors. The last placed wall is replaced by a door automatically if this happens


    // ---------------- ROOMS ----------------
    public Transform roomParent; 
    List<Room> rooms = new List<Room>(); // Holds all room objects

    [Header("Door Replacement Prefabs")]
    public GameObject wallReplacementPrefab;
    public GameObject windowReplacementPrefab;
    public GameObject doorReplacementPrefab;


#if UNITY_EDITOR
    void OnEnable() // runs when wallManager is enabled/clicked on
    {
        EditorApplication.update += EditorTick;
    }

    void OnDisable() // is used to prevent unncessary updates when wallManager is not enabled/clicked on
    {
        EditorApplication.update -= EditorTick;
    }

    void EditorTick() // runs every frame in the editor, is used to make everything accurate in the editor and changes are made to walls or rooms
    {
       
         if (Selection.activeGameObject != null)
         {
            Wall wall = Selection.activeGameObject.GetComponent<Wall>();
            if (wall != null)
            {
                lastPlacedWall = wall;
            }
         }

        if (Application.isPlaying)
            return;

        if (Selection.activeTransform == null)
            return;

        Wall movedWall = Selection.activeTransform.GetComponent<Wall>();
        if (movedWall == null)
            return;

        CollectAllWalls();

        foreach (var kvp in wallsByStory)
        {
            List<Wall> floorWalls = kvp.Value;
            allWalls = floorWalls;

            SnapWalls();
            ConnectNeighbors();
            BuildGraph();
            UpdateRooms();
           
        }
    }
#endif

    // duplicates a story on top of itself. If a door is being duplicated to a higher floor, doorPolicy will handle if its gonna be a wall, window, or keep the door
    public void DuplicateRoomToStory(Room room, int targetStory, DoorDuplicatePolicy doorPolicy) 
    {
        foreach (var w in room.walls)
        {
            Door doorComp = w.GetComponent<Door>();
            GameObject copyGO;

            if (doorComp != null && targetStory > 0)
            {
                switch (doorPolicy)
                {
                    case DoorDuplicatePolicy.ReplaceWithWall:
                        copyGO = Instantiate(GetWallReplacementPrefab(w), w.transform.parent);
                        SetReplacementScale(copyGO, w, WallType.Wall, targetStory);
                        break;

                    case DoorDuplicatePolicy.ReplaceWithWindow:
                        copyGO = Instantiate(GetWindowReplacementPrefab(w), w.transform.parent);
                        SetReplacementScale(copyGO, w, WallType.Window, targetStory);
                        break;

                    default:
                        copyGO = Instantiate(w.gameObject, w.transform.parent);
                        break;
                }
            }
            else
            {
                copyGO = Instantiate(w.gameObject, w.transform.parent);
            }

            // Match position and rotation
            copyGO.transform.position = w.transform.position;
            copyGO.transform.rotation = w.transform.rotation;

            // Move it up to the correct story
            Wall copyWall = copyGO.GetComponent<Wall>();
            copyWall.story = targetStory;
            Vector3 pos = copyWall.transform.position;
            pos.y = targetStory * copyWall.storyHeight + copyWall.Height * 0.5f;
            copyWall.transform.position = pos;

            // Update endpoints and rotation
            copyWall.RecalculateEndpoints();
            ApplyTransformFromEndpoints(copyWall);
        }

        CollectAllWalls();
        UpdateRooms();
    }

   
    /// Adjusts the scale of a replacement prefab to match the original wall or door dimensions.
    private enum WallType { Wall, Window }

    private void SetReplacementScale(GameObject replacement, Wall original, WallType type, int targetStory)
    {
        Wall replWall = replacement.GetComponent<Wall>();
        if (replWall == null) return;

        float storyHeight = original.storyHeight;

        // Width: match original wall/door length
        Vector3 dir = original.endPoint - original.startPoint;
        float length = dir.magnitude;
        Vector3 scale = replacement.transform.localScale;

        switch (type)
        {
            case WallType.Wall:
                scale.x = length;          // along the wall direction
                scale.y = storyHeight;     // full story height
                scale.z = original.transform.localScale.z; // keep depth
                break;

            case WallType.Window:
                scale.x = 3.6f;
                scale.y = 1.55f; // smaller than full story
                scale.z = 0.2f;
                break;
        }

        replacement.transform.localScale = scale;
    }


    // ---------------- WALL COLLECTION ----------------
    private List<Wall> allWalls = new();
    void CollectAllWalls()
    {
        wallsByStory.Clear();

        foreach (var w in FindObjectsOfType<Wall>())
        {
            int story = w.story;

            if (!wallsByStory.ContainsKey(story))
                wallsByStory[story] = new List<Wall>();

            wallsByStory[story].Add(w);
        }
    }

    // ---------------- SNAPPING ----------------
    void SnapWalls() 
    {
        foreach (Wall w in allWalls)
        {
            Vector3 originalStart = w.startPoint;
            Vector3 originalEnd = w.endPoint;

            Vector3 snappedStart = SnapToClosestPoint(originalStart, w);
            Vector3 snappedEnd = SnapToClosestPoint(originalEnd, w);

            if (snappedStart == snappedEnd)
            {
                float ds = Vector3.Distance(snappedStart, originalStart);
                float de = Vector3.Distance(snappedEnd, originalEnd);

                if (ds < de) snappedEnd = originalEnd;
                else snappedStart = originalStart;
            }

            if (snappedStart == originalStart && snappedEnd == originalEnd)
                continue;

            w.startPoint = Quantize(snappedStart);
            w.endPoint = Quantize(snappedEnd);

            ApplyTransformFromEndpoints(w);
        }
    }

    // actually moves the wall in the scene to match the start and end points of the other wall.
    void ApplyTransformFromEndpoints(Wall w)
    {
        Vector3 dir = w.endPoint - w.startPoint;
        w.transform.position = (w.startPoint + w.endPoint) * 0.5f;

        if (dir.sqrMagnitude > 0.0001f)
            w.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
    }

    // takes in a single point and matches it with the closest other point within snapping distance. Returns the new position of that point
    Vector3 SnapToClosestPoint(Vector3 point, Wall self)
    {
        float bestDist = snapDistance;
        Vector3 best = point;

        foreach (Wall w in allWalls)
        {
            if (w == self) continue;

            float d1 = Vector3.Distance(point, w.startPoint);
            if (d1 < bestDist)
            {
                bestDist = d1;
                best = w.startPoint;
            }

            float d2 = Vector3.Distance(point, w.endPoint);
            if (d2 < bestDist)
            {
                bestDist = d2;
                best = w.endPoint;
            }
        }

        return best;
    }

    // Makes sure my walls actaully lines up, without this there could be tiny gaps in between my walls
    Vector3 Quantize(Vector3 v)
    {
        const float grid = 0.01f;
        return new Vector3(
            Mathf.Round(v.x / grid) * grid,
            Mathf.Round(v.y / grid) * grid,
            Mathf.Round(v.z / grid) * grid
        );
    }

    // ---------------- NEIGHBORS + GRAPH ----------------
    void ConnectNeighbors() // Loops through every wall and checks if other walls share endpoints. If so they are neighbors and are added to the list
    {
        foreach (Wall w1 in allWalls)
        {
            w1.neighbors.Clear();

            foreach (Wall w2 in allWalls)
            {
                if (w1 == w2) continue;

                if (Vector3.Distance(w1.startPoint, w2.startPoint) < 0.01f ||
                    Vector3.Distance(w1.startPoint, w2.endPoint) < 0.01f ||
                    Vector3.Distance(w1.endPoint, w2.startPoint) < 0.01f ||
                    Vector3.Distance(w1.endPoint, w2.endPoint) < 0.01f)
                {
                    w1.neighbors.Add(w2);
                }
            }
        }
    }

    // Builds the graphs where each endpoint is a node and each wall is an edge connecting two nodes. Graphs make it easy for room detection
    void BuildGraph()
    {
        graph.Clear();

        foreach (Wall w in allWalls)
        {
            Vector3 a = Quantize(w.startPoint);
            Vector3 b = Quantize(w.endPoint);

            AddEdge(a, b);
            AddEdge(b, a);
        }
    }

    // Connects endpoints (edges)
    void AddEdge(Vector3 from, Vector3 to)
    {
        if (!graph.ContainsKey(from))
            graph[from] = new List<Vector3>();

        if (!graph[from].Contains(to))
            graph[from].Add(to);
    }

    // ---------------- ROOM DETECTION ----------------

    // represents edges between two points in 3D space
    class DEdge { public Vector3 from; public Vector3 to; public DEdge(Vector3 f, Vector3 t) { from = f; to = t; } }

    List<List<Vector3>> FindAllRooms()
    {
        List<List<Vector3>> polys = new(); // List of all detected rooms
        HashSet<(Vector3, Vector3)> used = new(); // keeps track of wall edges that have already been processed

        foreach (var kvp in graph)
        {
            Vector3 start = kvp.Key;

            foreach (Vector3 next in kvp.Value)
            {
                if (used.Contains((start, next))) continue; // skip edge if already processed

                List<Vector3> loop = WalkRoom(start, next); // finds potiential rooms by traversing wall edges to find a closed loop

                if (loop != null && loop.Count >= 3)
                {
                    if (IsClockwiseXZ(loop)) loop.Reverse();

                    if (polys.Any(p => ArePolygonsEquivalent(p, loop))) continue; // avoids adding duplicates

                    polys.Add(loop);

                    for (int i = 0; i < loop.Count; i++) // once room is found we mark all edges as processed
                    {
                        Vector3 a = loop[i];
                        Vector3 b = loop[(i + 1) % loop.Count];
                        used.Add((a, b));
                    }
                }
            }
        }

        return polys;
    }

    // follows the edges of walls until it finds a room
    List<Vector3> WalkRoom(Vector3 start, Vector3 next)
    {
        List<Vector3> poly = new();
        Vector3 current = start;
        Vector3 incoming = next;

        poly.Add(start);

        int safety = 0;
        while (safety++ < 1000) // failsafe that prevents infinite loops | loops through the walls, if the walls dont create a room it returns null
        {
            poly.Add(incoming);

            if (incoming == start) break;

            Vector3 nextEdge = NextClockwiseEdge(current, incoming);
            if (nextEdge == Vector3.positiveInfinity) return null;

            current = incoming;
            incoming = nextEdge;
        }

        if (incoming != start) return null; // check if we successfully returned to the start, if not we return null because its not a room
        if (poly.Count < 4) return null;

        poly.RemoveAt(poly.Count - 1); // removes last vertex (which is the same as the first) to keep it clean

        foreach (var v in poly)
            if (float.IsNaN(v.x) || float.IsNaN(v.z) || Mathf.Abs(v.x) > 10000 || Mathf.Abs(v.z) > 10000)
                return null;

        return poly;
    }

    // Finds the next best wall to traverse by finding the smallest clockwise turn 
    Vector3 NextClockwiseEdge(Vector3 from, Vector3 to)
    {
        if (!graph.ContainsKey(to)) return Vector3.positiveInfinity;

        List<Vector3> neighbors = graph[to];
        if (neighbors.Count == 0) return Vector3.positiveInfinity;

        Vector3 incomingDir = (from - to); incomingDir.y = 0; incomingDir.Normalize();

        float bestAngle = float.MaxValue;
        Vector3 best = Vector3.positiveInfinity;

        foreach (var n in neighbors)
        {
            if (n == from) continue;

            Vector3 outDir = (n - to); outDir.y = 0; outDir.Normalize();

            float angle = Vector3.SignedAngle(incomingDir, outDir, Vector3.up);
            if (angle < 0) angle += 360f;

            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = n;
            }
        }

        if (best == Vector3.positiveInfinity && neighbors.Count == 1)
            return neighbors[0];

        return best;
    }

    float AngleCW(Vector3 from, Vector3 to)
    {
        float angle = Vector3.SignedAngle(from, to, Vector3.up);
        if (angle < 0) angle += 360f;
        return angle;
    }

    bool ArePolygonsEquivalent(List<Vector3> a, List<Vector3> b, float tolerance = 0.001f)
    {
        if (a == null || b == null || a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            bool found = false;
            foreach (var vb in b)
                if (Vector3.Distance(a[i], vb) < tolerance) { found = true; break; }

            if (!found) return false;
        }

        return true;
    }

    void UpdateRooms()
    {
        // Clear existing rooms first
        foreach (var room in rooms) DestroyRoom(room);
        rooms.Clear();

        // Process rooms per story
        foreach (var kvp in wallsByStory) // kvp.Key = story, kvp.Value = walls
        {
            allWalls = kvp.Value;
            BuildGraph();

            var polys = FindAllRooms();

            foreach (var poly in polys)
            {
                Room r = new Room();
                r.polygon = poly;
                r.walls = CollectWallsFromLoop(poly);
                rooms.Add(r);

                CreateFloorAndCeiling(r);
            }
        }

        ValidateGroundFloorDoors();

    }


    void ValidateGroundFloorDoors()
    {
        #if UNITY_EDITOR
            foreach (var room in rooms)
            {
                // Only ground floor
                if (!room.walls.Any(w => w.story == 0))
                    continue;

                bool hasDoor = room.walls.Any(w => w.GetComponent<Door>() != null);
                if (hasDoor)
                    continue;

                // No door detected
                EditorUtility.DisplayDialog(
                    "No Door Detected",
                    "No door was detected in this ground-floor room.\n\n" +
                    "A door has been automatically placed using the most recently added wall or window.",
                    "OK"
                );

                AutoInsertDoor(room);
                break; // prevent spam
            }
        #endif
    }

    void AutoInsertDoor(Room room)
    {
        #if UNITY_EDITOR
            Wall target = null;

            // Prefer last placed wall if it belongs to this room
            if (lastPlacedWall != null && room.walls.Contains(lastPlacedWall))
            {
                target = lastPlacedWall;
            }
            else
            {
                // Fallback: pick any wall/window in the room
                target = room.walls.FirstOrDefault();
            }

            if (target == null)
                return;

            Transform parent = target.transform.parent;
            Vector3 pos = target.transform.position;
            Quaternion rot = target.transform.rotation;

            DestroyImmediate(target.gameObject);

            // Instantiate a door prefab (reuse your existing door system)
            GameObject doorGO = Instantiate(doorReplacementPrefab, parent);


            doorGO.transform.position = pos;
            doorGO.transform.rotation = rot;

            // Ensure it has a Door component
            if (doorGO.GetComponent<Door>() == null)
                doorGO.AddComponent<Door>();
        #endif
    }


    bool RoomsAreSame(List<Room> oldRooms, List<List<Vector3>> newPolys)
    {
        if (oldRooms.Count != newPolys.Count) return false;

        foreach (var poly in newPolys)
        {
            bool found = false;
            foreach (var room in oldRooms)
            {
                if (ArePolygonsEquivalent(room.polygon, poly)) { found = true; break; }
            }
            if (!found) return false;
        }

        return true;
    }

    float PolygonAreaXZ(List<Vector3> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = poly[i];
            Vector3 b = poly[(i + 1) % poly.Count];
            area += (a.x * b.z) - (b.x * a.z);
        }
        return Mathf.Abs(area) * 0.5f;
    }

    void DestroyRoom(Room room)
    {
        if (room.floor != null) DestroyImmediate(room.floor);
        if (room.ceiling != null) DestroyImmediate(room.ceiling);
    }

    void CreateFloorAndCeiling(Room room)
    {
        if (roomParent == null)
            roomParent = this.transform;

        Wall w = room.walls[0];
        float floorY = w.story * w.storyHeight;
        float ceilingY = floorY + w.storyHeight;

        // --- FLOOR ---
        GameObject floorGO = new GameObject("Floor");
        floorGO.transform.parent = roomParent;

        MeshFilter mf = floorGO.AddComponent<MeshFilter>();
        MeshRenderer mr = floorGO.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        mf.mesh = mesh;

        Vector3[] verts = new Vector3[room.polygon.Count];
        for (int i = 0; i < verts.Length; i++)
            verts[i] = new Vector3(room.polygon[i].x, floorY, room.polygon[i].z);

        int[] tris = new int[(verts.Length - 2) * 3];
        for (int i = 0; i < verts.Length - 2; i++)
        {
            tris[i * 3] = 0;
            tris[i * 3 + 1] = i + 2;
            tris[i * 3 + 2] = i + 1;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mr.sharedMaterial = new Material(Shader.Find("Standard")) { color = Color.black };
        room.floor = floorGO;

        // --- CEILING ---
        GameObject ceilingGO = new GameObject("Ceiling");
        ceilingGO.transform.parent = floorGO.transform;

        MeshFilter cmf = ceilingGO.AddComponent<MeshFilter>();
        MeshRenderer cmr = ceilingGO.AddComponent<MeshRenderer>();
        Mesh cMesh = new Mesh();
        cmf.mesh = cMesh;

        Vector3[] cVerts = new Vector3[room.polygon.Count];
        for (int i = 0; i < cVerts.Length; i++)
            cVerts[i] = new Vector3(room.polygon[i].x, ceilingY, room.polygon[i].z);

        int[] cTris = new int[(cVerts.Length - 2) * 3];
        for (int i = 0; i < cVerts.Length - 2; i++)
        {
            cTris[i * 3] = 0;
            cTris[i * 3 + 1] = i + 1;
            cTris[i * 3 + 2] = i + 2;
        }

        cMesh.vertices = cVerts;
        cMesh.triangles = cTris;
        cMesh.RecalculateNormals();
        cMesh.RecalculateBounds();

        Material m = new Material(Shader.Find("Standard")) { color = Color.gray };
        m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
        cmr.sharedMaterial = m;

        room.ceiling = ceilingGO;
    }


    List<Wall> CollectWallsFromLoop(List<Vector3> loop)
    {
        List<Wall> result = new();

        for (int i = 0; i < loop.Count; i++)
        {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Count];

            foreach (Wall w in allWalls)
            {
                Vector3 ws = Quantize(w.startPoint);
                Vector3 we = Quantize(w.endPoint);

                if ((ws == a && we == b) || (ws == b && we == a))
                {
                    result.Add(w);
                    break;
                }
            }
        }

        return result;
    }

    bool IsClockwiseXZ(List<Vector3> poly)
    {
        float sum = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = poly[i];
            Vector3 b = poly[(i + 1) % poly.Count];
            sum += (b.x - a.x) * (b.z + a.z);
        }
        return sum > 0f;
    }

#if UNITY_EDITOR
[CustomEditor(typeof(WallManager))]
public class WallManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WallManager wm = (WallManager)target;

        if (wm.rooms == null || wm.rooms.Count == 0)
        {
            EditorGUILayout.HelpBox("No rooms detected to duplicate.", MessageType.Info);
            return;
        }

        int highestStory = wm.rooms
            .SelectMany(r => r.walls)
            .Max(w => w.story);

        int maxStory = 10;

        if (highestStory >= maxStory - 1)
        {
            EditorGUILayout.HelpBox("Maximum number of stories reached.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button("Add Story on Top"))
        {
            EditorApplication.delayCall += () =>
            {
                DoorDuplicatePolicy policy = DoorDuplicatePolicy.KeepDoor;

                bool hasDoorsOnTopStory = wm.rooms.Any(r =>
                    r.walls.Any(w => w.story == highestStory && w.GetComponent<Door>() != null)
                );

                if (hasDoorsOnTopStory)
                {
                    try
                    {
                        policy = AskUserAboutDoors(highestStory + 1);
                    }
                    catch
                    {
                        return; // user cancelled
                    }
                }

                var roomsToDuplicate = wm.rooms
                    .Where(r => r.walls.Any(w => w.story == highestStory))
                    .ToList();

                foreach (var room in roomsToDuplicate)
                {
                    wm.DuplicateRoomToStory(room, highestStory + 1, policy);
                }
            };
        }
    }
}
#endif


#if UNITY_EDITOR
public static DoorDuplicatePolicy AskUserAboutDoors(int targetStory)
{
    int result = EditorUtility.DisplayDialogComplex(
        "Door Detected",
        $"A door was detected while duplicating to story {targetStory}.\n\n" +
        "Doors above the ground floor usually become windows.\n\n" +
        "What do you want to do?",
        "Replace with Window",
        "Cancel",
        "Replace with Wall"
    );

    return result switch
    {
        0 => DoorDuplicatePolicy.ReplaceWithWindow,
        2 => DoorDuplicatePolicy.ReplaceWithWall,
        _ => throw new System.OperationCanceledException()
    };
}
#endif



    bool RoomContainsDoors(Room room)
    {
        foreach (var w in room.walls)
        {
            Debug.Log($"{w.name} | Wall type = {w.GetType()} | Has Door comp = {w.GetComponent<Door>() != null}");
            if (w is Door)
                return true;
        }
        return false;

       


    }

 

    GameObject GetWallReplacementPrefab(Wall original)
    {
        return wallReplacementPrefab;
    }

    GameObject GetWindowReplacementPrefab(Wall original)
    {
        return windowReplacementPrefab;
    }




}

