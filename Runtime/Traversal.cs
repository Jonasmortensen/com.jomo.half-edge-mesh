using System;
using System.Collections.Generic;

namespace Jomo.HalfEdgeMesh
{
    // Shared, bounded walks over the half-edge structure. A corrupt mesh throws instead of looping forever.
    internal static class Traversal
    {
        public const int MaxSteps = 10000;

        // Follows Next from start until it returns to start (the edges of one face or boundary loop)
        public static IEnumerable<HalfEdge> Loop(HalfEdge start)
        {
            if (start == null) yield break;

            HalfEdge current = start;
            int steps = 0;
            do
            {
                yield return current;
                current = current.Next;
                if (current == null) throw Corrupt($"Half-edge loop from edge {start.ID} hit a missing Next link");
                if (++steps > MaxSteps) throw Corrupt($"Half-edge loop from edge {start.ID} did not close");
            } while (current != start);
        }

        // Rotates around start.Origin via Previous.Twin, yielding every outgoing half-edge once
        public static IEnumerable<HalfEdge> Fan(HalfEdge start)
        {
            if (start == null) yield break;

            HalfEdge current = start;
            int steps = 0;
            do
            {
                yield return current;
                if (current.Previous == null) throw Corrupt($"Vertex fan from edge {start.ID} hit a missing Previous link");
                current = current.Previous.Twin;
                if (current == null) throw Corrupt($"Vertex fan from edge {start.ID} hit a missing Twin link");
                if (++steps > MaxSteps) throw Corrupt($"Vertex fan from edge {start.ID} did not close");
            } while (current != start);
        }

        private static InvalidOperationException Corrupt(string message)
        {
            return new InvalidOperationException(message + ". The mesh is corrupt; call HalfEdgeMesh.Validate() for details.");
        }
    }
}
