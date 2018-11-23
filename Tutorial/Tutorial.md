Editor Scripting Tutorial - Building a Spline Tool
--------------------------------------------------

Author: Simon Wittber <simonwittber@unity3d.com>

# Creating the Component

If we create an interface which specifies the API of our spline tool, we can use
this interface instead of a concrete class, which allows us to switch between
implementations, and integrate with any other systems which might arrive in the
future, as long as they also use the interface.

This interface specification contains the general methods which are applicable
across most spline algorithms. It contains methods for creating and adjusting
the spline, and methods for querying the spline for different information.

## The Spline Interface (ISpline.cs)

    /// <summary>
    /// A interface for general spline data.
    /// NB: - All Vector3 arguments and Vector3 return values are in world space.
    ///     - All t arguments specify a uniform position along the spline, apart
    ///       from the GetNonUniformPoint method.
    /// </summary>
    public interface ISpline
    {
        Vector3 GetNonUniformPoint(float t);
        Vector3 GetPoint(float t);

        Vector3 GetLeft(float t);
        Vector3 GetRight(float t);
        Vector3 GetUp(float t);
        Vector3 GetDown(float t);
        Vector3 GetForward(float t);
        Vector3 GetBackward(float t);

        float GetLength(float stepSize);

        Vector3 GetControlPoint(int index);
        void SetControlPoint(int index, Vector3 position);
        void InsertControlPoint(int index, Vector3 position);
        void RemoveControlPoint(int index);

        Vector3 GetDistance(float distance);
        Vector3 FindClosest(Vector3 worldPoint);

        int ControlPointCount { get; }
    }
    
    
## An empty class. (SplineComponent.cs)

