using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Splines
{
    [CustomEditor(typeof(SplineComponent))]
    public class SplineComponentEditor : Editor
    {
        int hotIndex = -1;
        int removeIndex = -1;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Hold Shift and click to append and insert curve points. Backspace to delete points.", MessageType.Info);
            // DrawDefaultInspector();
            var spline = target as SplineComponent;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("splineType"));
            GUILayout.BeginHorizontal();
            var closed = GUILayout.Toggle(spline.closed, "Closed", "button");
            if (spline.closed != closed)
            {
                spline.closed = closed;
                spline.ResetIndex();
            }
            if (GUILayout.Button("Flatten Y Axis"))
            {
                Undo.RecordObject(target, "Flatten Y Axis");
                Flatten(spline.points);
                spline.ResetIndex();
            }
            if (GUILayout.Button("Center around Origin"))
            {
                Undo.RecordObject(target, "Center around Origin");
                CenterAroundOrigin(spline.points);
                spline.ResetIndex();
            }
            GUILayout.EndHorizontal();
            serializedObject.ApplyModifiedProperties();
        }

        [DrawGizmo(GizmoType.NonSelected)]
        static void DrawGizmosLoRes(SplineComponent spline, GizmoType gizmoType)
        {
            Gizmos.color = Color.white;
            DrawGizmo(spline, 64);
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawGizmosHiRes(SplineComponent spline, GizmoType gizmoType)
        {
            Gizmos.color = Color.white;
            DrawGizmo(spline, 1024);
        }

        static void DrawGizmo(SplineComponent spline, int stepCount)
        {
            if (spline.points.Count > 0)
            {
                var P = 0f;
                var start = spline.GetNonUniformPoint(0);
                var step = 1f / stepCount;
                do
                {
                    P += step;
                    var here = spline.GetNonUniformPoint(P);
                    Gizmos.DrawLine(start, here);
                    start = here;
                } while (P + step <= 1);

                Handles.color = Color.green;
                foreach (var i in spline.points)
                {
                    if (i.y != 0 || Tools.pivotRotation == PivotRotation.Global)
                    {
                        var cp = spline.transform.TransformPoint(i);
                        var end = cp;
                        var up = spline.transform.up;
                        if (Tools.pivotRotation == PivotRotation.Local)
                        {
                            end = i;
                            end.y = 0;
                            end = spline.transform.TransformPoint(end);
                        }
                        else
                        {
                            end.y = 0;
                            up = Vector3.up;
                        }
                        if ((cp - end).sqrMagnitude > 0)
                        {
                            Handles.DrawDottedLine(cp, end, 4);
                            var discSize = HandleUtility.GetHandleSize(end) * 0.25f;
                            Handles.DrawWireDisc(end, up, discSize);
                        }
                    }
                }
                Handles.color = Color.white;
            }
        }

        void OnSceneGUI()
        {
            var spline = target as SplineComponent;

            var e = Event.current;
            GUIUtility.GetControlID(FocusType.Passive);

            var mousePos = (Vector2)Event.current.mousePosition;
            var view = SceneView.currentDrawingSceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
            var mouseIsOutside = view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1;
            if (mouseIsOutside) return;
            var points = serializedObject.FindProperty("points");
            if (Event.current.shift)
            {
                if (spline.closed)
                    ShowClosestPointOnClosedSpline(points);
                else
                    ShowClosestPointOnOpenSpline(points);
            }

            for (int i = 0; i < spline.points.Count; i++)
            {
                var prop = points.GetArrayElementAtIndex(i);
                var point = prop.vector3Value;
                var wp = spline.transform.TransformPoint(point);
                if (hotIndex == i)
                {
                    var newWp = Handles.PositionHandle(wp, Tools.pivotRotation == PivotRotation.Global ? Quaternion.identity : spline.transform.rotation);
                    var delta = spline.transform.InverseTransformDirection(newWp - wp);
                    if (delta.sqrMagnitude > 0)
                    {
                        prop.vector3Value = point + delta;
                        spline.ResetIndex();
                    }
                    HandleCommands(wp);
                }
                Handles.color = i == 0 | i == spline.points.Count - 1 ? Color.red : Color.white;
                var buttonSize = HandleUtility.GetHandleSize(wp) * 0.1f;
                if (Handles.Button(wp, Quaternion.identity, buttonSize, buttonSize, Handles.SphereHandleCap))
                    hotIndex = i;
                {
                    var v = SceneView.currentDrawingSceneView.camera.transform.InverseTransformPoint(wp);
                    var labelIsOutside = v.z < 0;
                    if (!labelIsOutside) Handles.Label(wp, i.ToString());
                }
            }
            if (removeIndex >= 0 && points.arraySize > 4)
            {
                points.DeleteArrayElementAtIndex(removeIndex);
                spline.ResetIndex();
            }
            removeIndex = -1;
            serializedObject.ApplyModifiedProperties();

        }

        void HandleCommands(Vector3 wp)
        {
            if (Event.current.type == EventType.ExecuteCommand)
            {
                if (Event.current.commandName == "FrameSelected")
                {
                    SceneView.currentDrawingSceneView.Frame(new Bounds(wp, Vector3.one * 10), false);
                    Event.current.Use();
                }
            }
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Backspace)
                {
                    removeIndex = hotIndex;
                    Event.current.Use();
                }
            }
        }

        void ShowClosestPointOnClosedSpline(SerializedProperty points)
        {
            var spline = target as SplineComponent;
            var plane = new Plane(spline.transform.up, spline.transform.position);
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            float center;
            if (plane.Raycast(ray, out center))
            {
                var hit = ray.origin + ray.direction * center;
                Handles.DrawWireDisc(hit, spline.transform.up, 5);
                var p = SearchForClosestPoint(Event.current.mousePosition);
                var sp = spline.GetNonUniformPoint(p);
                Handles.DrawLine(hit, sp);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
                {
                    var i = (Mathf.FloorToInt(p * spline.points.Count) + 2) % spline.points.Count;
                    points.InsertArrayElementAtIndex(i);
                    points.GetArrayElementAtIndex(i).vector3Value = spline.transform.InverseTransformPoint(sp);
                    serializedObject.ApplyModifiedProperties();
                    hotIndex = i;
                }
            }
        }

        void ShowClosestPointOnOpenSpline(SerializedProperty points)
        {
            var spline = target as SplineComponent;
            var plane = new Plane(spline.transform.up, spline.transform.position);
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            float center;
            if (plane.Raycast(ray, out center))
            {
                var hit = ray.origin + ray.direction * center;
                var discSize = HandleUtility.GetHandleSize(hit);
                Handles.DrawWireDisc(hit, spline.transform.up, discSize);
                var p = SearchForClosestPoint(Event.current.mousePosition);

                if ((hit - spline.GetNonUniformPoint(0)).sqrMagnitude < 25) p = 0;
                if ((hit - spline.GetNonUniformPoint(1)).sqrMagnitude < 25) p = 1;

                var sp = spline.GetNonUniformPoint(p);

                var extend = Mathf.Approximately(p, 0) || Mathf.Approximately(p, 1);

                Handles.color = extend ? Color.red : Color.white;
                Handles.DrawLine(hit, sp);
                Handles.color = Color.white;

                var i = 1 + Mathf.FloorToInt(p * (spline.points.Count - 3));

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.shift)
                {
                    if (extend)
                    {
                        if (i == spline.points.Count - 2) i++;
                        points.InsertArrayElementAtIndex(i);
                        points.GetArrayElementAtIndex(i).vector3Value = spline.transform.InverseTransformPoint(hit);
                        hotIndex = i;
                    }
                    else
                    {
                        i++;
                        points.InsertArrayElementAtIndex(i);
                        points.GetArrayElementAtIndex(i).vector3Value = spline.transform.InverseTransformPoint(sp);
                        hotIndex = i;
                    }
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        float SearchForClosestPoint(Vector2 screenPoint, float A = 0f, float B = 1f, float steps = 1000)
        {
            var spline = target as SplineComponent;
            var smallestDelta = float.MaxValue;
            var step = (B - A) / steps;
            var closestI = A;
            for (var i = 0; i <= steps; i++)
            {
                var p = spline.GetNonUniformPoint(i * step);
                var gp = HandleUtility.WorldToGUIPoint(p);
                var delta = (screenPoint - gp).sqrMagnitude;
                if (delta < smallestDelta)
                {
                    closestI = i;
                    smallestDelta = delta;
                }
            }
            return closestI * step;
        }

        void Flatten(List<Vector3> points)
        {
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = Vector3.Scale(points[i], new Vector3(1, 0, 1));
            }
        }

        void CenterAroundOrigin(List<Vector3> points)
        {
            var center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                center += points[i];
            }
            center /= points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                points[i] -= center;
            }
        }

    }

}