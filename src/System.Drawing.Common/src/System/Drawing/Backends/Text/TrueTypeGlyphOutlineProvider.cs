using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;

namespace System.Drawing;

internal readonly record struct TrueTypeGlyphPoint(float X, float Y, bool OnCurve);

internal readonly record struct PositionedGlyphOutline(TrueTypeGlyphOutline Outline, float X, float BaselineY, float Scale);

internal sealed class TrueTypeGlyphOutline
{
    public TrueTypeGlyphOutline(TrueTypeGlyphPoint[][] contours) => Contours = contours;

    public TrueTypeGlyphPoint[][] Contours { get; }

    public bool IsEmpty => Contours.Length == 0;
}

internal sealed class TrueTypeGlyphOutlineProvider
{
    private const byte OnCurve = 0x01;
    private const byte XShortVector = 0x02;
    private const byte YShortVector = 0x04;
    private const byte RepeatFlag = 0x08;
    private const byte XIsSameOrPositive = 0x10;
    private const byte YIsSameOrPositive = 0x20;
    private const ushort Arg1And2AreWords = 0x0001;
    private const ushort ArgsAreXyValues = 0x0002;
    private const ushort WeHaveAScale = 0x0008;
    private const ushort MoreComponents = 0x0020;
    private const ushort WeHaveAnXAndYScale = 0x0040;
    private const ushort WeHaveATwoByTwo = 0x0080;

    private static readonly ConcurrentDictionary<string, Lazy<TrueTypeGlyphOutlineProvider>> s_cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly byte[] _data;
    private readonly Dictionary<string, TableRecord> _tables;
    private readonly int _glyphCount;
    private readonly int _locaFormat;
    private readonly uint[] _glyphOffsets;
    private readonly ConcurrentDictionary<uint, TrueTypeGlyphOutline> _glyphCache = [];

    private TrueTypeGlyphOutlineProvider(string path)
    {
        _data = File.ReadAllBytes(path);
        _tables = ReadTableDirectory(_data);
        UnitsPerEm = ReadUInt16(GetTable("head").Offset + 18);
        _locaFormat = ReadInt16(GetTable("head").Offset + 50);
        _glyphCount = ReadUInt16(GetTable("maxp").Offset + 4);
        _glyphOffsets = ReadGlyphOffsets();
    }

    public ushort UnitsPerEm { get; }

    public static TrueTypeGlyphOutlineProvider GetOrCreate(string path) =>
        s_cache.GetOrAdd(path, static value => new Lazy<TrueTypeGlyphOutlineProvider>(() => new(value))).Value;

    public TrueTypeGlyphOutline GetGlyph(uint glyphId)
    {
        if (glyphId >= _glyphCount)
        {
            return new TrueTypeGlyphOutline([]);
        }

        return _glyphCache.GetOrAdd(glyphId, static (id, provider) => provider.LoadGlyph(id, depth: 0), this);
    }

    private TrueTypeGlyphOutline LoadGlyph(uint glyphId, int depth)
    {
        if (depth > 8)
        {
            return new TrueTypeGlyphOutline([]);
        }

        uint start = _glyphOffsets[glyphId];
        uint end = _glyphOffsets[glyphId + 1];
        if (start == end)
        {
            return new TrueTypeGlyphOutline([]);
        }

        TableRecord glyf = GetTable("glyf");
        int glyphOffset = checked(glyf.Offset + (int)start);
        short numberOfContours = ReadInt16(glyphOffset);
        if (numberOfContours >= 0)
        {
            return ReadSimpleGlyph(glyphOffset, numberOfContours);
        }

        return ReadCompoundGlyph(glyphOffset, depth);
    }

    private TrueTypeGlyphOutline ReadSimpleGlyph(int glyphOffset, int contourCount)
    {
        if (contourCount == 0)
        {
            return new TrueTypeGlyphOutline([]);
        }

        int endPtsOffset = glyphOffset + 10;
        ushort[] endPts = new ushort[contourCount];
        for (int i = 0; i < endPts.Length; i++)
        {
            endPts[i] = ReadUInt16(endPtsOffset + (i * 2));
        }

        int pointCount = endPts[^1] + 1;
        int instructionLengthOffset = endPtsOffset + (contourCount * 2);
        int instructionLength = ReadUInt16(instructionLengthOffset);
        int offset = instructionLengthOffset + 2 + instructionLength;

        byte[] flags = new byte[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            byte flag = _data[offset++];
            flags[i] = flag;
            if ((flag & RepeatFlag) == 0)
            {
                continue;
            }

            int repeat = _data[offset++];
            for (int r = 0; r < repeat; r++)
            {
                flags[++i] = flag;
            }
        }

        short[] xs = ReadCoordinateDeltas(flags, ref offset, xAxis: true);
        short[] ys = ReadCoordinateDeltas(flags, ref offset, xAxis: false);

        TrueTypeGlyphPoint[][] contours = new TrueTypeGlyphPoint[contourCount][];
        int start = 0;
        for (int contour = 0; contour < contourCount; contour++)
        {
            int end = endPts[contour];
            TrueTypeGlyphPoint[] points = new TrueTypeGlyphPoint[end - start + 1];
            for (int p = start; p <= end; p++)
            {
                points[p - start] = new TrueTypeGlyphPoint(xs[p], ys[p], (flags[p] & OnCurve) != 0);
            }

            contours[contour] = points;
            start = end + 1;
        }

        return new TrueTypeGlyphOutline(contours);
    }

