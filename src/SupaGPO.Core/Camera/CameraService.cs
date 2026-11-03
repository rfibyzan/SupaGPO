namespace SupaGPO.Core.Camera;

/// <summary>
/// Watches the Roblox camera position; records movement so automation can notice if the game
/// yanks the camera during casting. Mirrors CaptureFishingCamera / RestoreFishingCamera.
/// </summary>
public sealed class CameraService
{
    private (float X, float Y, float Z)? _last;

    public void Observe(float x, float y, float z)
    {
        if (_last is { } l && l.X == x && l.Y == y && l.Z == z) return;
        _last = (x, y, z);
    }

    public (float X, float Y, float Z)? Last => _last;
}
