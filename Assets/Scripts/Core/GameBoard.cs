using System.Collections.Generic;
using UnityEngine;

namespace StackSurge.Core
{
    /// <summary>
    /// Indexing: [row, col], row 0 = bottom, row Height-1 = top. Rising pushes tiles upward; overflow at top = game over.
    /// </summary>
    public class GameBoard
    {
        readonly TileKind[,] _cells;
        readonly HashSet<(int r, int c)> _matchBuffer = new();

        public int Width { get; }
        public int Height { get; }

        public GameBoard(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new TileKind[height, width];
        }

        public TileKind[,] Cells => _cells;

        public float GetOccupancy01()
        {
            int n = Width * Height;
            int o = 0;
            for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
            {
                if (_cells[r, c] != TileKind.Empty) o++;
            }

            return (float)o / n;
        }

        public bool IsColumnFull(int col)
        {
            for (int r = 0; r < Height; r++)
                if (_cells[r, col] == TileKind.Empty) return false;
            return true;
        }

        /// <summary>Lowest empty row index, or -1 if column is full.</summary>
        public int GetLowestEmptyRow(int col)
        {
            for (int r = 0; r < Height; r++)
            {
                if (_cells[r, col] == TileKind.Empty) return r;
            }

            return -1;
        }

        public void SetCell(int col, int row, TileKind k) => _cells[row, col] = k;

        public bool IsBoardEmpty()
        {
            for (int r = 0; r < Height; r++)
            for (int c = 0; c < Width; c++)
            {
                if (_cells[r, c] != TileKind.Empty) return false;
            }

            return true;
        }

        public void ExplodeBomb3x3(int centerCol, int centerRow)
        {
            for (int dc = -1; dc <= 1; dc++)
            for (int dr = -1; dr <= 1; dr++)
            {
                int cc = centerCol + dc;
                int rr = centerRow + dr;
                if (cc < 0 || cc >= Width || rr < 0 || rr >= Height) continue;
                _cells[rr, cc] = TileKind.Empty;
            }
        }

        public void ApplyGravity()
        {
            for (int col = 0; col < Width; col++)
            {
                int write = 0;
                for (int read = 0; read < Height; read++)
                {
                    if (_cells[read, col] == TileKind.Empty) continue;
                    if (write != read) _cells[write, col] = _cells[read, col];
                    write++;
                }

                for (int r = write; r < Height; r++)
                    _cells[r, col] = TileKind.Empty;
            }
        }

        /// <summary>
        /// Clears matches repeatedly until stable. Returns total tiles cleared across all waves.
        /// </summary>
        public int ResolveMatchCascade(ScoreService score, float timeNow, out bool boardEmptyAfter, out int largestWaveSize)
        {
            int totalTiles = 0;
            largestWaveSize = 0;
            boardEmptyAfter = false;
            const int maxIterations = 64;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                MatchFinder.CollectMatches(_cells, Width, Height, _matchBuffer, out int largestInWave);
                if (_matchBuffer.Count == 0) break;

                bool fullRow = HasEntireRowInMatch(_matchBuffer);
                int cleared = _matchBuffer.Count;
                totalTiles += cleared;
                if (largestInWave > largestWaveSize) largestWaveSize = largestInWave;

                foreach (var (r, c) in _matchBuffer) _cells[r, c] = TileKind.Empty;

                ApplyGravity();

                bool perfect = IsBoardEmpty();
                score.RegisterClearWave(largestInWave, cleared, fullRow, perfect, timeNow, out _, out _, out _);

                if (perfect)
                {
                    boardEmptyAfter = true;
                    return totalTiles;
                }
            }

            boardEmptyAfter = IsBoardEmpty();
            return totalTiles;
        }

        bool HasEntireRowInMatch(HashSet<(int r, int c)> matchSet)
        {
            for (int r = 0; r < Height; r++)
            {
                bool ok = true;
                for (int c = 0; c < Width; c++)
                {
                    if (!matchSet.Contains((r, c)))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok) return true;
            }

            return false;
        }

        /// <summary>
        /// Inserts a new random bottom row and shifts existing tiles up. Returns false if overflow (game over).
        /// </summary>
        public bool TryRiseRow(System.Func<TileKind> rollCell)
        {
            for (int c = 0; c < Width; c++)
            {
                if (_cells[Height - 1, c] != TileKind.Empty) return false;
            }

            for (int c = 0; c < Width; c++)
            {
                for (int r = Height - 1; r >= 1; r--)
                    _cells[r, c] = _cells[r - 1, c];
                _cells[0, c] = rollCell();
            }

            return true;
        }
    }
}
