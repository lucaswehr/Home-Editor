using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Room
{
    public List<Vector3> polygon = new(); // determines the rooms shape in the editor so that the floor and ceiling can be created based off that shape
    public List<Wall> walls = new(); // List of wall objects

    public GameObject floor;
    public GameObject ceiling;
    public bool dirty = true; // initially dirty


    public float Area { get; private set; }
    public Vector3 Center { get; private set; }
    public bool Clockwise { get; private set; }

    public void Compute()
    {
        Area = ComputeAreaXZ(polygon);
        Center = ComputeCenter(polygon);
        Clockwise = IsClockwiseXZ(polygon);
    }

    // Finds area of the room
    float ComputeAreaXZ(List<Vector3> poly)
    {
        float area = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 a = poly[i];
            Vector3 b = poly[(i + 1) % poly.Count];
            area += (a.x * b.z - b.x * a.z);
        }
        return Mathf.Abs(area) * 0.5f;
    }

    // finds center of room
    Vector3 ComputeCenter(List<Vector3> poly)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in poly)
            sum += p;
        return sum / poly.Count;
    }

    // makes sure that the floor,ceiling and wallsare oriented correctly, if some walls are counterclockwise and some are clockwise, it could mess up some algorithims
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
}
