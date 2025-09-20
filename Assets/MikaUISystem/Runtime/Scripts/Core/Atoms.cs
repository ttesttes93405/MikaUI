namespace MikaUISystem
{

    public readonly struct Float2
    {
        public float X { get; init; }
        public float Y { get; init; }

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public readonly struct Float3
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public readonly struct Float4
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }
        public float W { get; init; }

        public Float4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    public readonly struct RectOffset
    {
        public int Left { get; init; }
        public int Right { get; init; }
        public int Top { get; init; }
        public int Bottom { get; init; }

        public RectOffset(int left, int right, int top, int bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

}