    private short[] ReadCoordinateDeltas(byte[] flags, ref int offset, bool xAxis)
    {
        short[] values = new short[flags.Length];
        int current = 0;
        byte shortVector = xAxis ? XShortVector : YShortVector;
        byte sameOrPositive = xAxis ? XIsSameOrPositive : YIsSameOrPositive;

        for (int i = 0; i < flags.Length; i++)
        {
            int delta;
            if ((flags[i] & shortVector) != 0)
            {
                int magnitude = _data[offset++];
                delta = (flags[i] & sameOrPositive) != 0 ? magnitude : -magnitude;
            }
            else
            {
                delta = (flags[i] & sameOrPositive) != 0 ? 0 : ReadInt16(offset);
                if ((flags[i] & sameOrPositive) == 0)
                {
                    offset += 2;
                }
            }

            current += delta;
            values[i] = checked((short)current);
        }

        return values;
    }

    private TrueTypeGlyphOutline ReadCompoundGlyph(int glyphOffset, int depth)
    {
        if (depth > 8)
        {
            return new TrueTypeGlyphOutline([]);
        }

        List<TrueTypeGlyphPoint[]> contours = [];
        int offset = glyphOffset + 10;
        ushort flags;
        do
        {
            flags = ReadUInt16(offset);
            ushort componentGlyphId = ReadUInt16(offset + 2);
            offset += 4;

            float arg1;
            float arg2;
            if ((flags & Arg1And2AreWords) != 0)
            {
                arg1 = ReadInt16(offset);
                arg2 = ReadInt16(offset + 2);
                offset += 4;
            }
            else
            {
                arg1 = unchecked((sbyte)_data[offset]);
                arg2 = unchecked((sbyte)_data[offset + 1]);
                offset += 2;
            }

            float dx = (flags & ArgsAreXyValues) != 0 ? arg1 : 0f;
            float dy = (flags & ArgsAreXyValues) != 0 ? arg2 : 0f;
            float xx = 1f;
            float xy = 0f;
            float yx = 0f;
            float yy = 1f;

            if ((flags & WeHaveAScale) != 0)
            {
                xx = yy = ReadF2Dot14(offset);
                offset += 2;
            }
            else if ((flags & WeHaveAnXAndYScale) != 0)
            {
                xx = ReadF2Dot14(offset);
                yy = ReadF2Dot14(offset + 2);
                offset += 4;
            }
            else if ((flags & WeHaveATwoByTwo) != 0)
            {
                xx = ReadF2Dot14(offset);
                xy = ReadF2Dot14(offset + 2);
                yx = ReadF2Dot14(offset + 4);
                yy = ReadF2Dot14(offset + 6);
                offset += 8;
            }

            TrueTypeGlyphOutline component = LoadGlyph(componentGlyphId, depth + 1);
            foreach (TrueTypeGlyphPoint[] contour in component.Contours)
            {
                TrueTypeGlyphPoint[] transformed = new TrueTypeGlyphPoint[contour.Length];
                for (int i = 0; i < contour.Length; i++)
                {
                    TrueTypeGlyphPoint point = contour[i];
                    transformed[i] = new TrueTypeGlyphPoint(
                        (point.X * xx) + (point.Y * yx) + dx,
                        (point.X * xy) + (point.Y * yy) + dy,
                        point.OnCurve);
                }

                contours.Add(transformed);
            }
        }
        while ((flags & MoreComponents) != 0);

        return new TrueTypeGlyphOutline([.. contours]);
    }

    private uint[] ReadGlyphOffsets()
    {
        TableRecord loca = GetTable("loca");
        uint[] offsets = new uint[_glyphCount + 1];

        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = _locaFormat == 0
                ? (uint)ReadUInt16(loca.Offset + (i * 2)) * 2u
                : ReadUInt32(loca.Offset + (i * 4));
        }

        return offsets;
    }

    private static Dictionary<string, TableRecord> ReadTableDirectory(byte[] data)
    {
        int tableCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4, 2));
        Dictionary<string, TableRecord> tables = new(StringComparer.Ordinal);
        int offset = 12;
        for (int i = 0; i < tableCount; i++)
        {
            string tag = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            tables[tag] = new TableRecord(
                checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8, 4))),
                checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 12, 4))));
            offset += 16;
        }

        return tables;
    }

    private TableRecord GetTable(string tag) =>
        _tables.TryGetValue(tag, out TableRecord table)
            ? table
            : throw new InvalidDataException($"Font is missing required '{tag}' table.");

    private ushort ReadUInt16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset, 2));

    private uint ReadUInt32(int offset) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset, 4));

    private short ReadInt16(int offset) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(offset, 2));

    private float ReadF2Dot14(int offset) => ReadInt16(offset) / 16384f;

    private readonly record struct TableRecord(int Offset, int Length);
}
