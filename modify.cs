using System;
using System.IO;
using System.Text;

string path = @"D:\Kodingan\GitHub\SupaGPO\src\SupaGPO.Presentation\RuntimeHost.cs";
string content = File.ReadAllText(path);
int pos = content.LastIndexOf("public void Dispose()");
if (pos >= 0) {
    string before = content.Substring(0, pos);
    string after = content.Substring(pos);
    string method = @"
    /// <summary>
    /// Test beli bait manual: jalan ke nearest shop -> E -> dialog -> buy -> balik ke spot.
    /// Hanya untuk debugging, tidak mempengaruhi macro utama.
    /// </summary>
    public async Task TestBaitRefillAsync()
    {
        if (!IsAttached) { AppLog.Line(\"TEST BAIT: Roblox not attached\"); return; }
        if (_macro.Running) { AppLog.Line(\"TEST BAIT: Macro is running, stop first\"); return; }
        var s = Settings.Fishing;
        if (!s.AutoRefillBait) { AppLog.Line(\"TEST BAIT: AutoRefillBait is disabled\"); return; }
        var debug = new ReelDebugDumper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), \"SupaGPO\", \"debug\"));
        var controller = new BaitRefillController(_input, _screen, () => s, _characterReader, _navigator, debug);
        long now = Environment.TickCount64; controller.Start(now);
        _characterReader.Refresh();
        var startPos = _characterReader.HasData ? (_characterReader.Position!.Value.X, _characterReader.Position!.Value.Z) : (0.0, 0.0);
        AppLog.Line($\"TEST BAIT: Start at ({startPos.X:0},{startPos.Z:0})\");
        bool finished = false; int stepCount = 0;
        while (!finished && stepCount < 2000)
        {
            now = Environment.TickCount64;
            PixelRect? rect = _screen.GetClientRect(ProcessId);
            if (rect is not PixelRect r) { await Task.Delay(50); continue; }
            var (sleep, status) = controller.Step(now, r);
            if (stepCount % 20 == 0) AppLog.Line($\"TEST BAIT: {controller.State} - {status}\");
            if (controller.Finished)
            {
                finished = true;
                if (controller.FullBait) AppLog.Line(\"TEST BAIT: Bait penuh (teks merah), backoff\");
                else if (controller.Failed) AppLog.Line(\"TEST BAIT: Gagal (timeout/tidak ada dialog)\");
                else AppLog.Line(\"TEST BAIT: Selesai berhasil\");
            }
            await Task.Delay(Math.Max(10, sleep)); stepCount++;
        }
        if (!finished) AppLog.Line(\"TEST BAIT: Timeout keseluruhan\");
        _navigator.ReleaseAll(); debug.Dispose();
    }

";
    File.WriteAllText(path, before + method + after);
    Console.WriteLine("Done");
}