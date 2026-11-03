using SupaGPO.Core.Input;
using SupaGPO.Core.Roblox;

namespace SupaGPO.Core.Fishing;

/// <summary>
/// Walk-to-target using camera-relative 8-direction movement (W/A/S/D).
/// Based on SupaZero Navigator.KeysToward — Roblox movement is camera-relative.
/// </summary>
public sealed class GpoNavigator
{
    private readonly IInput _input;
    private readonly GpoCharacterReader _reader;

    // Target and state
    private (double X, double Z) _target;
    private double _arriveRadius = 4.0;
    private int _stuckCount;
    private (double X, double Z) _lastPos;
    private long _lastMoveCheck;
    private readonly HashSet<ushort> _activeKeys = new();

    public GpoNavigator(IInput input, GpoCharacterReader reader)
    {
        _input = input;
        _reader = reader;
    }

    /// <summary>Start walking to a world position (X, Z).</summary>
    public void WalkTo(double targetX, double targetZ, double arriveRadius = 4.0)
    {
        _target = (targetX, targetZ);
        _arriveRadius = arriveRadius;
        _stuckCount = 0;
        _lastPos = (0, 0);
        _lastMoveCheck = 0;
    }

    /// <summary>Call every tick. Returns true when arrived, false while walking.</summary>
    public bool Update(long nowMs)
    {
        _reader.Refresh();
        if (!_reader.HasData) return false;

        var pos = _reader.Position!.Value;
        var look = _reader.CameraLook ?? (0, -1); // fallback: camera facing -Z

        double dx = _target.X - pos.X;
        double dz = _target.Z - pos.Z;
        double dist = Math.Sqrt(dx * dx + dz * dz);

        if (dist <= _arriveRadius)
        {
            ReleaseAll();
            return true; // Arrived
        }

        // Stuck detection: if position hasn't changed for 2 seconds
        if (nowMs - _lastMoveCheck > 2000)
        {
            double moved = Math.Sqrt((pos.X - _lastPos.X) * (pos.X - _lastPos.X) +
                                     (pos.Z - _lastPos.Z) * (pos.Z - _lastPos.Z));
            if (moved < 0.5) // less than 0.5 studs in 2 seconds = stuck
            {
                _stuckCount++;
                if (_stuckCount >= 3)
                {
                    ReleaseAll();
                    return true; // Give up, treat as "arrived" to avoid infinite loop
                }
                // Try jumping to unstuck
                _input.TapKey(0x20); // Space
            }
            _lastPos = (pos.X, pos.Z);
            _lastMoveCheck = nowMs;
        }

        // Calculate keys to hold (camera-relative)
        double fx = look.X, fz = look.Z;
        double rx = -fz, rz = fx; // Right vector

        double forward = (dx * fx + dz * fz) / dist;
        double right = (dx * rx + dz * rz) / dist;

        const double Threshold = 0.38; // sin(22.5°)

        bool wantW = forward > Threshold;
        bool wantS = forward < -Threshold;
        bool wantD = right > Threshold;
        bool wantA = right < -Threshold;

        // Press/release keys as needed
        SetKey(0x57, wantW); // W
        SetKey(0x53, wantS); // S
        SetKey(0x44, wantD); // D
        SetKey(0x41, wantA); // A

        return false; // Still walking
    }

    private void SetKey(ushort vk, bool down)
    {
        if (down)
        {
            if (!_activeKeys.Contains(vk))
            {
                _input.KeyDown(vk);
                _activeKeys.Add(vk);
            }
        }
        else
        {
            if (_activeKeys.Contains(vk))
            {
                _input.KeyUp(vk);
                _activeKeys.Remove(vk);
            }
        }
    }

    /// <summary>Release all movement keys.</summary>
    public void ReleaseAll()
    {
        foreach (ushort vk in _activeKeys.ToArray())
        {
            _input.KeyUp(vk);
        }
        _activeKeys.Clear();
    }

    /// <summary>Stop walking immediately.</summary>
    public void Stop()
    {
        ReleaseAll();
        _target = (0, 0);
    }
}