using System.Text;
using Evermore.Azeroth.Simulation;

namespace Evermore.ThreeD.Environment;

public sealed class AzerothSoftwareRenderer
{
    public const int MinimumDimension = 64;
    public const int MaximumDimension = 2_048;
    public const long MaximumPixels = 4_194_304;
    public const string Format = "ppm-p6";
    public const string VisualAuthority = "ConstrainedSimulationConceptOnly";

    public byte[] Render(AzerothOrbitalSceneSnapshot scene, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidateDimensions(width, height);
        if (scene.Disposition != AzerothSimulationDisposition.Completed)
            throw new InvalidOperationException("A contained orbital scene cannot be rendered.");

        byte[] header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        byte[] output = new byte[checked(header.Length + (width * height * 3))];
        header.CopyTo(output, 0);

        double aspect = (double)width / height;
        double radius = 0.58 + (scene.Camera.ZoomLevel * 0.045);
        double atmosphere = scene.Lighting.AtmosphereShellSignalMicros / 1_000_000.0;
        double mana = scene.Lighting.ManaVisibilitySignalMicros / 1_000_000.0;
        double phase = scene.Lighting.SoluneOrbitPhaseMicros / 1_000_000.0;
        double cameraAzimuth = (scene.Camera.OrbitAzimuthMicros / 1_000_000.0) * Math.Tau;
        double cameraElevation = (scene.Camera.OrbitElevationMicros / 1_000_000.0) * Math.PI;
        double azimuthCosine = Math.Cos(cameraAzimuth);
        double azimuthSine = Math.Sin(cameraAzimuth);
        double elevationCosine = Math.Cos(cameraElevation);
        double elevationSine = Math.Sin(cameraElevation);
        double lightAngle = phase * Math.Tau;
        double lightX = Math.Cos(lightAngle) * 0.82;
        double lightY = 0.24;
        double lightZ = Math.Sin(lightAngle) * 0.52;
        double lightLength = Math.Sqrt((lightX * lightX) + (lightY * lightY) + (lightZ * lightZ));
        lightX /= lightLength;
        lightY /= lightLength;
        lightZ /= lightLength;

        int offset = header.Length;
        for (int y = 0; y < height; y++)
        {
            double normalizedY = 1.0 - (((y + 0.5) / height) * 2.0);
            for (int x = 0; x < width; x++)
            {
                double normalizedX = ((((x + 0.5) / width) * 2.0) - 1.0) * aspect;
                double distanceSquared = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                Rgb24 pixel = distanceSquared <= radius * radius
                    ? ShadeGlobe(
                        normalizedX,
                        normalizedY,
                        radius,
                        atmosphere,
                        mana,
                        phase,
                        azimuthCosine,
                        azimuthSine,
                        elevationCosine,
                        elevationSine,
                        lightX,
                        lightY,
                        lightZ)
                    : ShadeSpace(x, y, width, distanceSquared, radius, atmosphere);
                output[offset++] = pixel.Red;
                output[offset++] = pixel.Green;
                output[offset++] = pixel.Blue;
            }
        }

        return output;
    }

    public static void ValidateDimensions(int width, int height)
    {
        if (width < MinimumDimension || width > MaximumDimension)
            throw new ArgumentOutOfRangeException(nameof(width), $"Width must be between {MinimumDimension} and {MaximumDimension} pixels.");
        if (height < MinimumDimension || height > MaximumDimension)
            throw new ArgumentOutOfRangeException(nameof(height), $"Height must be between {MinimumDimension} and {MaximumDimension} pixels.");
        if ((long)width * height > MaximumPixels)
            throw new ArgumentOutOfRangeException(nameof(width), $"The framebuffer cannot exceed {MaximumPixels} pixels.");
    }

    private static Rgb24 ShadeGlobe(
        double x,
        double y,
        double radius,
        double atmosphere,
        double mana,
        double phase,
        double azimuthCosine,
        double azimuthSine,
        double elevationCosine,
        double elevationSine,
        double lightX,
        double lightY,
        double lightZ)
    {
        double viewX = x / radius;
        double viewY = y / radius;
        double viewZ = Math.Sqrt(Math.Max(0.0, 1.0 - (viewX * viewX) - (viewY * viewY)));
        double elevatedY = (viewY * elevationCosine) - (viewZ * elevationSine);
        double elevatedZ = (viewY * elevationSine) + (viewZ * elevationCosine);
        double worldX = (viewX * azimuthCosine) + (elevatedZ * azimuthSine);
        double worldY = elevatedY;
        double worldZ = (-viewX * azimuthSine) + (elevatedZ * azimuthCosine);
        double diffuse = Math.Max(0.0, (worldX * lightX) + (worldY * lightY) + (worldZ * lightZ));
        double illumination = 0.10 + (diffuse * 0.90);
        double limb = Math.Pow(1.0 - viewZ, 2.2) * atmosphere;
        double manaWave = Math.Abs(Math.Sin(((worldX * 3.0) + (worldY * 5.0) + phase) * Math.PI));
        double manaGlow = Math.Pow(Math.Max(0.0, manaWave - 0.82) / 0.18, 2.0) * mana * 0.75;

        Rgb24 abyss = new(0x00, 0x1C, 0x39);
        Rgb24 ocean = new(0x1A, 0x69, 0xAE);
        Rgb24 cyan = new(0x1A, 0xB0, 0xE2);
        Rgb24 arcane = new(0xA1, 0x2D, 0x6C);
        Rgb24 solar = new(0xF5, 0xD6, 0x23);
        Rgb24 baseColor = Mix(abyss, ocean, Math.Clamp(illumination, 0.0, 1.0));
        baseColor = Mix(baseColor, solar, Math.Pow(diffuse, 12.0) * 0.28);
        baseColor = Mix(baseColor, cyan, Math.Clamp(limb, 0.0, 0.65));
        return Mix(baseColor, arcane, Math.Clamp(manaGlow, 0.0, 0.65));
    }

    private static Rgb24 ShadeSpace(
        int x,
        int y,
        int width,
        double distanceSquared,
        double radius,
        double atmosphere)
    {
        double distance = Math.Sqrt(distanceSquared);
        double haloLimit = radius * (1.0 + (0.055 * atmosphere));
        if (atmosphere > 0.0 && distance <= haloLimit)
        {
            double halo = 1.0 - ((distance - radius) / Math.Max(0.000001, haloLimit - radius));
            return Mix(new Rgb24(0x01, 0x01, 0x01), new Rgb24(0x1A, 0xB0, 0xE2), halo * 0.72);
        }

        uint hash = unchecked(((uint)x * 73_856_093U) ^ ((uint)y * 19_349_663U) ^ ((uint)width * 83_492_791U));
        return hash % 997U == 0U
            ? new Rgb24(0xDA, 0xDA, 0xDA)
            : new Rgb24(0x01, 0x01, 0x01);
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
