using System.Text;

namespace Evermore.ThreeD.Environment;

public sealed class OmegaSandboxSoftwareRenderer
{
    public const string Format = "ppm-p6-18d-projection";
    public const string ProjectionAuthority = "sandbox-projection-only-not-physical-spacetime";

    public byte[] Render(
        OmegaSandboxSnapshot snapshot,
        int width,
        int height,
        int axisX,
        int axisY,
        int axisDepth)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        AzerothSoftwareRenderer.ValidateDimensions(width, height);
        ValidateAxes(axisX, axisY, axisDepth);
        if (snapshot.Disposition != OmegaSandboxDisposition.Completed)
            throw new InvalidOperationException("A contained Omega Sandbox cannot be rendered.");
        if (snapshot.DimensionNames.Length != OmegaSandboxDimensions.Count)
            throw new InvalidDataException("The Omega Sandbox must expose exactly 18 dimensions.");

        byte[] header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        byte[] output = new byte[checked(header.Length + (width * height * 3))];
        header.CopyTo(output, 0);
        FillSpace(output.AsSpan(header.Length), width, height, snapshot.Input.Seed, axisX, axisY, axisDepth);

        foreach (OmegaSandboxSystem system in snapshot.Systems)
        {
            DrawBody(output, header.Length, width, height, system.Hearthstar, axisX, axisY, axisDepth);
            foreach (OmegaSandboxBody world in system.Worlds)
                DrawBody(output, header.Length, width, height, world, axisX, axisY, axisDepth);
            foreach (OmegaSandboxBody moon in system.Moons)
                DrawBody(output, header.Length, width, height, moon, axisX, axisY, axisDepth);
        }
        return output;
    }

    public static void ValidateAxes(int axisX, int axisY, int axisDepth)
    {
        if (axisX is < 0 or >= OmegaSandboxDimensions.Count)
            throw new ArgumentOutOfRangeException(nameof(axisX), "Projection axis X must be between 0 and 17.");
        if (axisY is < 0 or >= OmegaSandboxDimensions.Count)
            throw new ArgumentOutOfRangeException(nameof(axisY), "Projection axis Y must be between 0 and 17.");
        if (axisDepth is < 0 or >= OmegaSandboxDimensions.Count)
            throw new ArgumentOutOfRangeException(nameof(axisDepth), "Projection depth axis must be between 0 and 17.");
        if (axisX == axisY || axisX == axisDepth || axisY == axisDepth)
            throw new ArgumentException("Projection axes must be distinct.", nameof(axisX));
    }

    private static void FillSpace(
        Span<byte> pixels,
        int width,
        int height,
        ulong seed,
        int axisX,
        int axisY,
        int axisDepth)
    {
        uint seedFold = unchecked((uint)seed ^ (uint)(seed >> 32));
        int offset = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                uint hash = unchecked(
                    ((uint)x * 73_856_093U) ^
                    ((uint)y * 19_349_663U) ^
                    ((uint)width * 83_492_791U) ^
                    seedFold ^
                    ((uint)axisX << 24) ^
                    ((uint)axisY << 16) ^
                    ((uint)axisDepth << 8));
                byte star = hash % 1_013U == 0U ? (byte)0xA1 : (byte)0x01;
                pixels[offset++] = star;
                pixels[offset++] = star;
                pixels[offset++] = star;
            }
        }
    }

    private static void DrawBody(
        byte[] output,
        int headerLength,
        int width,
        int height,
        OmegaSandboxBody body,
        int axisX,
        int axisY,
        int axisDepth)
    {
        if (body.DimensionVector.Length != OmegaSandboxDimensions.Count)
            throw new InvalidDataException($"Body {body.BodyId} does not contain an 18-dimensional vector.");
        int centerX = Project(body.DimensionVector[axisX], width);
        int centerY = (height - 1) - Project(body.DimensionVector[axisY], height);
        double depth = (body.DimensionVector[axisDepth] - OmegaSandboxDimensions.MinimumValue) /
            (double)(OmegaSandboxDimensions.MaximumValue - OmegaSandboxDimensions.MinimumValue);
        int baseRadius = body.BodyKind switch
        {
            OmegaSandboxBodyKinds.Hearthstar => 4,
            OmegaSandboxBodyKinds.World => 2,
            OmegaSandboxBodyKinds.Moon => 1,
            _ => throw new InvalidDataException($"Unknown Omega Sandbox body kind: {body.BodyKind}.")
        };
        int radius = Math.Clamp(
            baseRadius + (int)Math.Round(depth * 2.0, MidpointRounding.ToEven),
            1,
            7);
        Rgb24 color = SelectColor(body);
        DrawDisc(output, headerLength, width, height, centerX, centerY, radius, color, depth);
    }

    private static int Project(int value, int extent)
    {
        long shifted = (long)Math.Clamp(
            value,
            OmegaSandboxDimensions.MinimumValue,
            OmegaSandboxDimensions.MaximumValue) - OmegaSandboxDimensions.MinimumValue;
        return checked((int)((shifted * (extent - 1)) /
            (OmegaSandboxDimensions.MaximumValue - OmegaSandboxDimensions.MinimumValue)));
    }

    private static Rgb24 SelectColor(OmegaSandboxBody body)
    {
        Rgb24 solar = new(0xF5, 0xD6, 0x23);
        Rgb24 abyss = new(0x1A, 0x69, 0xAE);
        Rgb24 arcane = new(0xA1, 0x2D, 0x6C);
        Rgb24 moon = new(0xDA, 0xDA, 0xDA);
        Rgb24 cyan = new(0x1A, 0xB0, 0xE2);
        return body.BodyKind switch
        {
            OmegaSandboxBodyKinds.Hearthstar => solar,
            OmegaSandboxBodyKinds.World => Mix(
                abyss,
                arcane,
                body.DimensionVector[OmegaSandboxDimensions.Mana] / 1_000_000.0),
            OmegaSandboxBodyKinds.Moon => Mix(
                moon,
                cyan,
                body.DimensionVector[OmegaSandboxDimensions.Tidal] / 1_000_000.0),
            _ => throw new InvalidDataException($"Unknown Omega Sandbox body kind: {body.BodyKind}.")
        };
    }

    private static void DrawDisc(
        byte[] output,
        int headerLength,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        Rgb24 color,
        double depth)
    {
        for (int y = Math.Max(0, centerY - radius); y <= Math.Min(height - 1, centerY + radius); y++)
        {
            for (int x = Math.Max(0, centerX - radius); x <= Math.Min(width - 1, centerX + radius); x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if ((dx * dx) + (dy * dy) > radius * radius)
                    continue;
                double centerGlow = 1.0 - (Math.Sqrt((dx * dx) + (dy * dy)) / Math.Max(1.0, radius));
                double intensity = Math.Clamp(0.45 + (depth * 0.35) + (centerGlow * 0.35), 0.0, 1.0);
                int offset = headerLength + (((y * width) + x) * 3);
                var existing = new Rgb24(output[offset], output[offset + 1], output[offset + 2]);
                Rgb24 mixed = Mix(existing, color, intensity);
                output[offset] = mixed.Red;
                output[offset + 1] = mixed.Green;
                output[offset + 2] = mixed.Blue;
            }
        }
    }

    private static Rgb24 Mix(Rgb24 from, Rgb24 to, double amount)
    {
        double bounded = Math.Clamp(amount, 0.0, 1.0);
        return new Rgb24(
            ToByte(from.Red + ((to.Red - from.Red) * bounded)),
            ToByte(from.Green + ((to.Green - from.Green) * bounded)),
            ToByte(from.Blue + ((to.Blue - from.Blue) * bounded)));
    }

    private static byte ToByte(double value) =>
        checked((byte)Math.Clamp((int)Math.Round(value, MidpointRounding.ToEven), 0, 255));

    private readonly record struct Rgb24(byte Red, byte Green, byte Blue);
}
