using System.Text;

namespace Aurora.Platform.Windows.ShellExtras;

/// <summary>
/// Zero-dependency, standards-compliant ISO/IEC 18004 QR Code encoder (Byte mode, EC Level L, Versions 1–10)
/// generating crisp inline SVG markup for instant iOS/Android camera scanning.
/// </summary>
public static class QrCodeSvgGenerator
{
    // Capacity in data codewords and EC codewords per block for EC Level L, Versions 1..10
    private static readonly (int TotalDataCw, int EcCwPerBlock, int NumBlocksG1, int DataCwG1, int NumBlocksG2, int DataCwG2)[] VersionTable =
    [
        (0, 0, 0, 0, 0, 0),       // unused 0
        (19, 7, 1, 19, 0, 0),     // V1  (21x21)
        (34, 10, 1, 34, 0, 0),    // V2  (25x25)
        (55, 15, 1, 55, 0, 0),    // V3  (29x29)
        (80, 20, 1, 80, 0, 0),    // V4  (33x33)
        (108, 26, 1, 108, 0, 0),  // V5  (37x37)
        (136, 18, 2, 68, 0, 0),   // V6  (41x41)
        (156, 20, 2, 78, 0, 0),   // V7  (45x45)
        (194, 24, 2, 97, 0, 0),   // V8  (49x49)
        (232, 30, 2, 116, 0, 0),  // V9  (53x53)
        (274, 18, 2, 68, 2, 69),  // V10 (57x57)
    ];

    private static readonly int[][] AlignmentCenters =
    [
        [],
        [],
        [6, 18],
        [6, 22],
        [6, 26],
        [6, 30],
        [6, 34],
        [6, 22, 38],
        [6, 24, 42],
        [6, 26, 46],
        [6, 28, 50]
    ];

    // Precomputed 15-bit format strings for EC Level L (01) and masks 0..7 (with XOR 0x5412 applied)
    private static readonly int[] FormatBitsLevelL =
    [
        0x77C4, 0x72F3, 0x7DAA, 0x789D, 0x662F, 0x6318, 0x6C41, 0x6976
    ];

    // Precomputed 18-bit version info for Versions 7..10
    private static readonly int[] VersionBits =
    [
        0, 0, 0, 0, 0, 0, 0, 0x07C94, 0x085BC, 0x09A99, 0x0A4D3
    ];

