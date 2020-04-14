using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace QuadTree
{
    /// <summary>
    /// Stores objects in a bucket PR quadtree. The objects must implement the interface IQTstoreable.
    /// </summary>
    public class QuadTree<T> where T : IQTstoreable
    {
        // The root element of the tree, spans the entire area.
        private readonly QuadtreeNode Root;

        // Max number of objects a node can store before it splits itself.
        private const int maxObjects = 8;

        /// <summary>
        /// Represents a node in the tree.
        /// </summary>
        private class QuadtreeNode
        {
            // Only the leaves will store objects.
            private bool isLeaf = true;
            private byte count;

            // The values of the area of the node.
            private readonly float nodeX, nodeY;
            private readonly float nodeWidth, nodeHeight;

            // Childrens, one for each quadrant of the area.
            private QuadtreeNode NE { get; set; }    // North-East, or Upper Left.
            private QuadtreeNode NW { get; set; }    // North-West, or Upper Right.
            private QuadtreeNode SE { get; set; }    // South-East, or Lower Left.
            private QuadtreeNode SW { get; set; }    // South-West, or Lower Right.

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
            internal QuadtreeNode(float xPos, float yPos, float widthOfArea, float heightOfArea)
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
                // The lower and left bounds are closed, the right and upper open.
                Debug.Assert(objectToStore.Coords.X >= nodeX && objectToStore.Coords.Y > nodeY, "object out of bounds in quadtreenode");
                Debug.Assert(objectToStore.Coords.X < nodeX + nodeWidth && objectToStore.Coords.Y <= nodeY + nodeHeight, "object out of bounds in quadtreenode");

                // Only the leaves store objects.
                if (isLeaf)
                {
                    // If the leaf is full it splits up and assigns the objects it stores to its new children.
                    if (count == maxObjects)
                    {
                        nodeBucket[count] = objectToStore;
                        count++;
                        Split();
                    }
                    else
                    {
                        nodeBucket[count] = objectToStore;
                        count++;
                    }
                }
                else
                {
                    // If the node isn't a leaf the it sends the object down the tree.
                    PushDownInsert(objectToStore);
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
                this.NE = new QuadtreeNode(nodeX, nodeY, widthHalf, heightHalf);
                this.NW = new QuadtreeNode(nodeX + widthHalf, nodeY, widthHalf, heightHalf);
                this.SE = new QuadtreeNode(nodeX, nodeY + heightHalf, widthHalf, heightHalf);
                this.SW = new QuadtreeNode(nodeX + widthHalf, nodeY + heightHalf, widthHalf, heightHalf);

                for (int i = 0; i < count; i++)
                {
                    PushDownInsert(nodeBucket[i]);
                }
            }

            /// <summary>
            /// Move something to be stored to the correct child.
            /// </summary>
            /// <param name="objectToStore">The position of this determines where it will be stored.</param>
            private void PushDownInsert(T objectToStore)
            {
                // Depending on which quadrant the object belongs in, it is inserted into it.
                GetQuadrant(objectToStore.Coords.X, objectToStore.Coords.Y).Insert(objectToStore);
            }

            /// <summary>
            /// If the current node is a leave, return the stored objects. Otherwise push down the search to the next level.
            /// </summary>
            /// <param name="searchObject">The object that is in the area of interest.</param>
            /// <returns>The objects in the area of the inputed object.</returns>
            internal List<T> GetObjectsInCell(T searchObject)
            {
                // The lower and left bounds are closed, the right and upper open.
                Debug.Assert(searchObject.Coords.X >= nodeX && searchObject.Coords.Y > nodeY, "object out of bounds in quadtreenode");
                Debug.Assert(searchObject.Coords.X < nodeX + nodeWidth && searchObject.Coords.Y <= nodeY + nodeHeight, "object out of bounds in quadtreenode");

                if (isLeaf)
                {
                    return nodeBucket.ToList();
                }
                else
                {
                    return PushDownSearch(searchObject);
                }
            }

            /// <summary>Gets all the objects that lies within the inputed rectangle,
            /// maybe plus some that are close to it.
            /// </summary>
            /// <param name="searchArea"></param>
            /// <returns>A list of the objects sort of close to the inputed rectangle.</returns>
            internal List<T> GetNeighbourhood(ref SimpleRect searchArea)
            {
                List<T> neighbourhood = new List<T>();

                if (isLeaf)
                {
                    neighbourhood = nodeBucket.ToList();
                }
                else
                {
                    /* If the node isn't a leaf the search needs to be push down to all the quadrants
                     * that are covered by the search area. The returned results must then be combined
                     * before they are returned.
                     */
                    List<QuadtreeNode> includedQuadrants = new List<QuadtreeNode>();

                    includedQuadrants = GetIncludedQuads(ref searchArea);

                    foreach (QuadtreeNode quadrant in includedQuadrants)
                        neighbourhood.AddRange(quadrant.GetNeighbourhood(ref searchArea));
                }

                return neighbourhood;
            }

            /// <summary>
            /// Calculates which quadrants are in the search area.
            /// </summary>
            /// <param name="searchArea">A rectangular area.</param>
            /// <returns>A list of quadrants that are in the area.</returns>
            private List<QuadtreeNode> GetIncludedQuads(ref SimpleRect searchArea)
            {
                /* The computation of all this is rather involved but the gist of it is this:
                 * You check where one corner(upper left) of the rectangle lies and based on that
                 * you can make some assumptions where the rectangle are. I.e. if the upper left
                 * corner is in the lower right quadrant the whole search rectangle must lie in that
                 * quadrant.
                 * Except in that example case you then have to check where a second corner 
                 * (dependent on the first check) of the search area lies and based on where that
                 * one lies, the result follows.
                 */

                List<QuadtreeNode> includedQuadrants = new List<QuadtreeNode>();
                includedQuadrants.Add(GetQuadrant(searchArea.leftX, searchArea.upperY));

                if (includedQuadrants[0] == SW)
                {
                    return includedQuadrants;
                }
                else if (includedQuadrants[0] == NE)
                {
                    includedQuadrants.Add(GetQuadrant(searchArea.rightX, searchArea.lowerY));

                    if (includedQuadrants[1] == SW)
                    {
                        includedQuadrants.Add(NW);
                        includedQuadrants.Add(SE);
                    }
                }
                else if (includedQuadrants[0] == NW)
                {
                    includedQuadrants.Add(GetQuadrant(searchArea.leftX, searchArea.lowerY));
                }
                else if (includedQuadrants[0] == SE)
                {
                    includedQuadrants.Add(GetQuadrant(searchArea.rightX, searchArea.upperY));
                }

                // In some cases the above logic returns doublettes.
                includedQuadrants = includedQuadrants.Distinct().ToList();

                return includedQuadrants;
            }

            /// <summary>
            /// Figures out to which child the inputed object belongs and gives the search to it.
            /// </summary>
            /// <param name="searchObject">The position of this will be used to determine where to search.</param>
            /// <returns>The objects that are in the same node as the input object.</returns>
            private List<T> PushDownSearch(T searchObject)
            {
                return GetQuadrant(searchObject.Coords.X, searchObject.Coords.Y).GetObjectsInCell(searchObject);
            }

            /// <summary>
            /// Travels down the node based on where the inputed coordinates lies.
            /// </summary>
            /// <param name="x">The horizontal position where the object lies.</param>
            /// <param name="y">The horizontal position where the object lies.</param>
            /// <returns>The node to which the input belong.</returns>
            private QuadtreeNode GetQuadrant(float x, float y)
            {
                QuadtreeNode searchedForNode;

                if (x <= nodeX + nodeWidth / 2)
                {
                    if (y <= nodeY + nodeHeight / 2)
                        searchedForNode = NE;
                    else
                        searchedForNode = SE;
                }
                else
                {
                    if (y <= nodeY + nodeHeight / 2)
                        searchedForNode = NW;
                    else
                        searchedForNode = SW;
                }

                return searchedForNode;
            }
        }

        // When the QuadTree is made a root node is created.
        public QuadTree(float widthOfArea, float heightOfArea)
        {
            // Since the root spans the entire area it starts at origo(Left upper corner of screen).
            this.Root = new QuadtreeNode(0, 0, widthOfArea, heightOfArea);
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
        /// A bad distance measure.
        /// </summary>
        /// <param name="searchArea">A reactangular area to search for objects.</param>
        /// <returns>A list of objects that lies "close to" the inputted area.</returns>
        public List<T> GetNeighbourhood(ref SimpleRect searchArea)
        {
            return Root.GetNeighbourhood(ref searchArea);
        }
    }

    /// <summary>
    /// A rectangle defined by the values of it's borders.
    /// </summary>
    public struct SimpleRect
    {
        readonly internal float upperY;
        readonly internal float lowerY;
        readonly internal float leftX;
        readonly internal float rightX;

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
