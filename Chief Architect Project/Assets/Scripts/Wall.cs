using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class Wall : MonoBehaviour
{
    [Header("World-space endpoints")]
    public Vector3 startPoint; // one side of wall
    public Vector3 endPoint; // other side of wall

    

    [Header("Multi-story")]
    public int story = 0; // 0 = ground floor, 1 = second floor, etc.
    public float storyHeight = 3f; // how tall each story is



    [HideInInspector] public List<Wall> neighbors = new();
    [HideInInspector] public List<Room> attachedRooms = new();

    [Header("Wall Dimensions")]
    [SerializeField] protected float length = 5f;
    [SerializeField] protected float height = 3f;
    [SerializeField] protected float thickness = 0.2f;

    public virtual float Length => length;
    public virtual float Height => height;
    public virtual float Thickness => thickness;


    // onEnable runs once, making sure the endpoints are correct when its initalially placed into Unity
    void OnEnable()
    {
       RecalculateEndpoints();
    }

    // Editor only: Continuously recalculates the endpoints every frame so when teh walls move, the endpoints move with them
    #if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying)
                RecalculateEndpoints();

      
        }
    #endif


    // Moves endpoints based on where the wall is 
    public void RecalculateEndpoints()
    {
        Vector3 right = transform.right;
        float half = length * 0.5f;

        startPoint = transform.position - right * half;
        endPoint = transform.position + right * half;
    }

  
    // Visually draws two circles that represent the endpoints
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startPoint, 0.12f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(endPoint, 0.12f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPoint, endPoint);
    }


}

