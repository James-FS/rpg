using System.Collections.Generic;
using FogHarbor.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(EditableRoadPath))]
public sealed class EditableRoadPathEditor : Editor
{
    private const string MeshFolder = "Assets/Art/Environment/WarmOchreRoad/EditableMeshes";
    private int selectedPoint;

    private void OnEnable()
    {
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnUndoRedo()
    {
        if (target is EditableRoadPath path)
            Rebuild(path);
    }

    public override void OnInspectorGUI()
    {
        var path = (EditableRoadPath)target;
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (changed)
            Rebuild(path);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("在 Scene 视图点选控制点，再拖动位置或两侧宽度手柄。所有宽度均为道路的完整宽度（米）。", MessageType.Info);

        if (path.Points.Count > 0)
        {
            selectedPoint = Mathf.Clamp(selectedPoint, 0, path.Points.Count - 1);
            selectedPoint = EditorGUILayout.IntSlider("选中控制点", selectedPoint, 0, path.Points.Count - 1);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("在选中点后插入"))
                InsertPoint(path);
            using (new EditorGUI.DisabledScope(path.Points.Count <= 2))
            {
                if (GUILayout.Button("删除选中点"))
                    DeletePoint(path);
            }
        }

        if (GUILayout.Button("重新生成路面网格"))
            Rebuild(path);
    }

    private void InsertPoint(EditableRoadPath path)
    {
        Undo.RecordObject(path, "Insert Road Point");
        int index = Mathf.Clamp(selectedPoint, 0, path.Points.Count - 1);
        var a = path.Points[index];
        var b = path.Points[Mathf.Min(index + 1, path.Points.Count - 1)];
        Vector3 position = index == path.Points.Count - 1
            ? a.position + (a.position - path.Points[Mathf.Max(0, index - 1)].position)
            : Vector3.Lerp(a.position, b.position, 0.5f);
        float width = (a.width + b.width) * 0.5f;
        path.Points.Insert(index + 1, new EditableRoadPath.RoadPoint(position, width));
        selectedPoint = index + 1;
        Rebuild(path);
    }

    private void DeletePoint(EditableRoadPath path)
    {
        Undo.RecordObject(path, "Delete Road Point");
        path.Points.RemoveAt(Mathf.Clamp(selectedPoint, 0, path.Points.Count - 1));
        selectedPoint = Mathf.Clamp(selectedPoint, 0, path.Points.Count - 1);
        Rebuild(path);
    }

    private void OnSceneGUI()
    {
        var path = (EditableRoadPath)target;
        var points = path.Points;
        if (points.Count < 2)
            return;

        Handles.color = new Color(1f, 0.8f, 0.15f, 0.95f);
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 position = path.transform.TransformPoint(points[i].position);
            if (i > 0)
                Handles.DrawLine(path.transform.TransformPoint(points[i - 1].position), position);

            float size = HandleUtility.GetHandleSize(position) * 0.075f;
            if (Handles.Button(position, Quaternion.identity, size, size * 1.4f, Handles.DotHandleCap))
            {
                selectedPoint = i;
                Repaint();
            }
        }

