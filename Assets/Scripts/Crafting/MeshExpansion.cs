using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MeshExpansion {
    // List of vertices / indices
    List<Vector3> vertexList;
    List<int> indexList;
    List<AttachmentPoint> attachmentList;

    // The base prefab of a component that will be modified when expansion is completed.
    public GameObject basePrefab;

    // The location that the rendered object should be placed at.
    public Transform renderPosition;

    // The triangulator script.
    Triangulator tri;

    public MeshExpansion () { 
        this.vertexList = new List<Vector3>();
        this.indexList = new List<int>();
        this.attachmentList = new List<AttachmentPoint>();

        this.tri = new Triangulator();
    }

    /// <summary>
    /// This method of mesh creation compresses the mesh into itself. This makes sure to keep
    /// the same outline as the one created by the player, making the weapon feel much more
    /// like what they drew.
    /// 
    /// It does this by starting at the outline, then moving "up" the mesh towards the flat
    /// outer plane(s). Each step along the way can be thought of as similar to an MRI scan.
    /// At each layer, we move each line back a certain distance, check if it's now flipped
    /// direction (meaning it should just become a point), then check if each line is crossing
    /// any other line. If two lines cross, we find out where, create a new point there, merge
    /// the line that crossed's point into the new one, create a new point on the other line,
    /// and merge that new point into the point where the two crossed. We keep going through
    /// the process until no two lines are crossing, then we move to the next layer. Since the
    /// mesh is symmetrical on each half (top and bottom), we only have to go through half the
    /// mesh. Once the outline created is at the half-thickness, we have finished and can now
    /// complete the mesh.
    /// 
    /// If the outline becomes more than one polygon, each will become a new "outline" to go
    /// through the process of and eventually triangulate. We know an outline splits into two
    /// or more when it has two lines that share points but are inverted in direction. This is
    /// where the two or more new polygons have formed off of each other. This process should
    /// happen after completing the new outline. We also know that if there are points that are
    /// colinear with their neighbors, then they aren't part of any outline anymore.
    /// </summary>

    /// <remarks>
    /// CURRENT ISSUES:
    /// 1) It does NOT fix lines that do not have enough space to include a new point in between 
    /// them. This causes an overlapping face that looks odd.
    /// 2) It does NOT fix lines that cross over multiple lines. Say two corners meet so the lines
    /// cross in two separate places on two different lines. This will not trigger a correction
    /// because the method looks only for lines crossing ONE line twice, not multiple lines.
    /// </remarks>
    /// <param name="vertices">The list of vertices that the player creates. This contains all
    /// the information we could need to calculate how this mesh will appear.</param>
    /// <param name="_thickness">How thick the component will be.</param>
    /// <param name="_edgeAngle">How sharp the edge will be. This is the angle of the edge from
    /// the middle (XY plane) and the front/back plane. 0 degrees would be infinitely sharp, 90
    /// degrees would be completely blunt (basically a flat edge).</param>
    public bool CreateMesh (List<Vertex> _vertices, List<Connection> _connections, float _thickness, float _edgeAngle, 
        out List<int> _indices, out List<Vector3> _verts, out List<AttachmentPoint> _attachments) {

        // Make sure to clear the lists before we start.
        this.vertexList.Clear();
        this.indexList.Clear();
        this.attachmentList.Clear();

        // Pull out data into two lists for easy math
        List<Vector2> points = _vertices.Select(v => v.Location).ToList();
        List<EdgeType> edges = _connections.Select(c => c.Edge).ToList();

        // Shift all the edges by one to align with points.
        EdgeType _e = edges[edges.Count - 1];
        edges.RemoveAt(edges.Count - 1);
        edges.Insert(0, _e);

        // TEMPORARY:
        // Determine where the attachment points are based on which edges have been marked as possible to attach to.
        for (int i = 0; i < _connections.Count; i++) {
            if (_connections[i].ConnectionCanBeAttachedTo) {
                // If a connection is set as able to be attached to, create a new attachment point there.
                AttachmentPoint newAttachPoint = new AttachmentPoint();
                newAttachPoint.SetLocation(_connections[i].MidPoint);
                newAttachPoint.SetNormalDirection(_connections[i].Normal);

                // Add it to the list.
                this.attachmentList.Add(newAttachPoint);
            }
        }

        // How many edge loops the mesh should have. This is calculated based on the thickness of
        // the mesh.
        int loops = Math.Max(1, Mathf.RoundToInt(_thickness * 20f));
        // Make sure the loops count are always an odd number.
        if (loops % 2 == 0)
            loops++;

        #region Compressed Mesh (V2)

        /// <summary>
        /// This method of mesh creation compresses the mesh into itself. This makes sure to keep
        /// the same outline as the one created by the player, making the weapon feel much more
        /// like what they drew.
        /// 
        /// It does this by starting at the outline, then moving "up" the mesh towards the flat
        /// outer plane(s). Each step along the way can be thought of as similar to an MRI scan.
        /// At each layer, we move each line back a certain distance, check if it's now flipped
        /// direction (meaning it should just become a point), then check if each line is crossing
        /// any other line. If two lines cross, we find out where, create a new point there, merge
        /// the line that crossed's point into the new one, create a new point on the other line,
        /// and merge that new point into the point where the two crossed. We keep going through
        /// the process until no two lines are crossing, then we move to the next layer. Since the
        /// mesh is symmetrical on each half (top and bottom), we only have to go through half the
        /// mesh. Once the outline created is at the half-thickness, we have finished and can now
        /// complete the mesh.
        /// 
        /// If the outline becomes more than one polygon, each will become a new "outline" to go
        /// through the process of and eventually triangulate. We know an outline splits into two
        /// or more when it has two lines that share points but are inverted in direction. This is
        /// where the two or more new polygons have formed off of each other. This process should
        /// happen after completing the new outline. We also know that if there are points that are
        /// colinear with their neighbors, then they aren't part of any outline anymore.
        /// </summary>

        /// <remarks>
        /// CURRENT ISSUES:
        /// 1) It does NOT fix lines that do not have enough space to include a new point in between 
        /// them. This causes an overlapping face that looks odd.
        /// 2) It does NOT fix lines that cross over multiple lines. Say two corners meet so the lines
        /// cross in two separate places on two different lines. This will not trigger a correction
        /// because the method looks only for lines crossing ONE line twice, not multiple lines.
        /// 3) Round lines aren't correctly computing how their roundness should appear.
        /// 4) MAJOR: In the points merging process, if multiple points want to merge with the same
        /// vertex, it will cause issues when one point merges first, deleting the original points
        /// and preventing any further points from merging. EX: if there are three points, and the
        /// left point wants to merge with the center and the right point wants to merge with the
        /// center, but the left doesn't want to merge with right, there will be an issue. The left
        /// point will first merge with the center, moving them and DELETING the center, then when
        /// the right tries to merge the center point no longer exists.
        /// 4.1) Possible solution: There's no need to merge points that are too close together if 
        /// the "switched directions" worked more accurately. If instead of only knowing that a line
        /// switched directions, we could also find how far along the "expansion" that occured, we
        /// could then merge the points there, then continue the rest of the way with the new point.
        /// This would solve issue #1 too. After the merging, it would also need to do a check if 
        /// the outline had split and reacted accordingly.
        /// </remarks>

        // Calcualate how far out the edges will need to be to be at the angle given.
        float edgeDistance = (_thickness / 2f * Mathf.Sin(Mathf.PI * 0.5f) / Mathf.Sin(_edgeAngle * Mathf.Deg2Rad));

        // The amount of percentage that will be increased each loop pass.
        float percentStep = 1.0f / loops;

        // The current outline(s) we've created. This is updated as we go up the mesh like
        // we're taking it through an MRI scan.
        List<List<Vector3>> currentOutlineVecs = new List<List<Vector3>>();
        List<List<EdgeType>> currentOutlineEdges = new List<List<EdgeType>>();
        List<List<int>> currentOutlineIndices = new List<List<int>>();

        // Initialize the starting outline.
        currentOutlineVecs.Add(new List<Vector3>());
        currentOutlineEdges.Add(new List<EdgeType>());
        currentOutlineIndices.Add(new List<int>());
        for (int i = 0; i < _vertices.Count; i++) {
            currentOutlineVecs[0].Add(_vertices[i].Location);
            currentOutlineEdges[0].Add(edges[i]);
            currentOutlineIndices[0].Add(i);
        }

        // The lists that gets created as we compress the mesh inward. This gets cleared every step
        // upwards after being applied to the current outlines after calculations.
        List<List<Vector3>> nextOutlineVecs = new List<List<Vector3>>();
        List<List<EdgeType>> nextOutlineEdges = new List<List<EdgeType>>();

        // How many vertices we have created so far.
        int vertexCount = currentOutlineVecs[0].Count;

        // The list of all the mesh's points as we create them.
        List<Vector3> meshPoints = new List<Vector3>();

        // Clear the debug lines.
        DebugLines.Instance.ClearQueue();

        for (int i = 0; i < currentOutlineVecs[0].Count; i++) {
            int next = ((i + 1) >= currentOutlineVecs[0].Count) ? 0 : i + 1;
            DebugLines.Instance.AddLine(currentOutlineVecs[0][i], currentOutlineVecs[0][next], Color.red);
        }

        // Add the initial outline to the mesh points.
        meshPoints.AddRange(currentOutlineVecs[0]);

        // Begin calculating inner mesh points.
        for (int i = 1; i < loops; i++) {

            // See if there's even anything to expand.
            if (currentOutlineVecs.Count == 0)
                break;

            // Figure out how far "out" the edge is.
            float percent = (i * 1.0f) / loops;

            // The Z-value of this layer.
            float z = _thickness / 2f * percent;

            // Go through each outline in the mesh.
            for (int j = 0; j < currentOutlineVecs.Count; j++) {
                // Make sure there's something expand.
                if (currentOutlineVecs[j].Count == 0)
                    continue;

                // Add a new list for the next outline we're creating.
                nextOutlineVecs.Add(new List<Vector3>());
                nextOutlineEdges.Add(new List<EdgeType>());

                // Add a list of indices that will keep track of each points previous connections to the
                // layer below it.
                List<List<Vector2Int>> nextOutlinePreviousConnections = new List<List<Vector2Int>>();
                // The list of indices for these points (relative to the total amount of points in the mesh).
                List<int> nextVertexIndex = new List<int>();
                // The list of indices that follow each point (relative to this outline).
                List<int> nextOutlineNextConnections = new List<int>();
                // The list of point indices that are the direct connection of each point (relative to the mesh).
                List<int> nextOutlineDirectConnection = new List<int>();

                // Go through each vertex and push it back and add it to the next outline.
                for (int k = 0; k < currentOutlineVecs[j].Count; k++) {

                    nextOutlinePreviousConnections.Add(new List<Vector2Int>());

                    // Find the next and the previous points.
                    int nextEdge = ((k + 1) >= currentOutlineEdges[j].Count) ? 0 : k + 1;
                    int lastEdge = ((k - 1) < 0) ? currentOutlineEdges[j].Count - 1 : k - 1;

                    // Find the new point. This is where the two edge lines would meet if they continued on
                    // after being pulled inward.
                    Vector2 leftNormalPoint, rightNormalPoint, leftNormal, rightNormal, leftEdgeDirection, rightEdgeDirection;
                    Vector3 farPoint;
                    farPoint = leftNormalPoint = rightNormalPoint = currentOutlineVecs[j][k];

                    // Calculate edge normals.
                    leftEdgeDirection = (currentOutlineVecs[j][k] - currentOutlineVecs[j][lastEdge]).normalized;
                    rightEdgeDirection = (currentOutlineVecs[j][nextEdge] - currentOutlineVecs[j][k]).normalized;

                    leftNormal = new Vector2(leftEdgeDirection.y, -leftEdgeDirection.x);
                    rightNormal = new Vector2(rightEdgeDirection.y, -rightEdgeDirection.x);

                    switch (currentOutlineEdges[j][k]) {
                        // A flat (or default) edge should not be pulled inwards at all.
                        default:
                        case EdgeType.FLAT:
                            break;
                        // A sharp edge gets pulled in evenly for each "step".
                        case EdgeType.SHARP:
                            leftNormalPoint += leftNormal * edgeDistance * percentStep;
                            break;
                        // A round edge is pulled in more as it gets closer to the top.
                        case EdgeType.ROUND:
                            leftNormalPoint += leftNormal * Mathf.Sin(Mathf.PI / 2f * (1 - percent)) * percentStep;
                            break;
                    }

                    switch (currentOutlineEdges[j][nextEdge]) {
                        default:
                        case EdgeType.FLAT:
                            break;
                        case EdgeType.SHARP:
                            rightNormalPoint += rightNormal * edgeDistance * percentStep;
                            break;
                        case EdgeType.ROUND:
                            rightNormalPoint += rightNormal * Mathf.Sin(Mathf.PI / 2f * (1 - percent)) * percentStep;
                            break;
                    }

                    // Calculate the far point.
                    farPoint = this.PointOnTwoLines(leftNormalPoint - leftEdgeDirection * 5000f, leftEdgeDirection * 10000f, rightNormalPoint - rightEdgeDirection * 5000f, rightEdgeDirection * 10000f);

                    //DebugLines.Instance.addLine(leftNormalPoint - leftEdgeDirection * 0.25f, leftNormalPoint + leftEdgeDirection * 0.25f, new Color(0.7f * percent, 0f, 1.0f * percent));
                    //DebugLines.Instance.addLine(rightNormalPoint - rightEdgeDirection * 0.25f, rightNormalPoint + rightEdgeDirection * 0.25f, new Color(1.0f * percent, 0.7f * percent, 0f));

                    // Make sure the point doesn't become unusable.
                    if (farPoint.x == float.NegativeInfinity || farPoint.y == float.NegativeInfinity)
                        farPoint = (leftNormalPoint + rightNormalPoint) / 2;

                    // Correct the Z-value.
                    farPoint.z = z;

                    // Create the initial points in the previous outline that this point will connect to.
                    nextOutlinePreviousConnections[k].Add(new Vector2Int(currentOutlineIndices[j][k], currentOutlineIndices[j][lastEdge]));
                    nextOutlineDirectConnection.Add(currentOutlineIndices[j][k]);

                    // Create the point in this outline that the point will connect to.
                    nextOutlineNextConnections.Add(nextEdge);

                    // Figure out what index this point will have.
                    nextVertexIndex.Add(vertexCount + k);

                    // Add the new point to the next outline.
                    nextOutlineVecs[j].Add(farPoint);
                    nextOutlineEdges[j].Add(currentOutlineEdges[j][k]);
                }

                // Merge points together that flipped direction.
                // List of points that need to be merged together.
                List<List<Vector3>> needToBeMergedIndices = new List<List<Vector3>>();
                // Check if the line switched direction (should be merged into one point).
                for (int l = 0, k = 0; k < nextOutlineVecs[j].Count; k++) {
                    int nextK = ((k + 1) >= nextOutlineVecs[j].Count) ? 0 : k + 1;

                    // Calculate edge directions.
                    Vector2 newEdgeDirection = (nextOutlineVecs[j][nextK] - nextOutlineVecs[j][k]).normalized;
                    Vector2 lastEdgeDirection = (currentOutlineVecs[j][nextK] - currentOutlineVecs[j][k]).normalized;

                    // See if they switched directions (this will be negative if they did).
                    float switched = Vector2.Dot(newEdgeDirection, lastEdgeDirection);

                    // Mark the two points for merging.
                    if (switched <= 0) {
                        needToBeMergedIndices.Add(new List<Vector3>());
                        needToBeMergedIndices[needToBeMergedIndices.Count - 1].Add(nextOutlineVecs[j][k]);
                        needToBeMergedIndices[needToBeMergedIndices.Count - 1].Add(nextOutlineVecs[j][nextK]);

                        // Make sure we're not looping around.
                        if (nextK == 0)
                            continue;

                        // Check to see if this was a chain of flipped points.
                        for (l = nextK; l < nextOutlineVecs[j].Count; l++) {
                            int nextL = ((l + 1) >= nextOutlineVecs[j].Count) ? 0 : l + 1;

                            // Make sure we're not looping around.
                            if (nextL == 0)
                                break;

                            // Calculate edge directions.
                            newEdgeDirection = (nextOutlineVecs[j][nextL] - nextOutlineVecs[j][l]).normalized;
                            lastEdgeDirection = (currentOutlineVecs[j][nextL] - currentOutlineVecs[j][l]).normalized;

                            // See if they switched directions (this will be negative if they did).
                            switched = Vector2.Dot(newEdgeDirection, lastEdgeDirection);

                            // Mark the two points for merging.
                            if (switched <= 0) {
                                needToBeMergedIndices[needToBeMergedIndices.Count - 1].Add(nextOutlineVecs[j][nextL]);
                            }
                            else {
                                break;
                            }
                        }

                        // Move the point we've checked up to.
                        k = l;
                    }
                }

                // Find out if two points are very close together and should be merged into one point.
                for (int l = 0, k = 0; k < nextOutlineVecs[j].Count; k++) {
                    int nextK = ((k + 1) >= nextOutlineVecs[j].Count) ? 0 : k + 1;

                    // Calculate the distance.
                    float dist = Vector2.Distance(nextOutlineVecs[j][k], nextOutlineVecs[j][nextK]);

                    // Mark the two points for merging if they are closer than the average edge distance.
                    // This is because that would mean that the points would be more than twice the edge
                    // distance from each other on next expansion.
                    if (dist <= edgeDistance * percentStep / 10f) {
                        needToBeMergedIndices.Add(new List<Vector3>());
                        needToBeMergedIndices[needToBeMergedIndices.Count - 1].Add(nextOutlineVecs[j][k]);
                        needToBeMergedIndices[needToBeMergedIndices.Count - 1].Add(nextOutlineVecs[j][nextK]);

                        // Make sure we're not looping around.
                        if (nextK == 0)
                            continue;

                        // Check to see if more than two points should be merged.
                        for (l = nextK; l < nextOutlineVecs[j].Count; l++) {
                            int nextL = ((l + 1) >= nextOutlineVecs[j].Count) ? 0 : l + 1;

                            // Make sure we're not looping around.
                            if (nextL == 0)
                                break;

                            // Calculate the distance.
                            dist = Vector2.Distance(nextOutlineVecs[j][nextK], nextOutlineVecs[j][nextL]);

                            // Mark the two points for merging.
                            if (dist <= edgeDistance * percentStep) {
                                needToBeMergedIndices[needToBeMergedIndices.Count - 1].Add(nextOutlineVecs[j][nextL]);
                            }
                            else {
                                break;
                            }
                        }

                        // Move the point we've checked up to.
                        k = l;
                    }
                }

                // Merge the points together.
                for (int k = 0; k < needToBeMergedIndices.Count; k++) {
                    // Find the initial point in the outline.
                    int point = -1;
                    Vector3 mergePoint = new Vector3();

                    // Find the merge point and remove the excess points.
                    for (int l = 0; l < needToBeMergedIndices[k].Count; l++) {
                        for (int n = 0; n < nextOutlineVecs[j].Count; n++) {
                            if (nextOutlineVecs[j][n] == needToBeMergedIndices[k][l]) {
                                // Add the point to the merge index.
                                mergePoint += nextOutlineVecs[j][n];

                                // See if we found the initial point (this point should be kept).
                                if (l == 0) {
                                    point = n;
                                    break;
                                }

                                // Add previous connections to this point so that we don't create gaps.
                                for (int m = 0; m < nextOutlinePreviousConnections[n].Count; m++) {
                                    if (!nextOutlinePreviousConnections[point].Contains(nextOutlinePreviousConnections[n][m])) {
                                        // Find out where this connection should be placed.
                                        for (int a = 0; a < nextOutlinePreviousConnections[point].Count; a++) {
                                            // Make sure we don't readd the same point multiple times.
                                            if (nextOutlinePreviousConnections[point].Contains(nextOutlinePreviousConnections[n][m]))
                                                continue;

                                            // If this point's x is the other points y, it should be
                                            // placed in front of it.
                                            if (nextOutlinePreviousConnections[point][a].x == nextOutlinePreviousConnections[n][m].y) {
                                                nextOutlinePreviousConnections[point].Insert(a, nextOutlinePreviousConnections[n][m]);
                                                // Make sure to move a forward.
                                                a++;
                                            }
                                            // Otherwise, if this point's y is the other's x, it should
                                            // be placed behind.
                                            else if (nextOutlinePreviousConnections[point][a].y == nextOutlinePreviousConnections[n][m].x)
                                                nextOutlinePreviousConnections[point].Insert(a + 1, nextOutlinePreviousConnections[n][m]);
                                        }

                                    }
                                }
                                // Correct the next point this will connect to.
                                nextOutlineNextConnections[point] = (nextOutlineNextConnections[point] > nextOutlineVecs[j].Count - 2) ? 0 : nextOutlineNextConnections[point];

                                // Correct the direct point this is above to be the later one now.
                                nextOutlineDirectConnection[point] = nextOutlineDirectConnection[n];

                                // Make sure we keep point accurate.
                                if (n < point) {
                                    point--;
                                }

                                // Make sure we correct each index for the triangle points.
                                for (int m = nextOutlineVecs[j].Count - 1; m >= 0; m--) {
                                    if (m > n) {
                                        nextVertexIndex[m]--;
                                        if (nextOutlineNextConnections[m] != 0)
                                            nextOutlineNextConnections[m] = (nextOutlineNextConnections[m - 1] > nextOutlineVecs[j].Count - 1) ? 0 : nextOutlineNextConnections[m - 1];
                                    }
                                }

                                // Otherwise we need to remove this point.
                                nextOutlineVecs[j].RemoveAt(n);
                                nextOutlineEdges[j].RemoveAt(n);
                                nextOutlineNextConnections.RemoveAt(n);
                                nextOutlinePreviousConnections.RemoveAt(n);
                                nextOutlineDirectConnection.RemoveAt(n);
                                nextVertexIndex.RemoveAt(n);
                                break;
                            }
                        }

                        if (point == -1) {
                            needToBeMergedIndices[k].RemoveAt(l);
                            l--;
                        }
                    }

                    // Make sure we still have the right point.
                    for (int l = 0; l < nextOutlineVecs[j].Count; l++) {
                        if (nextOutlineVecs[j][l] == needToBeMergedIndices[k][0]) {
                            point = l;
                            break;
                        }
                    }

                    // If we didn't find the point, then we'll skip ahead.
                    if (point == -1)
                        continue;

                    // Fix the points position.
                    mergePoint /= needToBeMergedIndices[k].Count;

                    // Apply the new info.
                    nextOutlineVecs[j][point] = mergePoint;
                }

                // Check each outline to see if the new edges are crossing each other. If they are, we go through
                // every other line to see where it comes back across. We know that if one line crosses, it has to
                // return, so once we find that, we can project all the points that are across the line that gets
                // crossed onto it.
                for (int k = 0; k < nextOutlineVecs[j].Count - 2; k++) {
                    // Initialize variables.
                    int l = 0, nextL = 0, nextK = 0, t = -1, nextT = -1, diff = 0, count = 0;
                    // Initialize cross point.
                    Vector3 crossCheckPoint = new Vector3(float.NegativeInfinity, float.NegativeInfinity);

                    // Initialize next crossing point.
                    Vector3 nextCrossCheck = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

                    // Whether the second line crossing is between nextK and l or nextL and k.
                    bool betweenNextKandL = true;

                    // Find the next point of a line from the point "k".
                    nextK = ((k + 1) >= nextOutlineVecs[j].Count) ? 0 : k + 1;
                    // Get the line segment between k and nextK.
                    Vector2 kSegment = nextOutlineVecs[j][nextK] - nextOutlineVecs[j][k];
                    // Intialize lSegment.
                    Vector2 lSegment = new Vector2();

                    // Go through each next line and find if they cross with the current line (k to nextK) anywhere.
                    for (l = k + 2; l < nextOutlineVecs[j].Count; l++) {
                        // Find the next point of a line from the point "l".
                        nextL = ((l + 1) >= nextOutlineVecs[j].Count) ? 0 : l + 1;

                        // Make sure we don't check ourselves or go around.
                        if (k >= l || k == nextL)
                            continue;

                        // Get the line segment between l and nextL.
                        lSegment = nextOutlineVecs[j][nextL] - nextOutlineVecs[j][l];

                        // Find the point where they cross (if any).
                        crossCheckPoint = this.PointOnTwoLines(nextOutlineVecs[j][k], kSegment, nextOutlineVecs[j][l], lSegment);

                        // Bring it into the correct Z-layer.
                        crossCheckPoint.z = z;

                        // If the lines cross, we need to find out two things:
                        // Which lines are crossing.
                        // And how many points and which points are inbetween the crossing.
                        if (crossCheckPoint.x != float.NegativeInfinity && crossCheckPoint.y != float.NegativeInfinity) {
                            // Make sure the point we found isn't one of the end points of the lines.
                            // If it is, skip this point.
                            if (crossCheckPoint == nextOutlineVecs[j][l] || crossCheckPoint == nextOutlineVecs[j][nextL] ||
                                crossCheckPoint == nextOutlineVecs[j][k] || crossCheckPoint == nextOutlineVecs[j][nextK]) {

                                crossCheckPoint = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                                continue;
                            }

                            // First we're going to check if the line comes across between nextK and l.
                            int a = 0;

                            // Find out how many points are between k and nextL.
                            diff = Mathf.Abs(l - nextK);

                            // Initialize tSegment.
                            Vector2 tSegment = new Vector2();

                            // Go through each line now.
                            for (a = 0; a < diff - 1; a++) {
                                count++;
                                // Get the points to check for next.
                                t = ((nextK + a) >= nextOutlineVecs[j].Count) ? (nextK + a - nextOutlineVecs[j].Count) : (nextK + a);
                                nextT = ((nextK + a + 1) >= nextOutlineVecs[j].Count) ? (nextK + a + 1 - nextOutlineVecs[j].Count) : (nextK + a + 1);

                                // Get the line segment between t and nextT.
                                tSegment = nextOutlineVecs[j][nextT] - nextOutlineVecs[j][t];

                                // Find if tSegment crosses the lSegment line.
                                nextCrossCheck = this.PointOnTwoLines(nextOutlineVecs[j][l], lSegment, nextOutlineVecs[j][t], tSegment);

                                // Bring it into the correct Z-layer.
                                nextCrossCheck.z = z;

                                if (nextCrossCheck.x != float.NegativeInfinity && nextCrossCheck.y != float.NegativeInfinity)
                                    break;
                            }

                            // If we found where the line crosses again, we'll break out.
                            if (t != -1 && nextCrossCheck.x != float.NegativeInfinity && nextCrossCheck.y != float.NegativeInfinity)
                                break;

                            // Reset the count.
                            count = 0;

                            // Set that we're looking between nextL and k now.
                            betweenNextKandL = false;

                            // Find out how many points are between k and nextL.
                            diff = Mathf.Abs(nextOutlineVecs[j].Count - diff - 2);

                            // Initialize tSegment.
                            tSegment = new Vector2();

                            // Go through each line now.
                            for (a = 0; a < diff - 1; a++) {
                                count++;
                                // Get the points to check for next.
                                t = ((nextL + a) >= nextOutlineVecs[j].Count) ? (nextL + a - nextOutlineVecs[j].Count) : (nextL + a);
                                nextT = ((nextL + a + 1) >= nextOutlineVecs[j].Count) ? (nextL + a + 1 - nextOutlineVecs[j].Count) : (nextL + a + 1);

                                // Get the line segment between t and nextT.
                                tSegment = nextOutlineVecs[j][nextT] - nextOutlineVecs[j][t];

                                // Find if tSegment crosses the lSegment line.
                                nextCrossCheck = this.PointOnTwoLines(nextOutlineVecs[j][k], kSegment, nextOutlineVecs[j][t], tSegment);

                                // Bring it into the correct Z-layer.
                                nextCrossCheck.z = z;

                                if (nextCrossCheck.x != float.NegativeInfinity && nextCrossCheck.y != float.NegativeInfinity)
                                    break;
                            }

                            // If we found where the line crosses again, we'll break out.
                            if (t != -1 && nextCrossCheck.x != float.NegativeInfinity && nextCrossCheck.y != float.NegativeInfinity)
                                break;
                        }
                    }

                    // Check again to make sure the lines are currently crossing. If they're not, this line
                    // doesn't cross any other lines on this outline and we should continue.
                    if (crossCheckPoint.x == float.NegativeInfinity || crossCheckPoint.y == float.NegativeInfinity
                        || nextCrossCheck.x == float.NegativeInfinity && nextCrossCheck.y == float.NegativeInfinity) {
                        continue;
                    }

                    int nextVert;
                    int pointToMove, pointToMoveFront, pointToInsertLead, pointToInsertFrontLead, pointToInsertTail, pointToInsertFrontTail;

                    // Figure out which points we're moving and inserting at.
                    if (betweenNextKandL) {
                        pointToMove = nextK;
                        pointToMoveFront = k;
                        pointToInsertLead = nextT;
                        pointToInsertFrontLead = t;
                        pointToInsertTail = nextL;
                        pointToInsertFrontTail = l;
                    }
                    else {
                        pointToMove = nextL;
                        pointToMoveFront = l;
                        pointToInsertLead = nextT;
                        pointToInsertFrontLead = t;
                        pointToInsertTail = nextK;
                        pointToInsertFrontTail = k;
                    }

                    // Figure out which direction we're adding points to the line.
                    float direction = Vector2.Dot((nextOutlineVecs[j][pointToMoveFront] - nextOutlineVecs[j][pointToInsertLead]), -(nextOutlineVecs[j][pointToInsertTail] - nextOutlineVecs[j][pointToInsertFrontTail]));

                    // Go through each point that is crossing "over" the line and move them onto the line.
                    if (t != pointToMove) {
                        for (int a = 0; a < count; a++) {
                            // Find which point we're looking at.
                            int r = ((pointToMove + a) >= nextOutlineVecs[j].Count) ? (pointToMove + a - nextOutlineVecs[j].Count) : (pointToMove + a);

                            if (a == 0) {
                                // Move the first point to the intial crossing point.
                                nextOutlineVecs[j][r] = crossCheckPoint;
                            }
                            else {
                                // Project the point onto the line.
                                if (betweenNextKandL) {
                                    nextOutlineVecs[j][r] = nextOutlineVecs[j][l] + Vector3.Project(nextOutlineVecs[j][r] - nextOutlineVecs[j][l], lSegment);
                                }
                                else {
                                    nextOutlineVecs[j][r] = nextOutlineVecs[j][k] + Vector3.Project(nextOutlineVecs[j][r] - nextOutlineVecs[j][k], kSegment);
                                }
                            }

                            // Put the new point into the outline where r now is.
                            nextOutlineVecs[j].Insert(pointToInsertTail, nextOutlineVecs[j][r]);
                            nextOutlineEdges[j].Insert(pointToInsertTail, nextOutlineEdges[j][pointToInsertTail]);

                            // Add the new triangle.
                            nextOutlinePreviousConnections.Insert(pointToInsertTail, new List<Vector2Int>());
                            nextOutlinePreviousConnections[pointToInsertTail].Add(new Vector2Int(nextOutlinePreviousConnections[pointToInsertFrontTail][nextOutlinePreviousConnections[pointToInsertFrontTail].Count - 1].x,
                                nextOutlinePreviousConnections[pointToInsertFrontTail][nextOutlinePreviousConnections[pointToInsertFrontTail].Count - 1].x));

                            nextVert = ((nextOutlineNextConnections[pointToInsertFrontTail] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[pointToInsertFrontTail] + 1;
                            nextOutlineNextConnections.Insert(pointToInsertTail, nextVert);
                            nextOutlineDirectConnection.Insert(pointToInsertTail, -1);
                            nextVertexIndex.Insert(pointToInsertTail, nextVertexIndex[pointToInsertFrontTail] + 1);

                            // Correct the indices and connections of the later vertices.
                            for (int n = pointToInsertTail + 1; n < nextVertexIndex.Count; n++) {
                                nextVertexIndex[n]++;

                                // Make sure we don't remove the connection to the first point.
                                if (n != nextVertexIndex.Count - 1)
                                    nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                            }

                            // Make sure we increase the point we're checking.
                            if (pointToMove > pointToInsertLead)
                                pointToMove++;

                            if (pointToMove > pointToInsertTail)
                                pointToMove++;

                            if (direction > 0) {
                                pointToInsertTail++;
                                pointToInsertFrontTail++;
                            }
                        }
                    }
                    // If there's not, we need to move one point and create a new one.
                    else {
                        // Project the first point onto the line.
                        nextOutlineVecs[j][pointToMove] = crossCheckPoint; //nextOutlineVecs[j][l] + Vector3.Project(nextOutlineVecs[j][t] - nextOutlineVecs[j][l], lSegment);

                        // Put the new point into the outline.
                        nextOutlineVecs[j].Insert(pointToInsertTail, crossCheckPoint);
                        nextOutlineEdges[j].Insert(pointToInsertTail, nextOutlineEdges[j][pointToInsertTail]);

                        // Add the new triangle.
                        nextOutlinePreviousConnections.Insert(pointToInsertTail, new List<Vector2Int>());
                        nextOutlinePreviousConnections[pointToInsertTail].Add(new Vector2Int(nextOutlinePreviousConnections[pointToInsertFrontTail][nextOutlinePreviousConnections[pointToInsertFrontTail].Count - 1].x,
                            nextOutlinePreviousConnections[pointToInsertFrontTail][nextOutlinePreviousConnections[pointToInsertFrontTail].Count - 1].x));

                        nextVert = ((nextOutlineNextConnections[pointToInsertFrontTail] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[pointToInsertFrontTail] + 1;
                        nextOutlineNextConnections.Insert(pointToInsertTail, nextVert);
                        nextOutlineDirectConnection.Insert(pointToInsertTail, -1);
                        nextVertexIndex.Insert(pointToInsertTail, nextVertexIndex[pointToInsertFrontTail] + 1);

                        // Correct the indices and connections of the later vertices.
                        for (int n = pointToInsertTail + 1; n < nextVertexIndex.Count; n++) {
                            nextVertexIndex[n]++;

                            // Make sure we don't remove the connection to the first point.
                            if (n != nextVertexIndex.Count - 1)
                                nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                        }

                        // Make sure we increase the point we're checking.
                        if (pointToInsertTail < pointToInsertLead) {
                            pointToInsertLead++;
                            pointToInsertFrontLead++;
                        }
                        if (pointToInsertTail < pointToMove)
                            pointToMove++;

                        // Create the second point.

                        // Put the new point into the outline.
                        nextOutlineVecs[j].Insert(pointToInsertLead, nextCrossCheck);
                        nextOutlineEdges[j].Insert(pointToInsertLead, nextOutlineEdges[j][pointToInsertLead]);

                        // Add the new triangle.
                        nextOutlinePreviousConnections.Insert(pointToInsertLead, new List<Vector2Int>());
                        nextOutlinePreviousConnections[pointToInsertLead].Add(new Vector2Int(nextOutlinePreviousConnections[pointToInsertFrontLead][nextOutlinePreviousConnections[pointToInsertFrontLead].Count - 1].x,
                            nextOutlinePreviousConnections[pointToInsertFrontLead][nextOutlinePreviousConnections[pointToInsertFrontLead].Count - 1].x));

                        nextVert = ((nextOutlineNextConnections[pointToInsertFrontLead] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[pointToInsertFrontLead] + 1;
                        nextOutlineNextConnections.Insert(pointToInsertLead, nextVert);
                        nextOutlineDirectConnection.Insert(pointToInsertLead, -1);
                        nextVertexIndex.Insert(pointToInsertLead, nextVertexIndex[pointToInsertFrontLead] + 1);

                        // Correct the indices and connections of the later vertices.
                        for (int n = pointToInsertLead + 1; n < nextVertexIndex.Count; n++) {
                            nextVertexIndex[n]++;

                            // Make sure we don't remove the connection to the first point.
                            if (n != nextVertexIndex.Count - 1)
                                nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                        }

                        if (pointToInsertLead < pointToInsertTail) {
                            pointToInsertTail++;
                            pointToInsertFrontTail++;
                        }
                        if (pointToInsertLead < pointToMove)
                            pointToMove++;

                        if (direction > 0f) {
                            pointToInsertTail++;
                            pointToInsertFrontTail++;
                        }

                        // Put the new point into the outline.
                        nextOutlineVecs[j].Insert(pointToInsertTail, nextCrossCheck);
                        nextOutlineEdges[j].Insert(pointToInsertTail, nextOutlineEdges[j][pointToInsertTail]);

                        // Add the new triangle.
                        nextOutlinePreviousConnections.Insert(pointToInsertTail, new List<Vector2Int>());
                        nextOutlinePreviousConnections[pointToInsertTail].Add(new Vector2Int(nextOutlinePreviousConnections[pointToInsertFrontTail][nextOutlinePreviousConnections[pointToInsertFrontTail].Count - 1].x,
                            nextOutlinePreviousConnections[pointToInsertFrontTail][nextOutlinePreviousConnections[pointToInsertFrontTail].Count - 1].x));

                        nextVert = ((nextOutlineNextConnections[pointToInsertFrontTail] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[pointToInsertFrontTail] + 1;
                        nextOutlineNextConnections.Insert(pointToInsertTail, nextVert);
                        nextOutlineDirectConnection.Insert(pointToInsertTail, -1);
                        nextVertexIndex.Insert(pointToInsertTail, nextVertexIndex[pointToInsertFrontTail] + 1);

                        // Correct the indices and connections of the later vertices.
                        for (int n = pointToInsertTail + 1; n < nextVertexIndex.Count; n++) {
                            nextVertexIndex[n]++;

                            // Make sure we don't remove the connection to the first point.
                            if (n != nextVertexIndex.Count - 1)
                                nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                        }

                    }
                }

                #region Line Crossing Splitter V1

                /**
                // Create a list of indices to skip (they were already checked).
                List<int> checkedIndexes = new List<int>();
                // Check each outline to see if the new edges are crossing each other. If they are, we
                // should find the midpoint of the lines and begin correcting them. Then we should check
                // if the outline has now become more than one polygon and split the outline up.
                for (int l, nextL, nextK, k = 0; k < nextOutlineVecs[j].Count - 2;) {
                    // Initialize cross point.
                    Vector3 crossCheckPoint = new Vector3(float.NegativeInfinity, float.NegativeInfinity);
                    // Set up next variables.
                    nextL = 0;
                    nextK = ((k + 1) >= nextOutlineVecs[j].Count) ? 0 : k + 1;
                    // Get the line segment between k and nextK.
                    Vector2 kSegment = nextOutlineVecs[j][nextK] - nextOutlineVecs[j][k];
                    // Intialize lSegment.
                    Vector2 lSegment = new Vector2();

                    // Go through each next line and find if they cross with the current line (k to nextK) anywhere.
                    for (l = k + 2; l <= nextOutlineVecs[j].Count - 1; l++) {
                        // Find the next point of a line from the "l".
                        nextL = ((l + 1) >= nextOutlineVecs[j].Count) ? 0 : l + 1;

                        // Make sure we don't want to skip this set.
                        if (checkedIndexes.Contains(l) || k >= l || k == nextL)
                            continue;

                        // Add this point to the checked list.
                        checkedIndexes.Add(l);

                        // Get the line segment between l and nextL.
                        lSegment = nextOutlineVecs[j][nextL] - nextOutlineVecs[j][l];

                        // Find the point where they cross (if any).
                        crossCheckPoint = this.PointOnTwoLines(nextOutlineVecs[j][k], kSegment, nextOutlineVecs[j][l], lSegment);

                        // Bring it into the correct Z-layer.
                        crossCheckPoint.z = nextOutlineVecs[j][k].z;

                        // If the lines cross, we'll break out and fix these current lines.
                        if (crossCheckPoint.x != float.NegativeInfinity && crossCheckPoint.y != float.NegativeInfinity) {
                            // Make sure the point we found isn't one of the end points of the lines.
                            // If it is, skip this point.
                            if (crossCheckPoint == nextOutlineVecs[j][l] || crossCheckPoint == nextOutlineVecs[j][nextL] ||
                                crossCheckPoint == nextOutlineVecs[j][k] || crossCheckPoint == nextOutlineVecs[j][nextK]) {

                                crossCheckPoint = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                                continue;
                            }

                            // Otherwise, this point crosses somewhere in between the two lines.
                            break;
                        }
                    }

                    // Check again to make sure the lines are currently crossing. If they're not, this line
                    // doesn't cross any other lines on this outline and we should continue.
                    if (crossCheckPoint.x == float.NegativeInfinity || crossCheckPoint.y == float.NegativeInfinity) {
                        checkedIndexes.Clear();
                        k++;
                        continue;
                    }

                    int nextVert;

                    // We need to find out a few things about this crossing point. Each has a different approach:
                    // Is it just one point crossing over?
                    // Is it two or more points?

                    // Find out if it's just one point crossing over.
                    int nextNextL = ((nextL + 1) >= nextOutlineVecs[j].Count) ? 0 : nextL + 1;
                    if (nextNextL != k) {
                        Vector2 nextLSegment = nextOutlineVecs[j][nextNextL] - nextOutlineVecs[j][nextL];
                        Vector3 nextCrossCheck = this.PointOnTwoLines(nextOutlineVecs[j][k], kSegment, nextOutlineVecs[j][nextL], nextLSegment);
                        nextCrossCheck.z = z;

                        // See if the next line coming out of nextL crosses back over.
                        if (crossCheckPoint.x != float.NegativeInfinity && crossCheckPoint.y != float.NegativeInfinity) {
                            // If it crosses again, that means we need to include two new major points.

                            // First introduce the first point (where it originally crosses over).
                            // Move the point to the crossing point.
                            nextOutlineVecs[j][nextL] = crossCheckPoint;

                            // Put the new point into the outline.
                            nextOutlineVecs[j].Insert(nextK, crossCheckPoint);
                            nextOutlineEdges[j].Insert(nextK, nextOutlineEdges[j][nextK]);

                            // Add the new triangle.
                            nextOutlinePreviousConnections.Insert(nextK, new List<Vector2Int>());
                            nextOutlinePreviousConnections[nextK].Add(new Vector2Int(nextOutlinePreviousConnections[k][nextOutlinePreviousConnections[k].Count - 1].x, nextOutlinePreviousConnections[k][nextOutlinePreviousConnections[k].Count - 1].x));
                            nextVert = ((nextOutlineNextConnections[k] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[k] + 1;
                            nextOutlineNextConnections.Insert(nextK, nextVert);
                            nextOutlineDirectConnection.Insert(nextK, -1);
                            nextVertexIndex.Insert(nextK, nextVertexIndex[k] + 1);

                            // Correct the indices and connections of the later vertices.
                            for (int n = nextK + 1; n < nextVertexIndex.Count; n++) {
                                nextVertexIndex[n]++;

                                // Make sure we don't remove the connection to the first point.
                                if (n != nextVertexIndex.Count - 1)
                                    nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                            }

                            // Make sure we increase each index that we've already checked that's past this point.
                            for (int a = 0; a < checkedIndexes.Count; a++) {
                                if (checkedIndexes[a] >= (nextK))
                                    checkedIndexes[a]++;
                            }
                            // Make sure to increase the points up if they're in the middle of the mesh.
                            if (l > nextK)
                                l++;
                            if (nextL > nextK)
                                nextL++;
                            if (nextNextL > nextK)
                                nextNextL++;

                            // Now introduce the next point.
                            float direction = Vector2.Dot(kSegment, (nextOutlineVecs[j][nextNextL] - nextOutlineVecs[j][l]));

                            if (direction < 0f) {
                                // Point on the original line.

                                // Put the new point into the outline.
                                nextOutlineVecs[j].Insert(nextK, nextCrossCheck);
                                nextOutlineEdges[j].Insert(nextK, nextOutlineEdges[j][nextK]);

                                // Add the new triangle.
                                nextOutlinePreviousConnections.Insert(nextK, new List<Vector2Int>());
                                nextOutlinePreviousConnections[nextK].Add(new Vector2Int(nextOutlinePreviousConnections[k][nextOutlinePreviousConnections[k].Count - 1].x, nextOutlinePreviousConnections[k][nextOutlinePreviousConnections[k].Count - 1].x));
                                nextVert = ((nextOutlineNextConnections[k] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[k] + 1;
                                nextOutlineNextConnections.Insert(nextK, nextVert);
                                nextOutlineDirectConnection.Insert(nextK, -1);
                                nextVertexIndex.Insert(nextK, nextVertexIndex[k] + 1);

                                // Correct the indices and connections of the later vertices.
                                for (int n = nextK + 1; n < nextVertexIndex.Count; n++) {
                                    nextVertexIndex[n]++;

                                    // Make sure we don't remove the connection to the first point.
                                    if (n != nextVertexIndex.Count - 1)
                                        nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                                }

                                // Make sure we increase each index that we've already checked that's past this point.
                                for (int a = 0; a < checkedIndexes.Count; a++) {
                                    if (checkedIndexes[a] >= (nextK))
                                        checkedIndexes[a]++;
                                }
                                // Make sure to increase the points up if they're in the middle of the mesh.
                                if (l > nextK)
                                    l++;
                                if (nextL > nextK)
                                    nextL++;
                                if (nextNextL > nextK)
                                    nextNextL++;

                                // Point on the crossing line.

                                // Put the new point into the outline.
                                nextOutlineVecs[j].Insert(nextNextL, nextCrossCheck);
                                nextOutlineEdges[j].Insert(nextNextL, nextOutlineEdges[j][nextNextL]);

                                // Add the new triangle.
                                nextOutlinePreviousConnections.Insert(nextNextL, new List<Vector2Int>());
                                nextOutlinePreviousConnections[nextNextL].Add(new Vector2Int(nextOutlinePreviousConnections[nextL][nextOutlinePreviousConnections[nextL].Count - 1].x, nextOutlinePreviousConnections[nextL][nextOutlinePreviousConnections[nextL].Count - 1].x));
                                nextVert = ((nextOutlineNextConnections[nextL] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[nextL] + 1;
                                nextOutlineNextConnections.Insert(nextNextL, nextVert);
                                nextOutlineDirectConnection.Insert(nextNextL, -1);
                                nextVertexIndex.Insert(nextNextL, nextVertexIndex[nextL] + 1);

                                // Correct the indices and connections of the later vertices.
                                for (int n = nextNextL + 1; n < nextVertexIndex.Count; n++) {
                                    nextVertexIndex[n]++;

                                    // Make sure we don't remove the connection to the first point.
                                    if (n != nextVertexIndex.Count - 1)
                                        nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                                }

                                // Make sure we increase each index that we've already checked that's past this point.
                                for (int a = 0; a < checkedIndexes.Count; a++) {
                                    if (checkedIndexes[a] >= nextNextL)
                                        checkedIndexes[a]++;
                                }
                            }

                        }
                    }
                    else {
                        // If two or more do cross, we need to figure out which points we need to bring together to
                        // merge the two lines together.

                        // Project the line segment that crosses onto the normal of the current line. If this is a 
                        // positive value, then that means that l should be brought to the crossing point. If it's
                        // negative, that means that nextL should be brought to the crossing point. If it's 0,
                        // that means that the lines are parallel.
                        Vector2 kNormal = new Vector2(kSegment.y, -kSegment.x).normalized;
                        float projection = Vector2.Dot(lSegment, kNormal);

                        Vector2 kOffset = nextOutlineVecs[j][k] - meshPoints[nextOutlineDirectConnection[k]];

                        int pointToMove = 0;

                        if (projection == 0) {
                            // Lines are parallel. This shouldn't happen. It means the two points create a thin line.

                        }
                        else if (projection > 0) {
                            // l brought to crossing point.
                            //nextOutlineVecs[j][l] = crossCheckPoint;
                            pointToMove = l;
                        }
                        else {
                            // nextL brought to crossing point.
                            //nextOutlineVecs[j][nextL] = crossCheckPoint;
                            pointToMove = nextL;
                        }

                        /* -- Need to work on this more in the future. --
                         * 
                        // Find the offset that the point moved before crossing.
                        Vector2 lOffset = meshPoints[nextOutlineDirectConnection[pointToMove]] - nextOutlineVecs[j][pointToMove];

                        // Figure out how far it moved.
                        float t, u;
                        Vector3 trueCrossPoint;
                        trueCrossPoint = this.PointOnTwoLines(nextOutlineVecs[j][k], (nextOutlineVecs[j][nextK] - nextOutlineVecs[j][k]), meshPoints[nextOutlineDirectConnection[pointToMove]], lOffset, out t, out u);
                        trueCrossPoint.z = z;

                        DebugLines.Instance.addLine(trueCrossPoint, meshPoints[nextOutlineDirectConnection[pointToMove]], new Color(1f, 0.7f, 0));

                        for (int a = 0; a < nextOutlineVecs[j].Count; a++) {
                            int next = ((a + 1) >= nextOutlineVecs[j].Count) ? 0 : a + 1;
                            DebugLines.Instance.addLine(nextOutlineVecs[j][a], nextOutlineVecs[j][next], new Color(1, 0.85f, 0));
                        }

                        // Move the point to the crossing point.
                        nextOutlineVecs[j][pointToMove] = crossCheckPoint;

                        // Put the new point into the outline.
                        nextOutlineVecs[j].Insert(nextK, crossCheckPoint);
                        nextOutlineEdges[j].Insert(nextK, nextOutlineEdges[j][nextK]);

                        // Add the new triangle.
                        nextOutlinePreviousConnections.Insert(k + 1, new List<Vector2Int>());
                        nextOutlinePreviousConnections[k + 1].Add(new Vector2Int(nextOutlinePreviousConnections[k][nextOutlinePreviousConnections[k].Count - 1].x, nextOutlinePreviousConnections[k][nextOutlinePreviousConnections[k].Count - 1].x));
                        nextVert = ((nextOutlineNextConnections[k] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[k] + 1;
                        nextOutlineNextConnections.Insert(k + 1, nextVert);
                        nextOutlineDirectConnection.Insert(nextK, -1);
                        nextVertexIndex.Insert(k + 1, nextVertexIndex[k] + 1);

                        // Correct the indices and connections of the later vertices.
                        for (int n = k + 2; n < nextVertexIndex.Count; n++) {
                            nextVertexIndex[n]++;

                            // Make sure we don't remove the connection to the first point.
                            if (n != nextVertexIndex.Count - 1)
                                nextOutlineNextConnections[n] = ((nextOutlineNextConnections[n] + 1) > nextOutlineNextConnections.Count) ? 0 : nextOutlineNextConnections[n] + 1;
                        }

                        // Make sure we increase each index that we've already checked that's past this point.
                        for (int a = 0; a < checkedIndexes.Count; a++) {
                            if (checkedIndexes[a] >= (k + 1))
                                checkedIndexes[a]++;
                        }

                        // See if we've checked every index already and skip ahead if we have.
                        if (checkedIndexes.Contains(nextOutlineVecs[j].Count - 2)) {
                            checkedIndexes.Clear();
                            k++;
                        }
                    }
                }
                */

                #endregion

                // Add all the points to the meshPoints list.
                meshPoints.AddRange(nextOutlineVecs[j]);

                // Start creating the triangles.
                for (int k = 0; k < nextOutlinePreviousConnections.Count; k++) {
                    for (int l = 0; l < nextOutlinePreviousConnections[k].Count; l++) {
                        // Create the bottom strip triangle(s).
                        if (nextOutlinePreviousConnections[k][l].x != nextOutlinePreviousConnections[k][l].y && nextOutlineDirectConnection[k] != -1) {
                            //DebugLines.Instance.addLine(meshPoints[nextOutlinePreviousConnections[k][l].x], meshPoints[nextVertexIndex[k]], Color.blue);
                            //DebugLines.Instance.addLine(meshPoints[nextOutlinePreviousConnections[k][l].y], meshPoints[nextVertexIndex[k]], Color.blue);
                            //DebugLines.Instance.addLine(meshPoints[nextOutlinePreviousConnections[k][l].x], meshPoints[nextOutlinePreviousConnections[k][l].y], Color.blue);

                            this.indexList.Add(nextVertexIndex[k]);
                            this.indexList.Add(nextOutlinePreviousConnections[k][l].x);
                            this.indexList.Add(nextOutlinePreviousConnections[k][l].y);
                        }
                    }


                    // Create the top strip triangle.
                    // Make sure there's more than one point in this outline.
                    if (nextOutlineNextConnections.Count > 1) {
                        // Make sure this point has a direct connection. Otherwise, rely on its previous
                        // connections list.
                        if (nextOutlineDirectConnection[k] != -1) {
                            this.indexList.Add(nextOutlineDirectConnection[k]);
                            this.indexList.Add(nextVertexIndex[k]);
                            this.indexList.Add(nextOutlineNextConnections[k] + vertexCount);

                            //DebugLines.Instance.addLine(meshPoints[nextOutlineDirectConnection[k]], meshPoints[nextVertexIndex[k]], Color.red);
                            //DebugLines.Instance.addLine(meshPoints[nextOutlineDirectConnection[k]], meshPoints[nextOutlineNextConnections[k] + vertexCount], Color.red);
                            //DebugLines.Instance.addLine(meshPoints[nextOutlineNextConnections[k] + vertexCount], meshPoints[nextVertexIndex[k]], Color.red);
                        }
                        else {
                            this.indexList.Add(nextOutlinePreviousConnections[k][0].x);
                            this.indexList.Add(nextVertexIndex[k]);
                            this.indexList.Add(nextOutlineNextConnections[k] + vertexCount);

                            //DebugLines.Instance.addLine(meshPoints[nextOutlinePreviousConnections[k][0].x], meshPoints[nextVertexIndex[k]], Color.red);
                            //DebugLines.Instance.addLine(meshPoints[nextOutlinePreviousConnections[k][0].x], meshPoints[nextOutlineNextConnections[k] + vertexCount], Color.red);
                            //DebugLines.Instance.addLine(meshPoints[nextOutlineNextConnections[k] + vertexCount], meshPoints[nextVertexIndex[k]], Color.red);
                        }
                    }
                }

                // Now that we're done merging points together, we know that this is the final list of
                // vertices that we will be working with. This means we can now increase the vertex
                // count for the mesh.
                vertexCount += nextOutlineVecs[j].Count;

                // Find out if two or more points are now at the same spot. This would create a "corner"
                // where the outline is now splitting into multiple other outlines.
                // Create a list of lists of ints that keep track of each index that's sharing spots.
                List<List<int>> sharedVertexes = new List<List<int>>();
                // Create a list of indices to skip (they were already checked).
                List<int> checkedIndexes = new List<int>();
                // Whether the point is shared with any other point. If it is, we create a new list
                // if this is the first point shared and we add the points to it.
                bool sharedPoint = false;
                // Go through each set of vertices to find out if they're sharing locations with any
                // other vertices.
                for (int k = 0; k < nextOutlineVecs[j].Count; k++) {
                    // Skip this point if it's already sharing with other points.
                    if (checkedIndexes.Contains(k))
                        continue;

                    sharedPoint = false;

                    for (int l = k + 1; l < nextOutlineVecs[j].Count; l++) {
                        // Skip this point if it's already sharing with other points.
                        if (checkedIndexes.Contains(l))
                            continue;

                        // See if the two points are at the same location. This means that these two
                        // points are where an outline breaks into two or more outlines.
                        if (nextOutlineVecs[j][k] == nextOutlineVecs[j][l]) {
                            // If this is a new shared point, create a new list and add the first point.
                            if (!sharedPoint) {
                                sharedVertexes.Add(new List<int>());
                                sharedVertexes[sharedVertexes.Count - 1].Add(k);
                                checkedIndexes.Add(k);

                                sharedPoint = true;
                            }

                            // Add the point being shared to the list.
                            sharedVertexes[sharedVertexes.Count - 1].Add(l);
                            checkedIndexes.Add(l);
                        }
                    }
                }

                // If we found that the outline should be split up (there will be a list in sharedVertexes)
                // then we now need to start splitting the outline up.
                if (sharedVertexes.Count > 0) {

                    // Find out how the outline should split up.
                    List<List<int>> newOutlinesIndices = this.SplitOutline(0, nextOutlineVecs[j].Count, false, sharedVertexes);

                    // Create the lists for the next sets of outlines.
                    List<List<Vector3>> newOutlineVecs = new List<List<Vector3>>();
                    List<List<EdgeType>> newOutlineEdges = new List<List<EdgeType>>();
                    List<List<int>> newOutlineVertexIndices = new List<List<int>>();

                    // Go through each new outline index and create the outline and edges.
                    for (int k = 0; k < newOutlinesIndices.Count; k++) {

                        // See if the outline became null, a single point, or a line. If it is one of these,
                        // it's done being expanded upon.
                        if (newOutlinesIndices[k] == null || newOutlinesIndices[k].Count <= 2) {

                            for (int l = 0; l < newOutlinesIndices[k].Count; l++) {
                                int next = ((l + 1) >= newOutlinesIndices[k].Count) ? 0 : l + 1;
                                //DebugLines.Instance.addLine(nextOutlineVecs[j][newOutlinesIndices[k][l]], nextOutlineVecs[j][newOutlinesIndices[k][next]], new Color(0, 1, (i * 1.0f / loops)));
                            }

                            continue;
                        }

                        // Go through and create the new outlines.
                        for (int l = 0; l < newOutlinesIndices[k].Count; l++) {
                            // Add the lists if this is the first index.
                            if (l == 0) {
                                newOutlineVecs.Add(new List<Vector3>());
                                newOutlineEdges.Add(new List<EdgeType>());
                                newOutlineVertexIndices.Add(new List<int>());
                            }

                            // Add the vecs and edges.
                            newOutlineVecs[newOutlineVecs.Count - 1].Add(nextOutlineVecs[j][newOutlinesIndices[k][l]]);
                            newOutlineEdges[newOutlineVecs.Count - 1].Add(nextOutlineEdges[j][newOutlinesIndices[k][l]]);
                            newOutlineVertexIndices[newOutlineVertexIndices.Count - 1].Add(nextVertexIndex[newOutlinesIndices[k][l]]);
                        }
                    }

                    // See if we have any new outlines to expand. If we don't, delete the current
                    // outline because it will no longer matter.
                    if (newOutlineVecs.Count == 0) {
                        // Remove the current outlines since they no longer matter.
                        currentOutlineVecs.RemoveAt(j);
                        currentOutlineEdges.RemoveAt(j);
                        currentOutlineIndices.RemoveAt(j);

                        // Reduce j to account for this.
                        j--;

                        // Clear the next outlines.
                        nextOutlineVecs.Clear();
                        nextOutlineEdges.Clear();

                        continue;
                    }

                    // Set the current outlines to the new ones.
                    for (int k = 0; k < newOutlineVecs.Count; k++) {
                        if (k == 0) {
                            currentOutlineVecs[j] = newOutlineVecs[k];
                            currentOutlineEdges[j] = newOutlineEdges[k];
                            currentOutlineIndices[j] = newOutlineVertexIndices[k];
                        }
                        else {
                            j++;
                            currentOutlineVecs.Insert(j, newOutlineVecs[k]);
                            currentOutlineEdges.Insert(j, newOutlineEdges[k]);
                            currentOutlineIndices.Insert(j, newOutlineVertexIndices[k]);
                        }
                    }
                }
                // If the outline doesn't get split, we can just add the nextOutline to the current one.
                else {

                    // See if the outline has become a single point or line first. If it has, we're at the
                    // end of expanding for this outline.
                    if (nextOutlineVecs[j].Count <= 2) {
                        for (int k = 0; k < nextOutlineVecs[j].Count; k++) {
                            int next = ((k + 1) >= nextOutlineVecs[j].Count) ? 0 : k + 1;
                            //DebugLines.Instance.addLine(nextOutlineVecs[j][k], nextOutlineVecs[j][next], new Color(0, 1, (i * 1.0f / loops)));
                        }

                        // Remove the current outlines since they no longer matter.
                        currentOutlineVecs.RemoveAt(j);
                        currentOutlineEdges.RemoveAt(j);
                        currentOutlineIndices.RemoveAt(j);

                        // Reduce j to account for this.
                        j--;

                        // Clear the next outlines.
                        nextOutlineVecs.Clear();
                        nextOutlineEdges.Clear();

                        continue;
                    }

                    // Set the current outline to this next one.
                    currentOutlineVecs[j] = nextOutlineVecs[j];
                    currentOutlineEdges[j] = nextOutlineEdges[j];
                    currentOutlineIndices[j] = nextVertexIndex;
                }
            }

            // Create debug lines for the current outline.
            for (int j = 0; j < currentOutlineVecs.Count; j++) {
                for (int k = 0; k < currentOutlineVecs[j].Count; k++) {
                    int next = ((k + 1) >= currentOutlineVecs[j].Count) ? 0 : k + 1;
                    //DebugLines.Instance.addLine(currentOutlineVecs[j][k], currentOutlineVecs[j][next], new Color(0, 1, (i * 1.0f / loops)));
                }
            }

            // Clear the next outlines.
            nextOutlineVecs.Clear();
            nextOutlineEdges.Clear();

        }

        // Go through every outline that didn't close up and triangulate them.
        for (int i = 0; i < currentOutlineVecs.Count; i++) {
            // Get the points.
            this.tri.SetPoints(currentOutlineVecs[i].Select(v => (Vector2) v).ToArray());
            // Triangulate them.
            int[] triangles = this.tri.Triangulate();

            // Correct them to use the right points.
            for (int j = 0; j < triangles.Length; j++) {
                triangles[j] = currentOutlineIndices[i][triangles[j]];

                // Now swap this point with its previous point to flip the triangle.
                if (j % 3 == 1) {
                    int temp = triangles[j];
                    triangles[j] = triangles[j - 1];
                    triangles[j - 1] = temp;
                }
            }
            // Add the face to the index list.
            this.indexList.AddRange(triangles);
        }

        // Now that we're done calculating the edges on one side, we can copy them over to the other
        // side and correct the indices as well.
        List<Vector3> otherSideVecs = meshPoints.GetRange(_vertices.Count, meshPoints.Count - _vertices.Count);
        for (int i = 0; i < otherSideVecs.Count; i++)
            otherSideVecs[i] = new Vector3(otherSideVecs[i].x, otherSideVecs[i].y, -otherSideVecs[i].z);
        // Add it back to the list of points.
        meshPoints.AddRange(otherSideVecs);

        // Now get the other side's indices.
        int[] otherSideIndices = new int[this.indexList.Count];
        this.indexList.CopyTo(otherSideIndices);
        for (int i = 0; i < otherSideIndices.Length; i++) {
            // Raise the index if it's not one of the original points.
            if (otherSideIndices[i] > _vertices.Count - 1)
                otherSideIndices[i] += otherSideVecs.Count;

            // Now swap this point with its previous point to flip the triangle.
            if (i % 3 == 1) {
                int temp = otherSideIndices[i];
                otherSideIndices[i] = otherSideIndices[i - 1];
                otherSideIndices[i - 1] = temp;
            }
        }
        // Add it back to the list of indices.
        this.indexList.AddRange(otherSideIndices);

        this.vertexList.AddRange(meshPoints);

        DebugLines.Instance.ClearQueue();

        #endregion



        #region Expanding Mesh (V1)
        /*

        // Take the points and build a set of triangles
        tri.SetPoints(points);
        int[] frontIndex = tri.Triangulate();
        // Then convert the 2D points to 3D vertices
        Vector3[] frontPlane = points.Select(v => new Vector3(v.x, v.y)).Reverse().ToArray();
        
        // This set of vertices is all behind the first (the "back face")
        Vector3[] backPlane = frontPlane.Select(v => new Vector3(v.x, v.y)).ToArray();

        // Calcualate how far out the edges will need to be to be at the angle given.
        edgeDistance = (thickness / 2f * Mathf.Sin(Mathf.PI * 0.5f) / Mathf.Sin(sharpness * Mathf.Deg2Rad));
        
        // Build edge loops.
        List<List<int>> edgeIndices = new List<List<int>>();
        List<List<Vector3>> edgePoints = new List<List<Vector3>>();
        // Which vertex each edge point will need to connect to.
        List<int> edgeConnections = new List<int>();

        // Figure out the edge and corner points.
        for (int j = 0; j < loops; j++) {
            // Create the lists.
            List<Vector3> ePoints = new List<Vector3>();

            // Go through each corner to create round/sharp corners.
            for (int i = 0; i < vertices.Count; i++) {
                Vertex curV = vertices[i];
                Vector3 location = curV.Location;

                // Find out how many points on each edge it will take for each corner.
                int cornerPointsCount = 0;

                if (curV.Corner == CornerType.ROUND) {
                    cornerPointsCount = (int) Mathf.Max(((2f / (Mathf.Sin(sharpness * Mathf.Deg2Rad))) * (45f / curV.Angle)), 1);
                }

                // Figure out how far "out" the edge is.
                float percent = ((j + 1) / (loops + 1f));

                // Correct the percent to be between 0 and 1.
                if (percent <= 0.5f)
                    percent *= 2f;
                else
                    percent = (1f - percent) * 2f;

                // Create the edge.

                // Right edge point.
                if (curV.RightEdge == EdgeType.FLAT) {
                    ePoints.Add(location + (Vector3) curV.RightEdgeNormal);
                }
                else if (curV.RightEdge == EdgeType.SHARP) {
                    ePoints.Add(location + (Vector3) curV.RightEdgeNormal * edgeDistance * percent);
                }
                else if (curV.RightEdge == EdgeType.ROUND) {
                    ePoints.Add(location + (Vector3) curV.RightEdgeNormal * Mathf.Sin(Mathf.PI / 2f * percent));
                }
                edgeConnections.Add(i);

                // Find the "far corner". This is where the two edge lines would meet if they continued on.
                Vector2 leftNormalPoint, rightNormalPoint, farPoint;
                leftNormalPoint = rightNormalPoint = farPoint = curV.Location;

                // If the edge is sharp, it will only have 1 "midpoint". We need to find out how this point will act,
                // depending on the edge types besides the corner.
                if (curV.Corner == CornerType.SHARP) {
                    Vector3 pos = location;
                    switch (curV.LeftEdge) {
                        case EdgeType.FLAT:
                            leftNormalPoint += curV.LeftEdgeNormal;
                            break;
                        case EdgeType.SHARP:
                            leftNormalPoint += curV.LeftEdgeNormal * edgeDistance * percent;
                            break;
                        case EdgeType.ROUND:
                            leftNormalPoint += curV.LeftEdgeNormal * Mathf.Sin(Mathf.PI / 2f * percent);
                            break;
                    }

                    switch (curV.RightEdge) {
                        case EdgeType.FLAT:
                            rightNormalPoint += curV.RightEdgeNormal;
                            break;
                        case EdgeType.SHARP:
                            rightNormalPoint += curV.RightEdgeNormal * edgeDistance * percent;
                            break;
                        case EdgeType.ROUND:
                            rightNormalPoint += curV.RightEdgeNormal * Mathf.Sin(Mathf.PI / 2f * percent);
                            break;
                    }

                    farPoint = this.PointOnTwoLines(leftNormalPoint, curV.LeftEdgeDirection * 10000f, rightNormalPoint, curV.RightEdgeDirection * 10000f);

                    ePoints.Add(farPoint);
                    edgeConnections.Add(i);
                }
                else if (curV.Corner == CornerType.ROUND) {

                    // If the edge is round, it may have multiple "midpoints". We need to find out how each point will act,
                    // depending on the edge types besides the corner and which edge it is around the corner.
                    for (int k = 0; k < cornerPointsCount; k++) {

                    }
                }

                // Left edge point.
                if (curV.LeftEdge == EdgeType.FLAT) {
                    ePoints.Add(location + (Vector3) curV.LeftEdgeNormal);
                }
                else if (curV.LeftEdge == EdgeType.SHARP) {
                    ePoints.Add(location + (Vector3) curV.LeftEdgeNormal * edgeDistance * percent);
                }
                else if (curV.LeftEdge == EdgeType.ROUND) {
                    ePoints.Add(location + (Vector3) curV.LeftEdgeNormal * Mathf.Sin(Mathf.PI / 2f * percent));
                }
                edgeConnections.Add(i);
            }

            edgePoints.Add(ePoints);
        }

        // Build the connections between the edges on the sides.
        // If there are 0 or only 1 edges, this will be skipped.
        int pointsBetween = frontPlane.Length;
        for (int i = 1; i < loops; i++) {
            List<int> connector = new List<int>();

            for (int j = 0; j < edgePoints[i].Count; j++) {
                int next = j + 1 >= edgePoints[i].Count ? 0 : j + 1;
                // Make the first of two triangles in the side quad
                connector.Add(pointsBetween + j + edgePoints[i].Count);
                connector.Add(pointsBetween + j);
                connector.Add(pointsBetween + next + edgePoints[i].Count);
                // Make the second of two triangles in the side quad
                connector.Add(pointsBetween + j);
                connector.Add(pointsBetween + next);
                connector.Add(pointsBetween + next + edgePoints[i].Count);
            }
            edgeIndices.Add(connector);

            pointsBetween += edgePoints[i - 1].Count;
        }
        
        // Build the connections between the planes and the bottom/top edges.
        if (loops == 0) {
            // If there are no edge loops, just connect the two planes together.
            List<int> connector = new List<int>();
            for (int i = 0; i < frontPlane.Length; i++) {
                int next = i + 1 >= frontPlane.Length ? 0 : i + 1;
                // Make the first of two triangles in the side quad
                connector.Add(i);
                connector.Add(i + frontPlane.Length);
                connector.Add(next + frontPlane.Length);
                // Make the second of two triangles in the side quad
                connector.Add(i);
                connector.Add(next + frontPlane.Length);
                connector.Add(next);
            }
            edgeIndices.Add(connector);
        }
        else {
            // If there are edge loops, connect the plane to their nearest loop.
            List<int> connectorFront = new List<int>();
            List<int> connectorBack = new List<int>();

            pointsBetween = frontPlane.Length;
            // Find out how many points are on the edges.
            for (int i = 0; i < edgePoints.Count - 1; i++) {
                pointsBetween += edgePoints[i].Count;
            }

            // Create the connections.
            for (int i = 0; i < edgePoints[0].Count; i++) {
                int currentVert = edgeConnections[i];
                int nextVert = edgeConnections[i + 1];
                int next = i + 1 >= edgePoints[0].Count ? 0 : i + 1;

                // If these two don't match, this is an edge and not a corner.
                if (nextVert != currentVert) {
                    // Front
                    // Make the first of two triangles in the side quad
                    connectorFront.Add(frontPlane.Length - currentVert - 1);
                    connectorFront.Add(frontPlane.Length - nextVert - 1);
                    connectorFront.Add(frontPlane.Length + i);
                    // Make the second of two triangles in the side quad
                    connectorFront.Add(frontPlane.Length + next);
                    connectorFront.Add(frontPlane.Length + i);
                    connectorFront.Add(frontPlane.Length - nextVert - 1);

                    // Back
                    // Make the first of two triangles in the side quad
                    connectorBack.Add(pointsBetween + i);
                    connectorBack.Add(pointsBetween + edgePoints[0].Count + frontPlane.Length - nextVert - 1);
                    connectorBack.Add(pointsBetween + edgePoints[0].Count + frontPlane.Length - currentVert - 1);
                    //// Make the second of two triangles in the side quad
                    connectorBack.Add(pointsBetween + i);
                    connectorBack.Add(pointsBetween + next);
                    connectorBack.Add(pointsBetween + edgePoints[0].Count + frontPlane.Length - nextVert - 1);
                }
                else {
                    // Make the corner connections.
                    // Front
                    connectorFront.Add(frontPlane.Length + i);
                    connectorFront.Add(frontPlane.Length - currentVert - 1);
                    connectorFront.Add(frontPlane.Length + next);

                    // Back
                    connectorBack.Add(pointsBetween + i);
                    connectorBack.Add(pointsBetween + next);
                    connectorBack.Add(pointsBetween + edgePoints[0].Count + frontPlane.Length - currentVert - 1);
                }
            }
            edgeIndices.Add(connectorFront);
            edgeIndices.Add(connectorBack);
        }

        // Create the back face now.
        pointsBetween += edgePoints[edgePoints.Count - 1].Count;
        int[] backIndex = frontIndex.Reverse().ToArray();
        for (int i = 0; i < backIndex.Length; i++) {
            backIndex[i] += pointsBetween;
        }

        // Determine the thickness of each plane/edge.
        // Front and back planes first.
        for (int i = 0; i < frontPlane.Length; i++) {
            frontPlane[i].z = thickness / 2f;
            backPlane[i].z = -thickness / 2f;
        }
        // Edge loops next.
        for (int i = 0; i < edgePoints.Count; i++) {
            float thick = (-thickness * ((i + 1) / (edgePoints.Count + 1f))) + (thickness / 2f);
            Vector3 curPos;
            for (int j = 0; j < edgePoints[i].Count; j++) {
                curPos = edgePoints[i][j];
                edgePoints[i][j] = new Vector3(curPos.x, curPos.y, thick);
            }
        }

        // Add the lists of vertexes together
        vertexList.Clear();
        vertexList.AddRange(frontPlane);
        for (int i = 0; i < edgePoints.Count; i++) {
            vertexList.AddRange(edgePoints[i]);
        }
        vertexList.AddRange(backPlane);


        // Add the lists of indices together
        indexList.Clear();
        indexList.AddRange(frontIndex);
        for (int i = 0; i < edgeIndices.Count; i++) {
            indexList.AddRange(edgeIndices[i]);
        }
        indexList.AddRange(backIndex);

        */
        #endregion

        // Output the final list of indices and vertices.
        _indices = this.indexList;
        _verts = this.vertexList;
        _attachments = this.attachmentList;

        // If we've made it this far, the expansion worked.
        return true;
    }

    /// <summary>
    /// This method splits an outline into multiple outlines by recursively going through
    /// the outline and seeing where things cross, then doing that for all the points in
    /// between the crossing.
    /// </summary>
    /// <param name="vertexCount"></param>
    /// <param name="sharedVertices"></param>
    /// <returns></returns>
    List<List<int>> SplitOutline (int _startingIndex, int _vertexCount, bool _skipInitialPoint, List<List<int>> _sharedVertices) {

        // See if this is a valid shape that can be made into a triangle at least.
        if (_vertexCount <= 2)
            return null;


        // Intialize the lists of outlines.
        List<List<int>> outlines = new List<List<int>>();
        // If we're here, we know that there's at least one outline.
        outlines.Add(new List<int>());

        // Go through each point finding out if it's in this outline or not.
        for (int i = _startingIndex; i < _vertexCount + _startingIndex; i++) {

            // Skip the initial point if needed.
            if (!_skipInitialPoint || i != _startingIndex) {
                // Check if this point is a "shared" point. If it is, we need to call this
                // function on this point and the point(s) it shares.
                for (int j = 0; j < _sharedVertices.Count; j++) {
                    // If this point is found, go through the process.
                    if (_sharedVertices[j].Contains(i)) {
                        // Find out if this is the start/middle/end of a line. We do this by
                        // checking in both directions to see if we've reached the start/end.

                        // The last point shared in this spot.
                        int lastPoint = i;

                        // Go through each shared point.
                        for (int k = 1; k < _sharedVertices[j].Count; k++) {
                            // A list of lists of ints to keep track of all the points.
                            List<List<int>> lastSplitOutlines;

                            int nextCount = _sharedVertices[j][k] - lastPoint;

                            // Find out if the point this is sharing with is actually
                            // lower than this one.
                            if (_sharedVertices[j][k] < lastPoint)
                                nextCount = (_vertexCount + lastPoint) - _sharedVertices[j][k];

                            // Recursively call this function.
                            lastSplitOutlines = this.SplitOutline(lastPoint, nextCount, true, _sharedVertices);

                            // Add the outlines to the list if they're valid.
                            if (lastSplitOutlines != null) {
                                for (int l = 0; l < lastSplitOutlines.Count; l++) {
                                    outlines.Add(lastSplitOutlines[l]);
                                }
                            }

                            // Set the lastPoint to this one.
                            lastPoint = _sharedVertices[j][k];
                        }

                        // Set i to the lastPoint.
                        i = lastPoint;

                        // Break out of the loop checking if the point is sharing a spot
                        // (we just dealt with all of that).
                        break;
                    }
                }
            }

            // Add the checked point (will be increased to the end if it shares a spot.
            outlines[0].Add(i);
        }

        // See if the first "outline" was only two points. If it was, this was a line.
        // We should then remove the line from being an outline.
        if (outlines[0].Count <= 2) {
            outlines.RemoveAt(0);
        }

        // Return the found outlines.
        return outlines;
    }

    // ----------------------------------------------------------------------------------- >> Helper functions for expanding <<

    /// <summary>
    /// A function to get the cross product of two Vector2s.
    /// </summary>
    /// <param name="_v">The first Vector2.</param>
    /// <param name="_w">The second Vector2.</param>
    /// <returns>The cross product of the two Vector2s.</returns>
    float CrossVector2 (Vector2 _v, Vector2 _w) {
        return ((_v.x * _w.y) - (_v.y * _w.x));
    }

    /// <summary>
    /// A function to find if the line segments formed by _p + _r and _q + _s cross at a point.
    /// </summary>
    /// <param name="_p">The starting point of the first line.</param>
    /// <param name="_r">The segment coming out of _p to form the first line.</param>
    /// <param name="_q">The starting point of the second line.</param>
    /// <param name="_s">The segment coming out of _q to form the second line.</param>
    /// <returns>The point where the two lines cross. If the return comes back with (-1, -1), the two lines do not cross.</returns>
    Vector2 PointOnTwoLines (Vector2 _p, Vector2 _r, Vector2 _q, Vector2 _s) {

        // initialize variables
        Vector2 point = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        float t, u;

        // calcualte parts
        float cross = this.CrossVector2(_r, _s);
        Vector2 mid = _q - _p;

        // find t and u where the two lines cross
        t = this.CrossVector2(mid, _s);
        u = this.CrossVector2(mid, _r);

        // find the crossing point or whether they don't cross
        if (cross == 0) {
            if (t == 0) {
                // lines are collinear
                return point;
            }
            else {
                // lines are parallel
                return point;
            }
        }
        else {
            t /= cross;
            u /= cross;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1) {
                // lines cross
                point = _p + (_r * t);
                return point;
            }
            else {
                // lines would cross in the future if they continued on
                return point;
            }
        }
    }


    /// <summary>
    /// A function to find if the line segments formed by _p + _r and _q + _s cross at a point.
    /// This version returns the percentages along the lines that the crossing was found.
    /// </summary>
    /// <param name="_p">The starting point of the first line.</param>
    /// <param name="_r">The segment coming out of _p to form the first line.</param>
    /// <param name="_q">The starting point of the second line.</param>
    /// <param name="_s">The segment coming out of _q to form the second line.</param>
    /// <returns>The point where the two lines cross. If the return comes back with (-1, -1), the two lines do not cross.</returns>
    Vector2 PointOnTwoLines (Vector2 _p, Vector2 _r, Vector2 _q, Vector2 _s, out float t, out float u) {

        // initialize variables
        Vector2 point = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        // calcualte parts
        float cross = this.CrossVector2(_r, _s);
        Vector2 mid = _q - _p;

        // find t and u where the two lines cross
        t = this.CrossVector2(mid, _s);
        u = this.CrossVector2(mid, _r);

        // find the crossing point or whether they don't cross
        if (cross == 0) {
            if (t == 0) {
                // lines are collinear
                return point;
            }
            else {
                // lines are parallel
                return point;
            }
        }
        else {
            t /= cross;
            u /= cross;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1) {
                // lines cross
                point = _p + (_r * t);
                return point;
            }
            else {
                // lines would cross in the future if they continued on
                return point;
            }
        }
    }


    /// <summary>
    /// Check to see if the two lines are crossing. The first line is between p1 and p2, second line is between p3 and p4.
    /// </summary>
    /// <param name="_p1">Start of the fist line.</param>
    /// <param name="_p2">End of the first line.</param>
    /// <param name="_p3">Start of the second line.</param>
    /// <param name="_p4">End of the second line.</param>
    /// <returns>Returns true if the lines cross. Returns false if they don't cross, the lines are identical, or if a line is just a point.</returns>
    bool lineCrossingCheck (Vector2 _p1, Vector2 _p2, Vector2 _p3, Vector2 _p4) {

        bool crossed = false;

        // check to see if either line is just a point
        if (_p1 == _p2 || _p3 == _p4)
            return false;

        // check to see if the lines are identical
        if (_p1 == _p3 && _p2 == _p4)
            return true;

        // check for crossing
        bool sign;
        // check for if line 2 crosses line 1
        sign = (((_p3.x - _p1.x) * (_p2.y - _p1.y) - (_p3.y - _p1.y) * (_p2.x - _p1.x)) > 0f);
        if (((_p4.x - _p1.x) * (_p2.y - _p1.y) - (_p4.y - _p1.y) * (_p2.x - _p1.x)) > 0f) {

            // check if line 1 crosses line 2
            sign = (((_p1.x - _p3.x) * (_p4.y - _p3.y) - (_p1.y - _p3.y) * (_p4.x - _p3.x)) > 0f);
            if (((_p1.x - _p3.x) * (_p4.y - _p3.y) - (_p1.y - _p3.y) * (_p4.x - _p3.x)) > 0f)
                crossed = true;
        }

        return crossed;
    }

}