using SupaGPO.Core.Input;

namespace SupaGPO.Core.Fishing;

/// <summary>
/// Shake assist: when a shake prompt appears, tap Enter and briefly hold LMB.
/// Simplified port of heritage ShakeAssistant (25 ms assist, gate ScreenGui).
/// </summary>
public sealed class ShakeAssistant
{
    public bool AssistEnabled { get; set; } = true;
    public bool SendShakeInput { get; set; } = true;

    public long LastPromptSeen { get; private set; }

    public Action? OnPromptEnter;
    public Action? OnAssistOff;

    public FishingStatus? Tick(bool shakePromptVisible, IInput input, Func<long> clock)
    {
        if (!AssistEnabled) return null;

        long now = clock();

        if (shakePromptVisible)
        {
            LastPromptSeen = now;
            OnPromptEnter?.Invoke();
            if (SendShakeInput) input.TapKey(0x0D); // Enter
            return new FishingStatus(FishingTexts.CategoryCasting, "Shake assist active.", FlagA: true);
        }

        if (LastPromptSeen != 0 && (now - LastPromptSeen) > 250)
        {
            LastPromptSeen = 0;
            OnAssistOff?.Invoke();
            return new FishingStatus(FishingTexts.CategoryCasting, "Shake assist off.", FlagA: true);
        }
        return null;
    }
}
