using System;
using System.IO;
using System.Text;

class Program {
    static void Main() {
        string path = @\"D:\\Kodingan\\GitHub\\SupaGPO\\src\\SupaGPO.Presentation\\RuntimeHost.cs\";
        string content = File.ReadAllText(path);
        int pollStart = content.IndexOf(\"    private void PollHotkey()\");
        int nextMethod = content.IndexOf(\"    private volatile bool _hotkeyWasDown;\", pollStart);
        if (pollStart >= 0 && nextMethod > pollStart) {
            string before = content.Substring(0, pollStart);
            string after = content.Substring(nextMethod);
            string newPoll = @\"
    private void PollHotkey()
    {
        if (HotkeyCaptureActive) { _hotkeyWasDown = _hotkey.IsHotkeyPressed(); return; }

        bool down = _hotkey.IsHotkeyPressed();
        if (down && !_hotkeyWasDown)
        {
            Toggle();
            AppLog.Line(\$\\\"Hotkey toggled -> fishing={_macro.Running}\\\");
        }
        _hotkeyWasDown = down;

        if (HotkeyService.IsVkDown(0x73))
        {
            if (!_macro.Running)
            {
                _ = Task.Run(async () => await TestBaitRefillAsync());
            }
            else
            {
                AppLog.Line(\\\"TEST BAIT: Cannot test while macro running (press F3 to stop first)\\\");
            }
            System.Threading.Thread.Sleep(300);
        }

        if (HotkeyService.IsVkDown(0x74))
        {
            _characterReader.Refresh();
            if (_characterReader.HasData)
            {
                var pos = _characterReader.Position!.Value;
                var look = _characterReader.CameraLook ?? (0.0, 0.0);
                AppLog.Line(\$\\\"DEBUG: Pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) Look=({look.Item1:F2},{look.Item2:F2})\\\");
            }
            else
            {
                AppLog.Line(\\\"DEBUG: No character data\\\");
            }
            System.Threading.Thread.Sleep(300);
        }
    }

\";
            File.WriteAllText(path, before + newPoll + after);
            Console.WriteLine(\"Fixed\");
        }
        else {
            Console.WriteLine(\"Not found\");
        }
    }
}