If we use a default impllementation of the class, we will get the following 
class. It does nothing in itself, but gives us stubs to enter all the required
methods to satisfy the ISpline interface.

    public class SplineComponent : MonoBehaviour, ISpline
    {
        public int ControlPointCount { get { throw new System.NotImplementedException(); } }

        public Vector3 FindClosest(Vector3 worldPoint)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetBackward(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetControlPoint(int index)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetDistance(float distance)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetDown(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetForward(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetLeft(float t)
        {
            throw new System.NotImplementedException();
        }

        public float GetLength(float stepSize)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetNonUniformPoint(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetPoint(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetRight(float t)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetUp(float t)
        {
            throw new System.NotImplementedException();
        }

        public void InsertControlPoint(int index, Vector3 position)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveControlPoint(int index)
        {
            throw new System.NotImplementedException();
        }

        public void SetControlPoint(int index, Vector3 position)
        {
            throw new System.NotImplementedException();
        }
    }
    
    
## The Interpolator

This is a hermite spline interpolation function. It takes 4 vectors (a and b are
control points, b and c are the start and end points) and a u parameter which
specifies the interpolation position.

        internal static Vector3 Interpolate(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u)
        {
            return (
                0.5f *
                (
                    (-a + 3f * b - 3f * c + d) *
                    (u * u * u) +
                    (2f * a - 5f * b + 4f * c - d) *
                    (u * u) +
                    (-a + c) *
                    u + 2f * b
                )
            );
        }
        
## The Data

We need some fields to store that data used by our Interpolate function. The 
closed field specifies if the spline should form a closed loop or not, the 
points list will contain our control points which specify the shape of the 
spline, and finally the length is a nullable float where we can store the length
of the spline once it has been calculated? Why nullable? You will find out soon!

        public bool closed = false;
        public List<Vector3> points = new List<Vector3>();
        public float? length;

We can now fill in the body of some of the methods required by the interface.

        public int ControlPointCount => points.Count;

        public Vector3 GetNonUniformPoint(float t)
        {
            switch (points.Count)
            {
                case 0:
                    return Vector3.zero;
                case 1:
                    return transform.TransformPoint(points[0]);
                case 2:
                    return transform.TransformPoint(Vector3.Lerp(points[0], points[1], t));
                case 3:
                    return transform.TransformPoint(points[1]);
                default:
                    return Hermite(t);
            }
        }
        
        public void InsertControlPoint(int index, Vector3 position)
        {
            ResetIndex();
            if (index >= points.Count)
                points.Add(position);
            else
                points.Insert(index, position);
        }

        public void RemoveControlPoint(int index)
        {
            ResetIndex();
            points.RemoveAt(index);
        }

        public Vector3 GetControlPoint(int index)
        {
            return points[index];
        }

        public void SetControlPoint(int index, Vector3 position)
        {
            ResetIndex();
            points[index] = position;
        }
        
This is the function which looks up the correct control points for a position
along the spline then performs and return the interpolated world position.
        
        Vector3 Hermite(float t)
        {
            var count = points.Count - (closed ? 0 : 3);
            var i = Mathf.Min(Mathf.FloorToInt(t * (float)count), count - 1);
            var u = t * (float)count - (float)i;
            var a = GetPointByIndex(i);
            var b = GetPointByIndex(i + 1);
            var c = GetPointByIndex(i + 2);
            var d = GetPointByIndex(i + 3);
            return transform.TransformPoint(Interpolate(a, b, c, d, u));
        }
        
        Vector3 GetPointByIndex(int i)
        {
            if (i < 0) i += points.Count;
            return points[i % points.Count];
        }
        
        
## How to get Uniform points along a spline? (SplineIndex.cs)

If we look at the interface documentation, you will notice that almost all the
query methods are expected to return a uniform position along the spline. This
is not straightforward, as our spline is composed of arbitrary control points
which could be any distance from each other. In addition to this, the nature
of our interpolation algorithm means we cannot simply store the distance between
control points and use that to modify the t parameter.

Therefore, we create an index of discrete, uniform positions along the spline.
This index is then used to provide the uniform positions assumed by the 
interface.


    public class SplineIndex
    {
        public Vector3[] linearPoints;
        SplineComponent spline;

        public int ControlPointCount => spline.ControlPointCount;

        public SplineIndex(SplineComponent spline)
        {
            this.spline = spline;
            ReIndex();
        }

        public void ReIndex()
        {
            var searchStepSize = 0.00001f;
            var length = spline.GetLength(searchStepSize);
            var indexSize = Mathf.FloorToInt(length * 2);
            var _linearPoints = new List<Vector3>(indexSize);
            var t = 0f;

            var linearDistanceStep = length / 1024;
            var linearDistanceStep2 = Mathf.Pow(linearDistanceStep, 2);

            var start = spline.GetNonUniformPoint(0);
            _linearPoints.Add(start);
            while (t <= 1f)
            {
                var current = spline.GetNonUniformPoint(t);
                while ((current - start).sqrMagnitude <= linearDistanceStep2)
                {
                    t += searchStepSize;
                    current = spline.GetNonUniformPoint(t);
                }
                start = current;
                _linearPoints.Add(current);
            }
            linearPoints = _linearPoints.ToArray();
        }

        public Vector3 GetPoint(float t)
        {
            var sections = linearPoints.Length - (spline.closed ? 0 : 3);
            var i = Mathf.Min(Mathf.FloorToInt(t * (float)sections), sections - 1);
            var count = linearPoints.Length;
            if (i < 0) i += count;
            var u = t * (float)sections - (float)i;
            var a = linearPoints[(i + 0) % count];
            var b = linearPoints[(i + 1) % count];
            var c = linearPoints[(i + 2) % count];
            var d = linearPoints[(i + 3) % count];
            return SplineComponent.Interpolate(a, b, c, d, u);
        }

    }


## Add lazy indexing to Spline

The index we have created is expensive to create, and takes (relatively 
speaking) quite a lot of memory. If the user does not need this index, we should
avoid creating it. This is achieved by using a private property which will only
create an index when required, then re-use that index. We also provide a method
to reset the index, so that the index will be rebuilt when control points or 
other parameters are changed.

The index now allows us to add a body to the GetPoint method required by the
interface, and return a uniform position along the spline.

        /// <summary>
        /// Index is used to provide uniform point searching.
        /// </summary>
        SplineIndex uniformIndex;
        SplineIndex Index
        {
            get
            {
                if (uniformIndex == null) uniformIndex = new SplineIndex(this);
                return uniformIndex;
            }
        }
        
        public void ResetIndex()
        {
            uniformIndex = null;
            length = null;
        }
        
        public Vector3 GetPoint(float t) => Index.GetPoint(t);
        
## Add Query Methods

Now that we have an implementation for GetPoint, we can construct the remainder
of the query methods.

        public Vector3 GetRight(float t)
        {
            var A = GetPoint(t - 0.001f);
            var B = GetPoint(t + 0.001f);
            var delta = (B - A);
            return new Vector3(-delta.z, 0, delta.x).normalized;
        }

        public Vector3 GetForward(float t)
        {
            var A = GetPoint(t - 0.001f);
            var B = GetPoint(t + 0.001f);
            return (B - A).normalized;
        }

        public Vector3 GetUp(float t)
        {
            var A = GetPoint(t - 0.001f);
            var B = GetPoint(t + 0.001f);
            var delta = (B - A).normalized;
            return Vector3.Cross(delta, GetRight(t));
        }

        public Vector3 GetPoint(float t) => Index.GetPoint(t);

        public Vector3 GetLeft(float t) => -GetRight(t);

        public Vector3 GetDown(float t) => -GetUp(t);

        public Vector3 GetBackward(float t) => -GetForward(t);

For the same reasons we need to construct an index, we also need to iterate 
along the spline to get an estimate of the total length. The step paramter
controls how accurate the estimate will be. It defaults to 0.001f, which is
acceptable for most cases.

        public float GetLength(float step = 0.001f)
        {
            var D = 0f;
            var A = GetNonUniformPoint(0);
            for (var t = 0f; t < 1f; t += step)
            {
                var B = GetNonUniformPoint(t);
                var delta = (B - A);
                D += delta.magnitude;
                A = B;
            }
            return D;
        }

        public Vector3 GetDistance(float distance)
        {
            if (length == null) length = GetLength();
            return uniformIndex.GetPoint(distance / length.Value);
        }

The FindClosest method returns the approximate closest position on the spline
to a world point. Due to the nature of splines, this solution cannot be 
analytical and we must create a numerical solution to solve the problem. The 
spline is divided into 1024 points and we choose the closest by comparing square
of the distance to the world point.

        public Vector3 FindClosest(Vector3 worldPoint)
        {
            var smallestDelta = float.MaxValue;
            var step = 1f / 1024;
            var closestPoint = Vector3.zero;
            for (var i = 0; i <= 1024; i++)
            {
                var p = GetPoint(i * step);
                var delta = (worldPoint - p).sqrMagnitude;
                if (delta < smallestDelta)
                {
                    closestPoint = p;
                    smallestDelta = delta;
                }
            }
            return closestPoint;
        }
        
## Add editor helper methods

The editor provides the Reset method, which is used to set default values on the
component when it is first added to a gameobject. Add 4 default points as that
is the minimum required for our spline implementation.

        void Reset()
        {
            points = new List<Vector3>() {
                Vector3.forward * 3,
                Vector3.forward * 6,
                Vector3.forward * 9,
                Vector3.forward * 12
            };
        }
        
OnValidate is called by the editor whenever values on the component have been
changed. If we have an active index on our component, we reindex the spline so 
that the index will be built on the changed values.

        void OnValidate()
        {
            if (uniformIndex != null) uniformIndex.ReIndex();
        }
        
# Creating the Editor

The SplineComponent works nicely, but to use it effectively inside the Unityv 
Editor, we are going to need to make it much more user friendly.

## A Custom Inspector (Editor/SplineComponentEditor.cs)

The first step is a custom inspector. This is created inside an Editor class
via the OnInspectorGUI method. The method below sets up widgets for the 
component fields, and adds some buttons for some useful utility methods we will
create later.

    [CustomEditor(typeof(SplineComponent))]
    public class SplineComponentEditor : Editor
    {
        
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Hold Shift and click to append and insert curve points. Backspace to delete points.", MessageType.Info);
            var spline = target as SplineComponent;
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
                //TODO: Flatten(spline.points);
                spline.ResetIndex();
            }
            if (GUILayout.Button("Center around Origin"))
            {
                Undo.RecordObject(target, "Center around Origin");
                //TODO: CenterAroundOrigin(spline.points);
                spline.ResetIndex();
            }
            GUILayout.EndHorizontal();
        }
        
    }
    
## Draw Gizmos

Gizmos are the visual inside the scene view that helps us identify the 
component, especially since it has no renderable geometry. There is 3 functions,
the main drawing function (DrawGizmo) and 2 other functions which have the
DrawGizmo attribute. This allows us to draw a high resolution gizmo when the
spline component is selected in the hierarchy, and a low resolution gizmo at 
other times.

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
            }
        }
        
## Scene View Controls

You will notice that we didn't create inspector fields for the spline control
points. That is because we are going to manage the control points through the 
scene view. 

These two fields store the index of the currently selected control point, and
if we choose to remove a control point, we store the index of that control point
too. Why? Stay tuned, this will be answered below.

        int hotIndex = -1;
        int removeIndex = -1;

The OnSceneGUI method allows us to draw widgets inside the scene view when the
component is selected in the hierarchy. If the mouse cursor is not over the
scene view, we early exit the method to avoid the potentially expensive drawing
which can really slow down the Editor when in play mode.

If the user is holding down the shift key, we perform some special visualisation
as we are going to use shift + left click events to add control points.

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

## Loop over the  serialized property

When modifying control points, a SerializedProperty is used instead of directly
modifying the points list, or using the appropriate methods on the component.
This is done so that Undo/Redo functionality is automatically applied to the
entire point list, including position value.

To use the control point in the scene view, it must be converted into world 
space using the TransformPoint method.

            for (int i = 0; i < spline.points.Count; i++)
            {
                var prop = points.GetArrayElementAtIndex(i);
                var point = prop.vector3Value;
                var wp = spline.transform.TransformPoint(point);

## Draw control widgets for the selected control point

If the current control point is 'hot' (selected by the user), the Handles which
allow position modification are drawn. We only update the position value of the
property if the handle was moved.

Command events are also applied only to the hot control point, these are put 
into the HandleCommands method for readability.

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

## Allow selection of control points                

How does the user select which control point to edit? The Handles.Button method
works just like a regular IMGUI Button method, however it allows us to use a 
sphere as the button visual instead of a GUI button. This is perfect for 
visualising and selecting points in the scene view. We use the GetHandleSize 
method so that the button-spheres are drawn at a consistent size across the
scene, regardless of the camera position.

                Handles.color = i == 0 | i == spline.points.Count - 1 ? Color.red : Color.white;
                var buttonSize = HandleUtility.GetHandleSize(wp) * 0.1f;
                if (Handles.Button(wp, Quaternion.identity, buttonSize, buttonSize, Handles.SphereHandleCap))
                    hotIndex = i;

We also draw the index of the control point using Handles.Label. This is a great
idea to help you debug problems in the future.

                var v = SceneView.currentDrawingSceneView.camera.transform.InverseTransformPoint(wp);
                var labelIsOutside = v.z < 0;
                if (!labelIsOutside) Handles.Label(wp, i.ToString());
            
            }
            
## Perform deletion last

Remember the removeIndex field we created? This is where we use the value of 
that field to remove a control point. This happens right at the end of the
OnSceneGUI method, so that next time the method is called it will have a correct
list of control points. It also avoids modifying the list of points during other
method calls, which can cause problems when iterating over the changed list.

            if (removeIndex >= 0 && points.arraySize > 4)
            {
                points.DeleteArrayElementAtIndex(removeIndex);
                spline.ResetIndex();
            }
            
Remember to set removeIndex to -1, otherwise we will delete a point every frame!
Also, to persist the changes we must must call ApplyModifiedProperties.

            removeIndex = -1;
            serializedObject.ApplyModifiedProperties();

        }
        
