//using UnityEngine;
//using System.Collections.Generic;

//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//[ExecuteAlways]
//public class WallGraphDebugger : MonoBehaviour
//{
//    public bool logSummary = true;
//    public bool logDanglingEndpoints = true;
//    public bool logRooms = true;

//    public WallManager manager;

//    public bool showGraph = true;
//    public bool showEndpoints = true;
//    public bool showRooms = true;

//    void OnDrawGizmos()
//    {
//        if (manager == null)
//            manager = FindObjectOfType<WallManager>();

//        if (manager == null)
//            return;

//        if (showGraph)
//        //    DrawGraph();

//        if (showEndpoints)
//            DrawEndpoints();

//        if (showRooms)
//            DrawRooms();
//    }

//    //void DrawGraph()
//    //{
//    //    Gizmos.color = Color.white;

//    //    foreach (var w in manager.allWalls)
//    //    {
//    //        Gizmos.DrawLine(w.startPoint, w.endPoint);
//    //    }
//    //}

//    void DrawEndpoints()
//    {
//        Dictionary<Vector3, int> degree = new();

//        foreach (var w in manager.allWalls)
//        {
//            Count(w.startPoint);
//            Count(w.endPoint);
//        }

//        void Count(Vector3 p)
//        {
//            Vector3 q = Quantize(p);
//            if (!degree.ContainsKey(q))
//                degree[q] = 0;
//            degree[q]++;
//        }

//        foreach (var kv in degree)
//        {
//            Gizmos.color = kv.Value == 2 ? Color.green : Color.red;
//            Gizmos.DrawSphere(kv.Key, 0.08f);

//            //if (logDanglingEndpoints && kv.Value != 2)
//            ////{
//            ////    Debug.LogWarning(
//            ////        $"[Dangling Endpoint] Degree={kv.Value} at {kv.Key}"
//            ////    );
//            //}
//        }

//        if (logSummary)
//        {
//            var rooms = GetRooms();
//            LogSummary(degree, rooms);
//        }
//    }


//    void DrawRooms()
//    {
//        var polys = GetRooms();
//        if (polys == null) return;

//        int i = 0;
//        foreach (var poly in polys)
//        {
//            Color c = Color.HSVToRGB((i * 0.17f) % 1f, 1f, 1f);
//            Gizmos.color = c;

//            float area = ComputeArea(poly);

//            //if (logRooms)
//            //{
//            //    Debug.Log(
//            //        $"[Room {i}] Points={poly.Count}, Area={area:F2}"
//            //    );
//            //}

//            for (int j = 0; j < poly.Count; j++)
//            {
//                Vector3 a = poly[j];
//                Vector3 b = poly[(j + 1) % poly.Count];
//                Gizmos.DrawLine(a + Vector3.up * 0.05f, b + Vector3.up * 0.05f);
//            }

//            i++;
//        }
//    }


//    List<List<Vector3>> GetRooms()
//    {
//        var method = manager.GetType()
//            .GetMethod("FindAllRooms", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

//        if (method == null)
//            return null;

//        return method.Invoke(manager, null) as List<List<Vector3>>;
//    }

//    Vector3 Quantize(Vector3 v)
//    {
//        const float grid = 0.01f;
//        return new Vector3(
//            Mathf.Round(v.x / grid) * grid,
//            Mathf.Round(v.y / grid) * grid,
//            Mathf.Round(v.z / grid) * grid
//        );
//    }

//    void LogSummary(
//    Dictionary<Vector3, int> degree,
//    List<List<Vector3>> rooms
//)
//    {
//        //Debug.Log($"[WallGraphDebugger] Walls: {manager.allWalls.Count}");

//        int dangling = 0;
//        foreach (var kv in degree)
//        {
//            if (kv.Value != 2)
//                dangling++;
//        }

//        //Debug.Log($"[WallGraphDebugger] Endpoints: {degree.Count}, Dangling: {dangling}");

//        //if (rooms != null)
//        //    Debug.Log($"[WallGraphDebugger] Rooms detected: {rooms.Count}");
//    }

//    float ComputeArea(List<Vector3> poly)
//    {
//        float area = 0f;

//        for (int i = 0; i < poly.Count; i++)
//        {
//            Vector3 a = poly[i];
//            Vector3 b = poly[(i + 1) % poly.Count];
//            area += (a.x * b.z - b.x * a.z);
//        }

//        return Mathf.Abs(area) * 0.5f;
//    }

//}

