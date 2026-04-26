using System.Collections.Generic;
using System;

namespace StackSurge.Core
{
    /// <summary>
    /// Row/column contiguous matches and L-shapes (corner + two perpendicular arms).
    /// Wild participates only when at least one non-wild defines the color; all-wild groups do not clear.
    /// Optimized line-sweeping algorithm evaluates maximal valid lines per cell, dropping complexity significantly.
    /// </summary>
    public static class MatchFinder
    {
        static readonly TileKind[] _colors = {
            TileKind.Red, TileKind.Blue, TileKind.Green, TileKind.Yellow, TileKind.Purple
        };

        public static void CollectMatches(TileKind[,] board, int width, int height, HashSet<(int r, int c)> into,
            out int largestMatchSize)
        {
            into.Clear();
            int maxSz = 0;

            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    TileKind t = board[r, c];
                    if (t == TileKind.Empty || t == TileKind.Bomb) continue;

                    int colorCount = (t == TileKind.Wild) ? _colors.Length : 1;

                    for (int i = 0; i < colorCount; i++)
                    {
                        TileKind testColor = (t == TileKind.Wild) ? _colors[i] : t;

                        GetExtent(board, width, height, r, c, -1, 0, testColor, out int lenD, out bool hasD);
                        GetExtent(board, width, height, r, c, 1, 0, testColor, out int lenU, out bool hasU);
                        GetExtent(board, width, height, r, c, 0, -1, testColor, out int lenL, out bool hasL);
                        GetExtent(board, width, height, r, c, 0, 1, testColor, out int lenR, out bool hasR);

                        // L-Shapes and Straight Lines checks
                        // Down-Right corner
                        if (lenD + lenR - 1 >= 3 && (hasD || hasR))
                        {
                            maxSz = Math.Max(maxSz, lenD + lenR - 1);
                            for (int step = 0; step < lenR; step++) into.Add((r, c + step));
                            for (int step = 0; step < lenD; step++) into.Add((r - step, c));
                        }

                        // Down-Left corner
                        if (lenD + lenL - 1 >= 3 && (hasD || hasL))
                        {
                            maxSz = Math.Max(maxSz, lenD + lenL - 1);
                            for (int step = 0; step < lenL; step++) into.Add((r, c - step));
                            for (int step = 0; step < lenD; step++) into.Add((r - step, c));
                        }

                        // Up-Right corner
                        if (lenU + lenR - 1 >= 3 && (hasU || hasR))
                        {
                            maxSz = Math.Max(maxSz, lenU + lenR - 1);
                            for (int step = 0; step < lenR; step++) into.Add((r, c + step));
                            for (int step = 0; step < lenU; step++) into.Add((r + step, c));
                        }

                        // Up-Left corner
                        if (lenU + lenL - 1 >= 3 && (hasU || hasL))
                        {
                            maxSz = Math.Max(maxSz, lenU + lenL - 1);
                            for (int step = 0; step < lenL; step++) into.Add((r, c - step));
                            for (int step = 0; step < lenU; step++) into.Add((r + step, c));
                        }
                    }
                }
            }

            largestMatchSize = maxSz;
        }

        public static void CollectMatches(TileKind[,] board, int width, int height, HashSet<(int r, int c)> into)
        {
            CollectMatches(board, width, height, into, out _);
        }

        static void GetExtent(TileKind[,] board, int W, int H, int startR, int startC, int dr, int dc, TileKind testColor, out int length, out bool hasColor)
        {
            length = 0;
            hasColor = false;
            int r = startR;
            int c = startC;
            
            while (r >= 0 && r < H && c >= 0 && c < W)
            {
                TileKind t = board[r, c];
                if (t == testColor)
                {
                    length++;
                    hasColor = true;
                }
                else if (t == TileKind.Wild)
                {
                    length++;
                }
                else
                {
                    break;
                }
                
                r += dr;
                c += dc;
            }
        }
    }
}