    public static bool[,] EncodeMatrix(string text)
    {
        byte[] payload = Encoding.UTF8.GetBytes(text ?? string.Empty);

        int version = -1;
        for (int v = 1; v <= 10; v++)
        {
            int countBits = v <= 9 ? 8 : 16;
            int neededBits = 4 + countBits + (payload.Length * 8);
            if (neededBits <= VersionTable[v].TotalDataCw * 8)
            {
                version = v;
                break;
            }
        }

        if (version < 0)
            throw new ArgumentException("QR payload exceeds supported Version 10 capacity (271 UTF-8 bytes).");

        var spec = VersionTable[version];
        var bits = new List<bool>(spec.TotalDataCw * 8);

        // Mode indicator: Byte (0100)
        AppendBits(bits, 0b0100, 4);
        // Character count indicator
        AppendBits(bits, payload.Length, version <= 9 ? 8 : 16);
        // Payload bytes
        foreach (byte b in payload)
            AppendBits(bits, b, 8);

        // Terminator (up to 4 zero bits)
        int maxBits = spec.TotalDataCw * 8;
        int termLen = Math.Min(4, maxBits - bits.Count);
        for (int i = 0; i < termLen; i++) bits.Add(false);

        // Pad to byte boundary
        while (bits.Count % 8 != 0) bits.Add(false);

        // Pad codewords 0xEC, 0x11
        byte[] padBytes = [0xEC, 0x11];
        int padIdx = 0;
        while (bits.Count < maxBits)
        {
            AppendBits(bits, padBytes[padIdx & 1], 8);
            padIdx++;
        }

        byte[] dataCw = new byte[spec.TotalDataCw];
        for (int i = 0; i < dataCw.Length; i++)
        {
            byte val = 0;
            for (int b = 0; b < 8; b++)
                val = (byte)((val << 1) | (bits[i * 8 + b] ? 1 : 0));
            dataCw[i] = val;
        }

        // Split into RS blocks & compute EC codewords
        var dataBlocks = new List<byte[]>();
        var ecBlocks = new List<byte[]>();
        byte[] generator = BuildRsGenerator(spec.EcCwPerBlock);

        int offset = 0;
        for (int i = 0; i < spec.NumBlocksG1; i++)
        {
            byte[] blk = dataCw[offset..(offset + spec.DataCwG1)];
            offset += spec.DataCwG1;
            dataBlocks.Add(blk);
            ecBlocks.Add(ComputeRsRemainder(blk, generator));
        }
        for (int i = 0; i < spec.NumBlocksG2; i++)
        {
            byte[] blk = dataCw[offset..(offset + spec.DataCwG2)];
            offset += spec.DataCwG2;
            dataBlocks.Add(blk);
            ecBlocks.Add(ComputeRsRemainder(blk, generator));
        }

        // Interleave data codewords, then EC codewords
        var finalBytes = new List<byte>(spec.TotalDataCw + (dataBlocks.Count * spec.EcCwPerBlock));
        int maxDataLen = Math.Max(spec.DataCwG1, spec.DataCwG2);
        for (int i = 0; i < maxDataLen; i++)
        {
            foreach (var blk in dataBlocks)
                if (i < blk.Length) finalBytes.Add(blk[i]);
        }
        for (int i = 0; i < spec.EcCwPerBlock; i++)
        {
            foreach (var ec in ecBlocks)
                finalBytes.Add(ec[i]);
        }

        int size = (version * 4) + 17;
        bool[,] baseModules = new bool[size, size];
        bool[,] isFunction = new bool[size, size];

        DrawFunctionPatterns(version, size, baseModules, isFunction);
        PlaceDataBits(size, baseModules, isFunction, finalBytes);

        // Evaluate all 8 masks and pick the lowest penalty score
        bool[,]? bestMatrix = null;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            var candidate = (bool[,])baseModules.Clone();
            ApplyMask(size, candidate, isFunction, mask);
            DrawFormatAndVersionBits(version, size, candidate, mask);
            int penalty = ComputePenalty(size, candidate);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                bestMatrix = candidate;
            }
        }

        return bestMatrix!;
    }

    public static string GenerateSvg(string text, int quietZone = 3)
    {
        var matrix = EncodeMatrix(text);
        int size = matrix.GetLength(0);
        int total = size + (quietZone * 2);

        var sb = new StringBuilder(4096);
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {total}\" shape-rendering=\"crispEdges\">");
        sb.Append($"<rect width=\"{total}\" height=\"{total}\" rx=\"2\" fill=\"#FFFFFF\"/>");
        sb.Append("<path fill=\"#0B1120\" d=\"");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (matrix[y, x])
                {
                    sb.Append('M').Append(x + quietZone).Append(',').Append(y + quietZone).Append("h1v1h-1z");
                }
            }
        }
        sb.Append("\"/></svg>");
        return sb.ToString();
    }

    private static void DrawFunctionPatterns(int version, int size, bool[,] m, bool[,] f)
    {
        // Timing patterns
        for (int i = 0; i < size; i++)
        {
            SetFunc(m, f, 6, i, (i & 1) == 0);
            SetFunc(m, f, i, 6, (i & 1) == 0);
        }

        // 3 Finder patterns + separators
        DrawFinder(m, f, size, 3, 3);
        DrawFinder(m, f, size, size - 4, 3);
        DrawFinder(m, f, size, 3, size - 4);

        // Alignment patterns
        var centers = AlignmentCenters[version];
        foreach (int cy in centers)
        {
            foreach (int cx in centers)
            {
                bool overlapsFinder =
                    (cx <= 8 && cy <= 8) ||
                    (cx >= size - 9 && cy <= 8) ||
                    (cx <= 8 && cy >= size - 9);
                if (overlapsFinder) continue;

                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        bool dark = Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1;
                        SetFunc(m, f, cx + dx, cy + dy, dark);
                    }
                }
            }
        }

        // Reserve format info areas + dark module
        for (int i = 0; i <= 8; i++)
        {
            if (i != 6) { SetFunc(m, f, 8, i, false); SetFunc(m, f, i, 8, false); }
        }
        for (int i = size - 8; i < size; i++)
        {
            SetFunc(m, f, 8, i, false);
            SetFunc(m, f, i, 8, false);
        }
        SetFunc(m, f, 8, size - 8, true); // Dark module

        // Reserve version info areas (v >= 7)
        if (version >= 7)
        {
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    SetFunc(m, f, i, size - 11 + j, false);
                    SetFunc(m, f, size - 11 + j, i, false);
                }
            }
        }
    }

    private static void DrawFinder(bool[,] m, bool[,] f, int size, int cx, int cy)
    {
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                bool dark = dist != 2 && dist != 4;
                SetFunc(m, f, x, y, dark);
            }
        }
    }

    private static void PlaceDataBits(int size, bool[,] m, bool[,] f, List<byte> bytes)
    {
        int bitIdx = 0;
        int totalBits = bytes.Count * 8;
        bool upward = true;

        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (int v = 0; v < size; v++)
            {
                int y = upward ? (size - 1 - v) : v;
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    if (f[y, x]) continue;
                    bool dark = false;
                    if (bitIdx < totalBits)
                    {
                        dark = ((bytes[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                        bitIdx++;
                    }
                    m[y, x] = dark;
                }
            }
            upward = !upward;
        }
    }

    private static void ApplyMask(int size, bool[,] m, bool[,] f, int mask)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (f[y, x]) continue;
                bool invert = mask switch
                {
                    0 => ((x + y) % 2) == 0,
                    1 => (y % 2) == 0,
                    2 => (x % 3) == 0,
                    3 => ((x + y) % 3) == 0,
                    4 => (((y / 2) + (x / 3)) % 2) == 0,
                    5 => ((x * y) % 2 + (x * y) % 3) == 0,
                    6 => (((x * y) % 2 + (x * y) % 3) % 2) == 0,
                    7 => (((x + y) % 2 + (x * y) % 3) % 2) == 0,
                    _ => false
                };
                if (invert) m[y, x] = !m[y, x];
            }
        }
    }

    private static void DrawFormatAndVersionBits(int version, int size, bool[,] m, int mask)
    {
        int fmt = FormatBitsLevelL[mask];
        for (int i = 0; i <= 5; i++) m[i, 8] = ((fmt >> i) & 1) != 0;
        m[7, 8] = ((fmt >> 6) & 1) != 0;
        m[8, 8] = ((fmt >> 7) & 1) != 0;
        m[8, 7] = ((fmt >> 8) & 1) != 0;
        for (int i = 9; i < 15; i++) m[8, 14 - i] = ((fmt >> i) & 1) != 0;

        for (int i = 0; i < 8; i++) m[8, size - 1 - i] = ((fmt >> i) & 1) != 0;
        for (int i = 8; i < 15; i++) m[size - 15 + i, 8] = ((fmt >> i) & 1) != 0;
        m[size - 8, 8] = true;

        if (version >= 7)
        {
            int vb = VersionBits[version];
            for (int i = 0; i < 18; i++)
            {
                bool bit = ((vb >> i) & 1) != 0;
                int a = size - 11 + (i % 3);
                int b = i / 3;
                m[b, a] = bit;
                m[a, b] = bit;
            }
        }
    }

    private static int ComputePenalty(int size, bool[,] m)
    {
        int penalty = 0;
        // Rule 1: runs of >= 5 same color in rows and columns
        for (int y = 0; y < size; y++)
        {
            int run = 1;
            for (int x = 1; x < size; x++)
            {
                if (m[y, x] == m[y, x - 1]) { run++; if (run == 5) penalty += 3; else if (run > 5) penalty += 1; }
                else run = 1;
            }
        }
        for (int x = 0; x < size; x++)
        {
            int run = 1;
            for (int y = 1; y < size; y++)
            {
                if (m[y, x] == m[y - 1, x]) { run++; if (run == 5) penalty += 3; else if (run > 5) penalty += 1; }
                else run = 1;
            }
        }
        // Rule 2: 2x2 blocks
        for (int y = 0; y < size - 1; y++)
        {
            for (int x = 0; x < size - 1; x++)
            {
                bool c = m[y, x];
                if (c == m[y, x + 1] && c == m[y + 1, x] && c == m[y + 1, x + 1])
                    penalty += 3;
            }
        }
        // Rule 4: dark ratio balance
        int dark = 0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (m[y, x]) dark++;
        int k = Math.Abs((dark * 20) - (size * size * 10)) / (size * size);
        penalty += k * 10;
        return penalty;
    }

    private static void SetFunc(bool[,] m, bool[,] f, int x, int y, bool dark)
    {
        m[y, x] = dark;
        f[y, x] = true;
    }

    private static void AppendBits(List<bool> bits, int val, int len)
    {
        for (int i = len - 1; i >= 0; i--)
            bits.Add(((val >> i) & 1) != 0);
    }

    private static byte[] BuildRsGenerator(int degree)
    {
        byte[] result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < degree; j++)
            {
                result[j] = GfMultiply(result[j], root);
                if (j + 1 < degree)
                    result[j] ^= result[j + 1];
            }
            root = GfMultiply(root, 0x02);
        }
        return result;
    }

    private static byte[] ComputeRsRemainder(byte[] data, byte[] gen)
    {
        byte[] rem = new byte[gen.Length];
        foreach (byte b in data)
        {
            byte factor = (byte)(b ^ rem[0]);
            Array.Copy(rem, 1, rem, 0, rem.Length - 1);
            rem[^1] = 0;
            for (int i = 0; i < gen.Length; i++)
                rem[i] ^= GfMultiply(gen[i], factor);
        }
        return rem;
    }

    private static byte GfMultiply(byte a, byte b)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((b >> i) & 1) * a;
        }
        return (byte)z;
    }
}
