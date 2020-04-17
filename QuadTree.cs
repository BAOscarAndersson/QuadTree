using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuadTree
{
    /// <summary>
    /// A bucket PR quadtree.
    /// </summary>
    public class QuadTree<T> where T : IQTstoreable
    {
        // The root element of the tree, spans the entire area.
        private readonly QuadTreeNode Root;

        // Max number of objects a node can store before it splits itself.
        private const int maxObjects = 8;

        /// <summary>
        /// Represents a node in the tree.
        /// </summary>
        private class QuadTreeNode
        {
            // Only the leaves will store objects.
            private bool isLeaf = true;
            private byte count;

            // The values of the area of the node.
            private readonly float nodeX, nodeY;
            private readonly float nodeWidth, nodeHeight;

            // Childrens, one for each quadrant of the area.
            private QuadTreeNode NE { get; set; }    // North-East, or Upper Left.
            private QuadTreeNode NW { get; set; }    // North-West, or Upper Right.
            private QuadTreeNode SE { get; set; }    // South-East, or Lower Left.
            private QuadTreeNode SW { get; set; }    // South-West, or Lower Right.

            // If the node is a leaf it can contain maxObjects number of objects.
            private T[] nodeBucket;

            /// <summary>
            /// Creats a new node for the tree that is a certain region.
            /// </summary>
            /// <param name="xPos">The vertical position of the rectangle.</param>
            /// <param name="yPos">The horizontal position of the rectangle.</param>
            /// <param name="widthOfArea">The vertical size of the rectangle.</param>
            /// <param name="heightOfArea">The horizontal size of the rectangle.</param>
            /// <param name="parent">The larger region the node lies in.</param>
            internal QuadTreeNode(float xPos, float yPos, float widthOfArea, float heightOfArea)
            {
                this.nodeX = xPos;
                this.nodeY = yPos;
                this.nodeWidth = widthOfArea;
                this.nodeHeight = heightOfArea;
                this.count = 0;
                this.nodeBucket = new T[maxObjects + 1];
            }

            /// <summary>
            /// If the node is a leaf the method starts to fill the bucket,
            /// unless the bucket is full in which case the node will be split.
            /// As the node is split the object to be inserted is sent
            /// down the tree and inserted downwards somewhere.
            /// </summary>
            /// <param name="objectToStore">A object that is to be stored in the tree.</param>
            internal void Insert(T objectToStore)
            {
                Debug.Assert(objectToStore.Coords.X >= nodeX && objectToStore.Coords.Y >= nodeY, "object out of bounds in quadtreenode");
                Debug.Assert(objectToStore.Coords.X <= nodeX + nodeWidth && objectToStore.Coords.Y <= nodeY + nodeHeight, "object out of bounds in quadtreenode");

                // Only the leaves store objects.
                if (isLeaf)
                {
                    // As long as the leaf isn't at capacity, add objects to it's bucket and increase the count.
                    if (count < maxObjects)
                    {
                        nodeBucket[count] = objectToStore;
                        count++;
                    }
                    // If the leaf is full it splits up and assigns the objects it stores to its new children.
                    else
                    {
                        nodeBucket[count] = objectToStore;
                        count++;
                        Split();
                    }
                }
                else
                {
                    // If the node isn't a leaf the it sends the object down the tree.
                    GetQuadrant(objectToStore).Insert(objectToStore);
                }
            }

            /// <summary>
            /// Creates four children to the node and gives the object in the node to them for safekeeping.
            /// </summary>
            private void Split()
            {
                isLeaf = false;

                // As PR Quadtree consists of quadrants of equal size the lengths of the child is half that of the parent.
                float widthHalf = nodeWidth / 2;
                float heightHalf = nodeHeight / 2;

                // The nodes will end up at different places depending on which quadrant they represent.
                this.NE = new QuadTreeNode(nodeX, nodeY, widthHalf, heightHalf);
                this.NW = new QuadTreeNode(nodeX + widthHalf, nodeY, widthHalf, heightHalf);
                this.SE = new QuadTreeNode(nodeX, nodeY + heightHalf, widthHalf, heightHalf);
                this.SW = new QuadTreeNode(nodeX + widthHalf, nodeY + heightHalf, widthHalf, heightHalf);

                for (int i = 0; i < count; i++)
                {
                    GetQuadrant(nodeBucket[i]).Insert(nodeBucket[i]); 
                }
            }

            /// <summary>
            /// If the current node is a leave, return the stored objects. Otherwise push down the search to the next level.
            /// </summary>
            /// <param name="searchObject">The object that is in the area of interest.</param>
            /// <returns>The objects in the area of the inputed object.</returns>
            internal List<T> GetObjectsInCell(T searchObject)
            {
                Debug.Assert(searchObject.Coords.X >= nodeX && searchObject.Coords.Y >= nodeY, "object out of bounds in quadtreenode");
                Debug.Assert(searchObject.Coords.X <= nodeX + nodeWidth && searchObject.Coords.Y <= nodeY + nodeHeight, "object out of bounds in quadtreenode");

                if (isLeaf)
                {
                    return nodeBucket.ToList();
                }
                else
                {
                    return PushDownSearch(searchObject);
                }
            }

            /// <summary>
            /// Gets all the objects that lies within the inputed rectangle.
            /// </summary>
            /// <param name="searchArea">Objects withing this rectangle is returned.</param>
            /// <param name="theWholeThing">If the whole node is in the area set this to true.</param>
            /// <returns>A list of the objects sort of close to the inputed rectangle.</returns>
            internal List<T> GetNeighbourhood(SimpleRect searchArea, bool theWholeThing)
            {
                List<T> neighbourhood = new List<T>();

                /* If the entire node is inside the search area no further searching
                 * need be done, and all the objects stored in or under are returned.
                 */
                if (theWholeThing)
                {
                    neighbourhood.AddRange(GetAllObjectsUnder());
                }
                /* If the node is a leaf, all the objects within are checked to see if they
                 * are inside of the search area.
                 */
                else if (isLeaf)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (IsInRectangle(nodeBucket[i], searchArea))
                            neighbourhood.Add(nodeBucket[i]);
                    }
                }
                /* If the node isn't a leaf or entirely withing the search area, the search needs
                 * to be pushed down to all the quadrants that are in the search area.
                 * The returned results from that must then be combined before they are returned.
                 */
                else
                {
                    List<Tuple<QuadTreeNode, bool>> includedQuadrants = GetIncludedQuads(searchArea);

                    foreach (Tuple<QuadTreeNode, bool> quadrant in includedQuadrants)
                        neighbourhood.AddRange(quadrant.Item1.GetNeighbourhood(searchArea, quadrant.Item2));
                }

                return neighbourhood;
            }

            /// <summary>
            /// Calculates which quadrants are in the search area.
            /// </summary>
            /// <param name="searchArea">Objects withing this rectangle is returned.</param>
            /// <returns>A list of quadrants that are in the area 
            /// and a bool that is true if they are entirely within it.</returns>
            private List<Tuple<QuadTreeNode, bool>> GetIncludedQuads(SimpleRect searchArea)
            {
                List<Tuple<QuadTreeNode, bool>> includedQuadrants = new List<Tuple<QuadTreeNode, bool>>();

                List<QuadTreeNode> allQuads = new List<QuadTreeNode> { NE, NW, SE, SW };

                foreach (QuadTreeNode aQuad in allQuads)
                {
                    if (aQuad == null)
                        continue;

                    /* If the searchArea is entirely withing a quad, it can not be in other quads, 
                     * so break out of the loop.*/
                    if (IsInRectangle(searchArea.leftX, searchArea.upperY, aQuad) &&
                        IsInRectangle(searchArea.rightX, searchArea.lowerY, aQuad))
                    {
                        includedQuadrants.Add(new Tuple<QuadTreeNode, bool>(aQuad, false));
                        break;
                    }
                    /* If a quad is entirely within the search area, no further searching needs to be done
                     * below it, so the bool to get the all the objects from the subtree is set to true.
                     */
                    if (IsInRectangle(aQuad.nodeX, aQuad.nodeY, searchArea) &&
                        IsInRectangle(aQuad.nodeX + aQuad.nodeWidth, aQuad.nodeY + aQuad.nodeHeight, searchArea))
                    {
                        includedQuadrants.Add(new Tuple<QuadTreeNode, bool>(aQuad, true));
                        continue;
                    }
                    /* If any part of the search area is withing the quad it's included in the list */

                    if (IsPartiallyInRectangle(searchArea, aQuad))
                    {
                        includedQuadrants.Add(new Tuple<QuadTreeNode, bool>(aQuad, false));
                    }
                }

                return includedQuadrants;
            }

            /// <summary>
            /// Checks if input object is within a rectangular area.
            /// </summary>
            /// <param name="aObject">Object one is looking for to see if it is inside of the area.</param>
            /// <param name="searchArea">The are one is looking at to see if the object is inside of.</param>
            /// <returns>True if the object is in the area, false if it's outside.</returns>
            private bool IsInRectangle(T aObject, SimpleRect searchArea)
            {
                return IsInRectangle(aObject.Coords.X, aObject.Coords.Y, searchArea.leftX, searchArea.rightX, searchArea.upperY, searchArea.lowerY);
            }
            #region IsInRectangle overloads
            private bool IsInRectangle(float x, float y, QuadTreeNode aNode)
            {
                return IsInRectangle(x, y, aNode.nodeX, aNode.nodeX + aNode.nodeWidth, aNode.nodeY, aNode.nodeY + aNode.nodeHeight);
            }

            private bool IsInRectangle(float x, float y, SimpleRect searchArea)
            {
                return IsInRectangle(x, y, searchArea.leftX, searchArea.rightX, searchArea.upperY, searchArea.lowerY);
            }

            private bool IsInRectangle(float x, float y, float leftX, float rightX, float upperY, float lowerY)
            {
                if (x >= leftX &&
                    x <= rightX &&
                    y >= upperY &&
                    y <= lowerY)
                    return true;
                else
                    return false;
            }
            #endregion

            /// <summary>
            /// Checks wether some part of the rectangle is inside of the quadrant.
            /// </summary>
            /// <param name="searchArea">The rectangle that will be checked.</param>
            /// <param name="aQuad">The quad to check against.</param>
            /// <returns>True if some part of the rectangle is in the quad, false otherwise.</returns>
            private bool IsPartiallyInRectangle(SimpleRect searchArea, QuadTreeNode aQuad)
            {
                if (IsInXInterval(searchArea.leftX, aQuad) ||
                    IsInXInterval(searchArea.rightX, aQuad) ||
                    IsInYInterval(searchArea.upperY, aQuad) ||
                    IsInYInterval(searchArea.lowerY, aQuad))
                    return true;
                return false;
            }

            /// <summary>
            /// Checks if the input float is inside the  horizontal span of the quad.
            /// </summary>
            /// <param name="x">The coordinate to check if it's inside the quads horizontal span.</param>
            /// <param name="aQuad">The quad to check against.</param>
            /// <returns>True if input is inside the quad.</returns>
            private bool IsInXInterval(float x, QuadTreeNode aQuad)
            {
                if (x <= aQuad.nodeX + aQuad.nodeWidth && x >= aQuad.nodeX)
                    return true;
                return false;
            }

            /// <summary>
            /// Checks if the input float is inside the  vertical span of the quad.
            /// </summary>
            /// <param name="y">The coordinate to check if it's inside the quads vertical span.</param>
            /// <param name="aQuad">The quad to check against.</param>
            /// <returns>True if input is inside the quad.</returns>
            private bool IsInYInterval(float y, QuadTreeNode aQuad)
            {
                if (y <= aQuad.nodeY + aQuad.nodeHeight && y >= aQuad.nodeY)
                    return true;
                return false;
            }

            /// <summary>
            /// Figures out to which child the inputed object belongs and gives the search to it.
            /// </summary>
            /// <param name="searchObject">The position of this will be used to determine where to search.</param>
            /// <returns>The objects that are in the same node as the input object.</returns>
            private List<T> PushDownSearch(T searchObject)
            {
                return GetQuadrant(searchObject).GetObjectsInCell(searchObject);
            }

            /// <summary>
            /// Travels down the node based on where the inputed coordinates lies.
            /// </summary>
            /// <param name="x">The horizontal position where the object lies.</param>
            /// <param name="y">The horizontal position where the object lies.</param>
            /// <returns>The node to which the input belong.</returns>
            private QuadTreeNode GetQuadrant(float x, float y)
            {
                if (x <= nodeX + nodeWidth / 2)
                {
                    if (y <= nodeY + nodeHeight / 2)
                        return NE;
                    else
                        return SE;
                }
                else
                {
                    if (y <= nodeY + nodeHeight / 2)
                        return NW;
                    else
                        return SW;
                }
            }
            private QuadTreeNode GetQuadrant(T aObject)
            {
                return GetQuadrant(aObject.Coords.X, aObject.Coords.Y);
            }

            /// <summary>
            /// Returns all objects stored in this node/nodes under this node.
            /// </summary>
            /// <returns>A list of objects that are stored under this node.</returns>
            private List<T> GetAllObjectsUnder()
            {
                if (isLeaf)
                {
                    return nodeBucket.ToList();
                }
                else
                {
                    List<T> allObjects = new List<T>();

                    allObjects.AddRange(NE.GetAllObjectsUnder());
                    allObjects.AddRange(NW.GetAllObjectsUnder());
                    allObjects.AddRange(SE.GetAllObjectsUnder());
                    allObjects.AddRange(SW.GetAllObjectsUnder());

                    return allObjects;
                }
            }

        }

        /// <summary>
        /// When the QuadTree is made a root node is created with the area of the input.
        /// </summary>
        /// <param name="widthOfArea">The quad tree will cover an area of this width.</param>
        /// <param name="heightOfArea">The quad tree will cover an area of this height.</param>
        public QuadTree(float widthOfArea, float heightOfArea)
        {
            // Since the root spans the entire area it starts at origo(Left upper corner of screen).
            this.Root = new QuadTreeNode(0, 0, widthOfArea, heightOfArea);
        }

        /// <summary>
        /// Stores an object in the tree. The object needs to implement the interface IQTstoreable.
        /// </summary>
        /// <param name="insertObject">A object that is to be stored in the tree. </param>
        public void Insert(T insertObject)
        {
            Root.Insert(insertObject);
        }

        /// <summary>
        /// Gets all the objects of the node where the inputed object lies.
        /// A very bad distance measure.
        /// </summary>
        /// <param name="searchObject">The object whose position constitutes the search.</param>
        /// <returns>A list of objects that lies in the same node as input.</returns>
        public List<T> GetObjectsInCell(T searchObject)
        {
            return Root.GetObjectsInCell(searchObject);
        }

        /// <summary>
        /// Gets all the objects that lies within nodes that are partially covered by the inputed 
        /// rectangular area.
        /// A better distance measure than GetObjectsInCell.
        /// </summary>
        /// <param name="searchArea">A reactangular area to search for objects.</param>
        /// <returns>A list of objects that lies "close to" the inputted area.</returns>
        public List<T> GetNeighbourhood(SimpleRect searchArea)
        {
            return Root.GetNeighbourhood(searchArea, false);
        }
    }

    /// <summary>
    /// A rectangle defined by the values of it's borders.
    /// </summary>
    public struct SimpleRect
    {
        readonly public float upperY;
        readonly public float lowerY;
        readonly public float leftX;
        readonly public float rightX;

        public SimpleRect(float setUpperY, float setLowerY, float setLeftX, float setRightX)
        {
            upperY = setUpperY;
            lowerY = setLowerY;
            leftX = setLeftX;
            rightX = setRightX;
        }
    }

    /// <summary>
    /// Represents a point in the two dimensions that the QuadTree covers.
    /// </summary>
    public struct Coordinates
    {
        public float X, Y;
    }

    /// <summary>
    /// All objects that are to be stored in the QuadTree must implement this interface.
    /// </summary>
    public interface IQTstoreable
    {
        Coordinates Coords { get; }
    }
}
