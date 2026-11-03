namespace SupaGPO.Core.Visual;

/// <summary>
/// Kontrol tahan/lepas klik agar AREA AMAN menutupi garis putih (ikan) di bar kiri.
/// Mekanik GPO: zona biru + celah aman bergeser saat klik ditahan/dilepas; garis putih
/// bergerak sendiri. Nilai yang dikendalikan = tengah celah aman; target = posisi garis
/// putih (diprediksi ±LeadMs karena ikan bergerak). Bang-bang + hysteresis 2 px.
/// Arah gerak "tahan" dipelajari dari gerak celah aman: bila posisi bergerak berlawanan
/// dengan asumsi selama ≥2 frame dengan state sama, vote berbalik.
/// </summary>
public sealed class ReelController
{
    public const double LeadMs = 70;
    private const int StableFrames = 2;
    private const int MaxVotes = 12;

    private double _lastZoneY = double.NaN;
    private double _lastFishY = double.NaN;
    private long _lastT;
    private double _zoneVel;
    private double _fishVel;
    private bool _hold;
    private int _sameState;
    private int _votes;
    private bool _locked; // arah terkunci setelah cukup bukti; tidak di-reset antar minigame

    /// <summary>true = tahan klik menggeser area aman ke atas (default sampai terbukti sebaliknya).</summary>
    public bool HoldMovesUp => _votes >= 0;

    /// <summary>Reset per minigame; hasil belajar arah dipertahankan.</summary>
    public void Reset()
    {
        _lastZoneY = double.NaN;
        _lastFishY = double.NaN;
        _lastT = 0;
        _zoneVel = 0;
        _fishVel = 0;
        _hold = false;
        _sameState = 0;
        // _votes & _locked sengaja dipertahankan: arah gerak klik bersifat perangkat-per sesi.
    }

    /// <param name="zoneY">Tengah area aman (benda yang digerakkan klik).</param>
    /// <param name="fishY">Posisi garis putih (target, bergerak sendiri).</param>
    public bool Update(int? zoneY, int? fishY, long nowMs)
    {
        if (zoneY is not int y || fishY is not int t)
        {
            _lastZoneY = double.NaN;
            _lastFishY = double.NaN;
            _hold = false;
            _sameState = 0;
            return false;
        }

        if (!double.IsNaN(_lastZoneY) && nowMs > _lastT)
        {
            double dt = nowMs - _lastT;
            double dz = y - _lastZoneY;
            double df = t - _lastFishY;
            _zoneVel = 0.6 * _zoneVel + 0.4 * (dz / dt);
            _fishVel = 0.6 * _fishVel + 0.4 * (df / dt);
            if (!_locked && _sameState >= StableFrames && Math.Abs(dz) >= 1)
            {
                bool movedUp = dz < 0;
                _votes = Math.Clamp(_votes + (_hold == movedUp ? 1 : -1), -MaxVotes, MaxVotes);
                if (Math.Abs(_votes) >= 5) _locked = true;
            }
        }

        double fishAhead = t + _fishVel * LeadMs;   // kejar ikan ke posisi mendatang
        double err = y + _zoneVel * LeadMs - fishAhead; // > 0: area aman di bawah ikan → perlu naik
        bool hold = _hold;
        if (Math.Abs(err) > 2)
        {
            bool needUp = err > 0;
            hold = HoldMovesUp ? needUp : !needUp;
        }

        _sameState = hold == _hold ? _sameState + 1 : 0;
        _hold = hold;
        _lastZoneY = y;
        _lastFishY = t;
        _lastT = nowMs;
        return hold;
    }
}
