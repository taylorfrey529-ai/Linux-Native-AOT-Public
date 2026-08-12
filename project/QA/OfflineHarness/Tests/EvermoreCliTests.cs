using System.Text.Json;
using Evermore.Cli;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class EvermoreCliTests
{
    [Fact]
    public async Task StatusJsonReportsTheSeparateCliEvidenceBoundary()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(["status", "--format", "json"], output, error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("XXIII", document.RootElement.GetProperty("phase").GetString());
        Assert.Equal("separate-evidence-required", document.RootElement.GetProperty("nativeAotCertification").GetString());
    }

    [Fact]
    public async Task ReplayVerificationAcceptsTheCanonicalFixture()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["verify", "replay", "--input", Fixture("valid-replay.json"), "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("replay", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("frameCount").GetInt32());
    }

    [Fact]
    public async Task CameraVerificationRequiresTheExplicitAuthorizedZone()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int success = await EvermoreCliApp.InvokeAsync(
            ["verify", "camera", "--input", Fixture("valid-camera.json"), "--zone", "zone.a"],
            output,
            error);
        int rejected = await EvermoreCliApp.InvokeAsync(
            ["verify", "camera", "--input", Fixture("valid-camera.json"), "--zone", "zone.b"],
            new StringWriter(),
            error = new StringWriter());

        Assert.Equal(EvermoreCliApp.SuccessExitCode, success);
        Assert.Equal(EvermoreCliApp.VerificationFailureExitCode, rejected);
        Assert.Contains("outside the authorized Eastern Kingdoms set", error.ToString());
    }

    [Fact]
    public async Task ReplayVerificationRejectsPayloadTampering()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["verify", "replay", "--input", Fixture("tampered-replay.json")],
            output,
            error);

        Assert.Equal(EvermoreCliApp.VerificationFailureExitCode, exitCode);
        Assert.Contains("integrity verification failed", error.ToString());
    }

    [Fact]
    public async Task MalformedReplayJsonReturnsVerificationFailure()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{\"authority\":");
            var error = new StringWriter();

            int exitCode = await EvermoreCliApp.InvokeAsync(
                ["verify", "replay", "--input", path],
                new StringWriter(),
                error);

            Assert.Equal(EvermoreCliApp.VerificationFailureExitCode, exitCode);
            Assert.Contains("VERIFICATION_FAILURE", error.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SceneVerificationRejectsSemanticTamperingAfterPayloadRehash()
    {
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["verify", "scene", "--input", Fixture("semantic-tampered-scene.json"), "--zone", "zone.a"],
            new StringWriter(),
            error);

        Assert.Equal(EvermoreCliApp.VerificationFailureExitCode, exitCode);
        Assert.Contains("semantic verification failed", error.ToString());
    }

    [Fact]
    public async Task ReplayVerificationDoesNotModifyItsInput()
    {
        string path = Fixture("valid-replay.json");
        byte[] before = await File.ReadAllBytesAsync(path);

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["verify", "replay", "--input", path],
            new StringWriter(),
            new StringWriter());

        byte[] after = await File.ReadAllBytesAsync(path);
        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task SceneVerificationAcceptsTheCanonicalRendererNeutralFixture()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["verify", "scene", "--input", Fixture("valid-scene.json"), "--zone", "zone.a", "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("scene", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("frameIndex").GetInt32());
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "CliFixtures", name);
}
