using System.Collections.Generic;

namespace StackSurge.Core
{
    /// <summary>
    /// Row/column contiguous matches and L-shapes (corner + two perpendicular arms).
    /// Wild participates only when at least one non-wild defines the color; all-wild groups do not clear.
    /// </summary>
    public static class MatchFinder
    {
        public static void CollectMatches(TileKind[,] board, int width, int height, HashSet<(int r, int c)> into,
            out int largestMatchSize)
        {
            into.Clear();
            largestMatchSize = 0;
            AddHorizontalMatches(board, width, height, into, ref largestMatchSize);
            AddVerticalMatches(board, width, height, into, ref largestMatchSize);
            AddLShapeMatches(board, width, height, into, ref largestMatchSize);
        }

        public static void CollectMatches(TileKind[,] board, int width, int height, HashSet<(int r, int c)> into)
        {
            CollectMatches(board, width, height, into, out _);
        }

        static void AddHorizontalMatches(TileKind[,] board, int width, int height, HashSet<(int, int)> into,
            ref int largestMatchSize)
        {
            for (int r = 0; r < height; r++)
            {
                int c = 0;
                while (c < width)
                {
                    if (board[r, c] == TileKind.Empty) { c++; continue; }
                    int start = c;
                    while (c < width && board[r, c] != TileKind.Empty) c++;
                    AddLineSegment(board, width, height, r, start, c - 1, true, into, ref largestMatchSize);
                }
            }
        }

        static void AddVerticalMatches(TileKind[,] board, int width, int height, HashSet<(int, int)> into,
            ref int largestMatchSize)
        {
            for (int c = 0; c < width; c++)
            {
                int r = 0;
                while (r < height)
                {
                    if (board[r, c] == TileKind.Empty) { r++; continue; }
                    int start = r;
                    while (r < height && board[r, c] != TileKind.Empty) r++;
                    AddLineSegment(board, width, height, c, start, r - 1, false, into, ref largestMatchSize);
                }
            }
        }

        /// <param name="lineCoord">Column index when horizontal line; row index when vertical line.</param>
        static void AddLineSegment(TileKind[,] board, int width, int height, int lineCoord, int a0, int a1, bool horizontal,
            HashSet<(int, int)> into, ref int largestMatchSize)
        {
            int start = a0;
            while (start <= a1)
            {
                if (horizontal && board[lineCoord, start] == TileKind.Empty ||
                    !horizontal && board[start, lineCoord] == TileKind.Empty)
                {
                    start++;
                    continue;
                }

                TileKind groupColor = TileKind.Empty;
                int end = start;
                while (end <= a1)
                {
                    TileKind t = horizontal ? board[lineCoord, end] : board[end, lineCoord];
                    if (t == TileKind.Empty) break;
                    if (t == TileKind.Wild)
                    {
                        if (groupColor == TileKind.Empty)
                        {
                            end++;
                            continue;
                        }

                        end++;
                        continue;
                    }

                    if (!t.IsColor()) break;
                    if (groupColor == TileKind.Empty) groupColor = t;
                    else if (t != groupColor) break;
                    end++;
                }

                if (end - start >= 3 && groupColor != TileKind.Empty)
                {
                    int len = end - start;
                    if (len > largestMatchSize) largestMatchSize = len;
                    for (int k = start; k < end; k++)
                    {
                        var cell = horizontal ? (lineCoord, k) : (k, lineCoord);
                        into.Add(cell);
                    }
                }

                start = end;
            }
        }

        static void AddLShapeMatches(TileKind[,] board, int width, int height, HashSet<(int, int)> into,
            ref int largestMatchSize)
        {
            var scratch = new List<(int r, int c)>(32);
            for (int r = 0; r < height; r++)
            for (int c = 0; c < width; c++)
            {
                if (board[r, c] == TileKind.Empty) continue;
                TryLDownRight(board, width, height, r, c, into, scratch, ref largestMatchSize);
                TryLDownLeft(board, width, height, r, c, into, scratch, ref largestMatchSize);
                TryLUpRight(board, width, height, r, c, into, scratch, ref largestMatchSize);
                TryLUpLeft(board, width, height, r, c, into, scratch, ref largestMatchSize);
            }
        }

        static void TryLDownRight(TileKind[,] b, int W, int H, int r, int c, HashSet<(int, int)> into, List<(int, int)> scratch,
            ref int largestMatchSize)
        {
            for (int v = 1; r + v - 1 < H; v++)
            for (int h = 1; c + h - 1 < W; h++)
            {
                int sz = v + h - 1;
                if (sz < 3) continue;
                scratch.Clear();
                for (int i = 0; i < v; i++) scratch.Add((r + i, c));
                for (int j = 0; j < h; j++) scratch.Add((r, c + j));
                if (TryInferGroupColor(scratch, b, out _))
                {
                    if (sz > largestMatchSize) largestMatchSize = sz;
                    foreach (var x in scratch) into.Add(x);
                }
            }
        }

        static void TryLDownLeft(TileKind[,] b, int W, int H, int r, int c, HashSet<(int, int)> into, List<(int, int)> scratch,
            ref int largestMatchSize)
        {
            for (int v = 1; r + v - 1 < H; v++)
            for (int h = 1; c - h + 1 >= 0; h++)
            {
                int sz = v + h - 1;
                if (sz < 3) continue;
                scratch.Clear();
                for (int i = 0; i < v; i++) scratch.Add((r + i, c));
                for (int j = 0; j < h; j++) scratch.Add((r, c - j));
                if (TryInferGroupColor(scratch, b, out _))
                {
                    if (sz > largestMatchSize) largestMatchSize = sz;
                    foreach (var x in scratch) into.Add(x);
                }
            }
        }

        static void TryLUpRight(TileKind[,] b, int W, int H, int r, int c, HashSet<(int, int)> into, List<(int, int)> scratch,
            ref int largestMatchSize)
        {
            for (int v = 1; r - v + 1 >= 0; v++)
            for (int h = 1; c + h - 1 < W; h++)
            {
                int sz = v + h - 1;
                if (sz < 3) continue;
                scratch.Clear();
                for (int i = 0; i < v; i++) scratch.Add((r - i, c));
                for (int j = 0; j < h; j++) scratch.Add((r, c + j));
                if (TryInferGroupColor(scratch, b, out _))
                {
                    if (sz > largestMatchSize) largestMatchSize = sz;
                    foreach (var x in scratch) into.Add(x);
                }
            }
        }

        static void TryLUpLeft(TileKind[,] b, int W, int H, int r, int c, HashSet<(int, int)> into, List<(int, int)> scratch,
            ref int largestMatchSize)
        {
            for (int v = 1; r - v + 1 >= 0; v++)
            for (int h = 1; c - h + 1 >= 0; h++)
            {
                int sz = v + h - 1;
                if (sz < 3) continue;
                scratch.Clear();
                for (int i = 0; i < v; i++) scratch.Add((r - i, c));
                for (int j = 0; j < h; j++) scratch.Add((r, c - j));
                if (TryInferGroupColor(scratch, b, out _))
                {
                    if (sz > largestMatchSize) largestMatchSize = sz;
                    foreach (var x in scratch) into.Add(x);
                }
            }
        }

        static bool TryInferGroupColor(List<(int r, int c)> cells, TileKind[,] board, out TileKind color)
        {
            color = TileKind.Empty;
            foreach (var (r, c) in cells)
            {
                var t = board[r, c];
                if (t == TileKind.Wild) continue;
                if (!t.IsColor()) return false;
                if (color == TileKind.Empty) color = t;
                else if (t != color) return false;
            }

            return color != TileKind.Empty;
        }
    }
}