## Intercept and Handle Keyboard Commands

This is the method mentioned previously for handling commands which are intended
for the hot control point. The first command is 'FrameSelected', which occurs
when you press the F key in the scene view. We intercept the command here, so 
that instead of framing the game object which the spline component is attached 
to, we frame the hot control point.

The second command catches the Backspace keypress, allowing the hot control 
point to be scheduled for deletion, by assign it's index to the removeIndex 
field.

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

## Allow adding and inserting control points

These are the two functions which are called from OnSceneGUI when the user has
the shift key pressed. They have slightly different behaviour depending on 
whether the spline is closed or open, so for clarity this is split into two
different methods. 

Both methods have similar functionality. They draw a line from the mouse cursor
to the intersection point on the spline where the new control point will be
inserted. In the case of an open spline, they also show a line when extending
the spline from one of the end points.

They then check for the left click of the mouse button and if clicked use the 
SerializedProperty API to insert an item into the list of points, and then set 
it's value to the new control point position.

As both methods have the common function of searching for a closest point, this
function is split out into a separate method.

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
        
## Add Utility Methods

The final task is to create the utility methods which are called by the custom
inspector buttons. The first method flattens the y position of all the control
points. The second repositions all the control points, so that the GameObjects's 
transform is at the center of all the control points.

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