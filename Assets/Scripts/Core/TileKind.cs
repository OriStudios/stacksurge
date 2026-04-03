namespace StackSurge.Core
{
    /// <summary>
    /// Row 0 = bottom, row Height-1 = top. Five candy colors plus specials.
    /// </summary>
    public enum TileKind
    {
        Empty = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        Wild = 6,
        Bomb = 7
    }

    public static class TileKindExtensions
    {
        public static bool IsColor(this TileKind k) =>
            k is >= TileKind.Red and <= TileKind.Purple;

        public static bool IsEmpty(this TileKind k) => k == TileKind.Empty;

        public static bool IsSolid(this TileKind k) =>
            k is >= TileKind.Red and <= TileKind.Wild;
    }
}
