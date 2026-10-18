using System;

namespace Musketeers.Sim
{
    // Integer grid coordinate. x grows to the right, y grows upward (GAME_DESIGN §3).
    public readonly struct Int2 : IEquatable<Int2>
    {
        public readonly int X;
        public readonly int Y;

        public Int2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly Int2 Zero = new Int2(0, 0);

        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);
        public static Int2 operator *(Int2 a, int k) => new Int2(a.X * k, a.Y * k);
        public static bool operator ==(Int2 a, Int2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Int2 a, Int2 b) => !(a == b);

        public int Manhattan(Int2 other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public bool Equals(Int2 other) => this == other;
        public override bool Equals(object obj) => obj is Int2 o && this == o;
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";
    }
}
