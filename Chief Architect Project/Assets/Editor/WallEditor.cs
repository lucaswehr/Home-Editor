using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Wall))]
public class WallEditor : Editor
{
    void OnSceneGUI()
    {
        Wall w = (Wall)target;

        EditorGUI.BeginChangeCheck();

        Vector3 newStart =
            Handles.PositionHandle(w.startPoint, Quaternion.identity);
        Vector3 newEnd =
            Handles.PositionHandle(w.endPoint, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(w, "Move Wall Endpoint");

            // Only move one endpoint at a time
            if ((newStart - w.startPoint).sqrMagnitude >
                (newEnd - w.endPoint).sqrMagnitude)
            {
                w.startPoint = newStart;
            }
            else
            {
                w.endPoint = newEnd;
            }

            // Rebuild transform
            w.transform.position = (w.startPoint + w.endPoint) * 0.5f;
            Vector3 dir = w.endPoint - w.startPoint;

            if (dir.sqrMagnitude > 0.0001f)
            {
                w.transform.rotation =
                    Quaternion.FromToRotation(Vector3.right, dir.normalized);
            }
        }
    }

}