        selectedPoint = Mathf.Clamp(selectedPoint, 0, points.Count - 1);
        var point = points[selectedPoint];
        Vector3 world = path.transform.TransformPoint(point.position);
        Vector3 before = path.transform.TransformPoint(points[Mathf.Max(0, selectedPoint - 1)].position);
        Vector3 after = path.transform.TransformPoint(points[Mathf.Min(points.Count - 1, selectedPoint + 1)].position);
        Vector3 direction = after - before;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.000001f)
            return;
        Vector3 sideways = Vector3.Cross(direction.normalized, Vector3.up);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Move Road Point");
            point.position = path.transform.InverseTransformPoint(moved);
            Rebuild(path);
            world = moved;
        }

        Handles.color = Color.yellow;
        Vector3 left = world + sideways * (point.width * 0.5f);
        Vector3 right = world - sideways * (point.width * 0.5f);
        Handles.DrawLine(left, right);
        Handles.Label(world + Vector3.up * HandleUtility.GetHandleSize(world) * 0.2f,
            $"{path.name}  点 {selectedPoint}  宽 {point.width:F2}m");

        EditorGUI.BeginChangeCheck();
        Vector3 movedLeft = Handles.Slider(left, sideways, HandleUtility.GetHandleSize(left) * 0.08f,
            Handles.CubeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Change Road Width");
            point.width = Mathf.Max(0.05f, 2f * Vector3.Dot(movedLeft - world, sideways));
            Rebuild(path);
        }

        EditorGUI.BeginChangeCheck();
        Vector3 movedRight = Handles.Slider(right, -sideways, HandleUtility.GetHandleSize(right) * 0.08f,
            Handles.CubeHandleCap, 0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(path, "Change Road Width");
            point.width = Mathf.Max(0.05f, 2f * Vector3.Dot(world - movedRight, sideways));
            Rebuild(path);
        }
    }

    public static void Rebuild(EditableRoadPath path)
    {
        if (path == null || path.Points.Count < 2)
            return;

        var centers = new List<Vector3>();
        var widths = new List<float>();
        for (int segment = 0; segment < path.Points.Count - 1; segment++)
        {
            Vector3 a = path.Points[segment].position;
            Vector3 b = path.Points[segment + 1].position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / path.SampleSpacing));
            for (int j = 0; j < steps; j++)
            {
                float t = (float)j / steps;
                centers.Add(Evaluate(path.Points, segment, t));
                widths.Add(Mathf.Lerp(path.Points[segment].width, path.Points[segment + 1].width, t));
            }
        }
        var last = path.Points[path.Points.Count - 1];
        centers.Add(last.position);
        widths.Add(last.width);

        int rows = centers.Count;
        var vertices = new Vector3[rows * 4];
        var uv = new Vector2[rows * 4];
        var triangles = new int[(rows - 1) * 18];
        float distance = 0f;
        for (int i = 0; i < rows; i++)
        {
            Vector3 before = centers[Mathf.Max(0, i - 1)];
            Vector3 after = centers[Mathf.Min(rows - 1, i + 1)];
            Vector3 direction = after - before;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector3.right;
            Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);
            float half = Mathf.Max(0.025f, widths[i] * 0.5f);
            Vector3 center = centers[i];
            int v = i * 4;
            vertices[v] = center + side * half + Vector3.up * path.SurfaceHeight;
            vertices[v + 1] = center - side * half + Vector3.up * path.SurfaceHeight;
            vertices[v + 2] = center + side * (half + path.ShoulderWidth) + Vector3.up * path.EdgeHeight;
            vertices[v + 3] = center - side * (half + path.ShoulderWidth) + Vector3.up * path.EdgeHeight;

            if (i > 0)
                distance += Vector3.Distance(centers[i - 1], center);
            float u = distance / path.TextureRepeatMeters;
            float widthUv = widths[i] / path.TextureRepeatMeters;
            float shoulderUv = path.ShoulderWidth / path.TextureRepeatMeters;
            uv[v] = new Vector2(u, 0f);
            uv[v + 1] = new Vector2(u, widthUv);
            uv[v + 2] = new Vector2(u, -shoulderUv);
            uv[v + 3] = new Vector2(u, widthUv + shoulderUv);

            if (i == rows - 1)
                continue;
            int next = v + 4;
            int t = i * 18;
            // Top surface, left shoulder, right shoulder: all face upward.
            triangles[t] = v; triangles[t + 1] = next; triangles[t + 2] = v + 1;
            triangles[t + 3] = next; triangles[t + 4] = next + 1; triangles[t + 5] = v + 1;
            triangles[t + 6] = v; triangles[t + 7] = v + 2; triangles[t + 8] = next;
            triangles[t + 9] = v + 2; triangles[t + 10] = next + 2; triangles[t + 11] = next;
            triangles[t + 12] = v + 1; triangles[t + 13] = next + 1; triangles[t + 14] = v + 3;
            triangles[t + 15] = next + 1; triangles[t + 16] = next + 3; triangles[t + 17] = v + 3;
        }

        // Very narrow end caps can fold a shoulder triangle; keep its visible face upward.
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]];
            Vector3 b = vertices[triangles[i + 1]];
            Vector3 c = vertices[triangles[i + 2]];
            if (Vector3.Cross(b - a, c - a).y < 0f)
            {
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }
        }

        var filter = path.GetComponent<MeshFilter>();
        if (filter == null)
            filter = Undo.AddComponent<MeshFilter>(path.gameObject);
        if (path.GetComponent<MeshRenderer>() == null)
            Undo.AddComponent<MeshRenderer>(path.gameObject);

        Mesh mesh = filter.sharedMesh;
        string assetPath = mesh == null ? null : AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(MeshFolder + "/"))
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/Art/Environment/WarmOchreRoad", "EditableMeshes");
            mesh = new Mesh { name = path.name + "_Mesh" };
            assetPath = AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/" + path.name + "_Mesh.asset");
            AssetDatabase.CreateAsset(mesh, assetPath);
            filter.sharedMesh = mesh;
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(path);
        if (path.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(path.gameObject.scene);
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
    }

    private static Vector3 Evaluate(List<EditableRoadPath.RoadPoint> points, int segment, float t)
    {
        Vector3 a = points[segment].position;
        Vector3 b = points[segment + 1].position;
        Vector3 previous = points[Mathf.Max(0, segment - 1)].position;
        Vector3 following = points[Mathf.Min(points.Count - 1, segment + 2)].position;
        Vector3 tangentA = segment == 0 ? b - a : (b - previous) * 0.5f;
        Vector3 tangentB = segment + 2 >= points.Count ? b - a : (following - a) * 0.5f;
        float t2 = t * t;
        float t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * a
             + (t3 - 2f * t2 + t) * tangentA
             + (-2f * t3 + 3f * t2) * b
             + (t3 - t2) * tangentB;
    }
}
