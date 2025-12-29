using System;
using System.Collections.Generic;
using UnityEngine;

namespace PromptsmithProtocol
{
    public static class MapGenerator
    {
        public static MapState Generate(DeterministicRng rng, int rows, int lanes)
        {
            if (rows < 4) rows = 4;
            if (lanes < 2) lanes = 2;

            var map = new MapState();

            // Build grid first (do NOT populate map.nodes yet)
            var idx = 0;
            var grid = new MapNode[rows, lanes];

            for (var r = 0; r < rows; r++)
            {
                for (var l = 0; l < lanes; l++)
                {
                    grid[r, l] = new MapNode
                    {
                        index = idx++,
                        row = r,
                        lane = l,
                        type = NodeType.Fight,
                        next = new List<int>(2)
                    };
                }
            }

            // Start row 0
            for (var l = 0; l < lanes; l++)
                grid[0, l].type = NodeType.Start;

            // Boss last row
            for (var l = 0; l < lanes; l++)
                grid[rows - 1, l].type = NodeType.Boss;

            var shopRow = rng.NextInt(4, rows - 3);
            var restRow = rng.NextInt(2, rows - 4);

            for (var r = 1; r < rows - 1; r++)
            {
                for (var l = 0; l < lanes; l++)
                {
                    if (r == shopRow) { grid[r, l].type = NodeType.Shop; continue; }
                    if (r == restRow) { grid[r, l].type = NodeType.Rest; continue; }

                    var roll = rng.NextDouble();
                    grid[r, l].type =
                        roll < 0.12 ? NodeType.Event :
                        roll < 0.22 ? NodeType.Shop :
                        roll < 0.32 ? NodeType.Rest :
                        roll < 0.40 ? NodeType.Elite :
                        NodeType.Fight;
                }
            }

            // Link forward edges
            for (var r = 0; r < rows - 1; r++)
            {
                for (var l = 0; l < lanes; l++)
                {
                    // Copy out -> mutate -> write back (works for struct or class)
                    var cur = grid[r, l];
                    cur.next ??= new List<int>(2);
                    cur.next.Clear();

                    var candidates = new List<MapNode>(3);
                    for (var dl = -1; dl <= 1; dl++)
                    {
                        var nl = l + dl;
                        if (nl < 0 || nl >= lanes) continue;
                        candidates.Add(grid[r + 1, nl]);
                    }

                    rng.Shuffle(candidates);

                    var count = rng.NextDouble() < 0.35 ? 2 : 1;
                    for (var i = 0; i < Math.Min(count, candidates.Count); i++)
                        cur.next.Add(candidates[i].index);

                    if (cur.next.Count == 0 && candidates.Count > 0)
                        cur.next.Add(candidates[0].index);

                    grid[r, l] = cur;
                }
            }

            // NOW populate map.nodes from the fully-linked grid.
            // This fixes the "struct copy" problem.
            map.nodes = new List<MapNode>(rows * lanes);
            for (var r = 0; r < rows; r++)
                for (var l = 0; l < lanes; l++)
                    map.nodes.Add(grid[r, l]);

            // Canonical start = middle lane start node
            map.currentIndex = grid[0, lanes / 2].index;

            return map;
        }
    }
}
