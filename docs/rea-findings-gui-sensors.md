# REA Findings — Grand Piece.exe (GUI & Sensor Verification)

Target: `C:\Users\Asus\AppData\Local\Astro Launcher\Macros\grand-piece\Grand Piece.exe`
SHA256: `001197AD15B329076CAD507ABFC9BC4BB3F47BF2EE3D6AC3AF3D819E21896618`
Format: PE32+ x86_64, .NET NativeAOT ("rtr-9.1-x64-pe-read-only-v3"), Avalonia 11
Assembly name: `AstroGpoTrickOrTreat` (product "GPO Halloween" v1.0.0.0)
Pass date: 2026-11-01. Evidence IDs from REA 6.4.0 + Ghidra 12.1.4.

## TL;DR — koreksi penting untuk SupaGPO

**Grand Piece.exe TIDAK mencari elemen GUI berdasarkan nama.** String
`"PlayerGui"`, `"FindFirstChild"`, `"reel"`, `"SpawnNotices"` = **0 kemunculan**
di seluruh binary (ASCII maupun UTF-16, semua section; bounded scan).
Deteksi reel bersifat **visual/pixel-based** (`PerfectVisualReelController`,
`VisualReelMetrics`, log "Covered image accepted as blue bait"). Pembacaan
memori memakai **offset eksternal** (file bersama dari Astro Launcher via env
`ASTRO_SHARED_OFFSETS_PATH`), bukan tree-walk by-name. Berarti asumisi
`RobloxMemoryReader` + nama `reel`/`Bait`/`Fruits` di `RobloxFishingSensors`
**tidak meniru desain asli** — asli = offsets + visual + parsing teks.

## Evidence ledger

### Observasi (statis, langsung terlihat di binary)

| # | Temuan | Evidence |
|---|--------|----------|
| 1 | Binary overview: 7.250 prosedur, 32.886 string, segment `.rdata` 62 MB (embedded map JSON `fruit_jacko` dll.), `.text` hanya 463 KB | `ev_49964643…ae23` (binary_overview) |
| 2 | Metadata method names (UTF-8 blob refleksi NativeAOT): `GetDataModel`, `GetLocalPlayer`, `ReadPlayerUserId`, `ReadSpawnNotices`, `ReadFishing`, `ReadFishingFruits`, `ReadFishingBaitCount`, `ReadPreferredFishingBait`, `ReadFruitStoreButton`, `ReadCommonBaitCount`, `ReadBaitPurchasePrompt`, `ReadBaitQuantityPrompt`, `ReadBaitDialogueText`, `HasBaitPurchasePrompt`, `ParseBaitCount`, `SampleBaitQuantity`, `BaitQuantityVerified`, `ReadBasket`, `ReadCraftNavigation`, `ChestStatus`, `LastChestRemoved`, `ChestScreenOpen`, `ChestControl`, `ChestSelection`, `SelectedChestEquip`, `ChestAccept`, `ChestResult`, `FishingRodInSlot`, `FishingFinished`, `ReadName` | `ev_81810af2…ad290` (search_strings), `ev_a8cc7c26…d787` |
| 3 | Visual reel: `PerfectVisualReelController.PerfectVisualReelTuning`, `VisualReelDecision`, `VisualReelMetrics`, `ReadName`, state `FishingCameraPose`, `RestoreFishingCamera`, `FishingRecovery`/`FishingDiagnostics`, `FishingVisibility` | string scan ASCII (0x15C24C5, 0x159B519, 0x1568638) |
| 4 | **Absen**: `PlayerGui`, `FindFirstChild` = 0 hits; `reel` hanya di embedded map JSON (0x2E3Exxx, bukan literal kode); `DataModel`/`LocalPlayer` hanya di blob nama method | bounded ASCII+UTF-16 scan semua section |
| 5 | Offsets eksternal: env `ASTRO_SHARED_OFFSETS_PATH`, "Shared offsets file is damaged. Open Astro Launcher to refresh offsets, then restart the macro", "Offsets unavailable; sign in and stay connected to receive offsets", "Roblox version changed - update shared offsets through Astro Launcher and restart the macro", parser `System.Text.Json` | UTF-16 scan (0x1C7A5A0, 0x1CCD456, 0x1CBB814, 0x1CC7F78) |
| 6 | Proses target: `RobloxPlayerBet…` (file 0x1CC8035); "Could not open Roblox for memory reads. Run the client as x64 and, if needed, as administrator." (0x1C913BB) | UTF-16 scan |
| 7 | Import: `OpenProcess` (thunk 0x14006c128, 1 pemanggil), `GetProcAddress`, `LoadLibraryExW`, `VirtualProtect`, `GetModuleHandleW`; **tanpa** `ReadProcessMemory`/`WriteProcessMemory` statis (resolusi dinamis/syscall) | `ev_be1f159f…0cfc`, `ev_b2bed9cc…ee0` (xrefs), search_procedures |
| 8 | Pemanggil `OpenProcess` (0x14007747e) = wrapper runtime `SafeProcessHandle` (bukan app code); pemanggil `GetProcAddress` = stub P/Invoke runtime | `ev_f97ab8d0…3726`, `ev_1645e630…c990` (analyze_function) |
| 9 | Spawn notice diparsing dari teks sistem: template "A fruit has spawned at Tow…" (frozen string, verified header len=0x1B @ VA 0x141CDC6F2) | `ev_3e3ac1d9…d46b` (read_bytes) + file offset 0x1C790FE |
| 10 | Settings keys (record ToString): `AutoOpenChests`, `AutoStoreChestFruits`, `FruitRefillAmount`-style `BaitRefillAmount`, `FruitSpawnNotifier`, `SpawnFruits`, `StartOffset`, `EndOffset`, `Offset` | UTF-16 scan (0x1C6EC1E dst.) |
| 11 | Fitur lain dari UI text: "Move cursor over your fishing cast position", `F3 stop`, `Auto Craft`, "Craft more bait type", "Uses Legendary bait first, then Common bait", "Auto buy leg bait (Trading Hub)", chest batch + "After the full chest batch, store newly received fruits before resuming farming" | UTF-16 scan |
| 12 | Auth gate Discord: "Connect Discord to open GPO Halloween…", "ASTRO AUTHORIZATION", `PaymentObserve` | UTF-16 scan (0x1C8E9DC, 0x1CBFDCD) |

### Inferensi (dari observasi, bukan dilihat langsung)

- **I1.** Karena tidak ada nama GUI apa pun di binary namun ada `GetDataModel`/`GetLocalPlayer`/`Read*`, sensor memori asli bekerja murni lewat **offset numerik** dari shared file (dengan version check) — bukan lookup nama instance. Ini konsisten dengan heritage SupaFisch (offset provider yang sama: Astro Launcher).
- **I2.** Reel trigger dideteksi dari **piksel layar** (kemungkinan pola/bar "reel" via `PerfectVisualReelTuning`), didukung log pixel-acceptance ("Covered image accepted as blue bait") dan total absennya string reel di kode.
- **I3.** Spawn notices dibaca sebagai **teks** (chat/system feed) lalu di-parse dengan template "A fruit has spawned at …", bukan via elemen GUI bernama `SpawnNotices`.
- **I4.** Static→string xref tidak material di Ghidra karena format rtr rel32 + segment `hydrated` (slot statis di-hydrate saat startup); karena itu pencarian fungsi via string literal mentah membuahkan hasil nol — keterbatasan analisis, bukan bukti ketiadaan pemakaian.

### Unknown / belum tertutup (butuh runtime observation)

- U1: Pseudocode fungsi `ReadSpawnNotices`/`ReadFishingFruits` konkret (tidak bisa dilokalisasi statis; perlu capture runtime atau analisis manual Ghidra GUI).
- U2: Format persis shared offsets file (kemungkinan JSON sama dengan `offsets.json` SupaFisch — perlu intercept file/env saat launcher menulis).
- U3: Apakah pembacaan memori memakai `NtReadVirtualMemory` hasil `GetProcAddress` (scan dinamis saat runtime bisa menjawab).
- U4: Parameter tuning `PerfectVisualReelTuning` (nilai threshold pixel).

## Dampak ke SupaGPO

1. `RobloxFishingSensors` yang mencari `"reel"`, `"Bait"`, `"Fruits"`, `"SpawnNotices"` di PlayerGui **tidak akan menemukan apa pun pada desain asli** — nama itu tebakan dari pass REA pertama dan kini terbukti bukan mekanisme asli.
2. Dua desain yang jujur meniru asli:
   - a) **Offsets-driven flat read** (nilai numerik dari offsets.json — sudah terdownload otomatis, 10 entries) ditambah **version check** seperti asli.
   - b) **Visual reel detection** (capture layar + template matching) untuk reel/bait-indicator, menggantikan `IsReelVisible` berbasis memory.
   - Spawn notices: parse teks system/chat feed Roblox, bukan elemen bernama.
3. Mekanisme distribusi offsets SupaGPO (auto-download provider URL) sudah setara fungsional dengan `ASTRO_SHARED_OFFSETS_PATH` milik asli — pertahankan.

## Applied 2026-11-01 (sensor rework tahap 1–3)

* `OffsetsSnapshot`: parse wrapper provider (properti `"Offsets"` = JSON string ter-escape
  di-flatten ke kunci `Group.Name`), expose `RobloxVersion`/`HasVersionInfo`/
  `MatchesRobloxVersion`, alias warisan (`Children`→`Instance.ChildrenStart`, dll.).
* `RobloxMemoryReader`: flat reads `TryReadModuleByte/Int32/Float/Pointer` di
  `exeBase + offset key` (jalur utama sensor, sesuai desain asli);
  `GetRobloxVersion()` dari nama folder install `version-…`; setter `OffsetsSnapshot`
  agar reload benar-benar tertukar di reader.
* `RobloxFishingSensors`: setiap sensor GPO mencoba flat read dulu (`Fishing.*`, `Loot.*`),
  fallback tree-walk by-name warisan tetap ada. Kunci GPO-spesifik tidak ada di dump
  publik 392-offset sehingga jalur flat inert & degradasi graceful. Kamera tetap
  tree-walk (offset per-instance, bukan static).
* Version check: `RuntimeHost.VersionStatus` (log + status Offsets page + peringatan di
  Overview bila mismatch), pola pesan meniru asli ("Roblox version changed - update offsets!").
* Tests: 13 test baru (`OffsetsWrapperTests`), total **37/37 hijau**; build + smoke test OK.

## Applied 2026-11-01 (tahap 4: visual reel detection)

Meniru `PerfectVisualReelController`/`VisualReelMetrics` asli (deteksi piksel, bukan nama GUI):

* Core `Visual/`: `IReelFrameSource` (seam), `GdiScreenFrameSource` (GDI BitBlt+GetDIBits →
  grayscale, tanpa dependency System.Drawing), `VisualReelDetector` (template matching:
  similarity = 1 − mean|Δ|/255, threshold %), `ReelTemplateStore` (binari `SGRT` +
  version + w/h + pixel di `%APPDATA%\SupaGPO\reel-template.bin`), `VisualReelProbe`
  (cache capture 100 ms, hasil `bool?` = null → fallback memori).
* `RobloxFishingSensors`: probe visual true selalu menang untuk `ReelActive`; null/false
  jatuh ke jalur memori (flat → tree-walk fallback). Verifikasi nyata menunggu runtime.
* Settings `FishingSettings`: `VisualReel`, `ReelRegionX/Y/W/H`, `ReelMatchThresholdPct`.
* UI halaman Fishing: enable checkbox, region X/Y/W/H, threshold, tombol **Calibrate**
  (tangkap region sebagai template) + **Test match** (similarity vs threshold), status.
* Wiring `RuntimeHost`: probe + template store + `CalibrateVisualReel()`/`TestVisualReel()`.
* Tests: 21 test baru (`VisualReelTests`), total **58/58 hijau**; build + smoke test OK.

## Applied 2026-11-01 (tahap 5: spawn-notice parser feed teks)

Meniru asli (REA I3): notice di-parse dari **teks feed**, bukan elemen GUI bernama —
template frozen string "A fruit has spawned at …" (VA 0x141CDC6F2, terverifikasi):

* Core `Fishing/SpawnNoticeParser.cs`: `ParseLine` (template case-insensitive, lokasi
  di-strip tanda baca, varian tanpa lokasi ditoleransi, baris tak dikenal lewat apa adanya)
  + `ParseBatch` (split `|`/newline, output kanonik "Fruit spawned at X").
* Sensor `ParseNotices`: buffer UTF-16 feed → `SpawnNoticeParser.ParseBatch` (buffer
  256 → 1024 byte). Konsumen tetap `string[]` (FishingState), bentuk tak berubah.
* Tests: 10 test baru (`SpawnNoticeParserTests`), total **68/68 hijau**; smoke test OK.

## Applied 2026-11-01 (UI: design system Of* SupaFisch/SupaZero)

Port penuh design system Of* dari SupaFisch/SupaZero (identik di kedua repo itu)
ke SupaGPO — pure XAML, tanpa menyentuh logic:

* `App.axaml`: palet brush Of* (OfBg/OfCard/OfBorder/OfAccent/…), override brush
  Fluent (toggle/checkbox accent netral), ~30 style selector (`Border.card/row/
  strip/badge/sep`, `TextBlock.h1/h2/label/muted/caps/mono/warn/error`,
  `Button.accent` dengan template states, `ToggleSwitch` tanpa teks On/Off,
  `ListBox.tabs` untuk tab strip).
* `MainWindow`: pola SupaFisch — title bar + badge brand "G", **tab strip atas**
  (ListBox.tabs, 7 halaman, ganti sidebar), strip status koneksi (Roblox attached
  + hasil version check), header halaman dengan indikator Running/Stopped,
  konten di ScrollViewer.
* `MainWindowViewModel`: model Tabs (`TabViewModel` title+content) +
  `IPageViewModel.Refresh()`; ~7 view di-rewrite ke class baru; 3 tombol
  placeholder diperbaiki (Attach/Detach/ToggleFishing, Test Webhook, Clear).
* **Fix bug lama**: `Refresh()` di VM halaman tak pernah dipanggil → status UI
  diam; kini `DispatcherTimer` 250 ms di code-behind memanggil
  `MainWindowViewModel.RefreshCurrent()` (~4 Hz, hanya tab aktif).
* Verifikasi: build 0 error/warning, **68/68 test hijau**, screenshot smoke test
  (`supagpo-ui4.png`) memverifikasi dark theme + tab strip berfungsi.
  Perhatian: `MainWindowViewModel.ConnectionText` membaca `App.Host` (public).

## Applied 2026-11-02 (sensor fallback diperbaiki — pola SupaZero)

"Offsets tidak terdeteksi" ternyata **bukan masalah download**: provider hidup,
versi cocok (`version-5a21b87bbc2641cf`), parse flatten = 393 kunci. Akar masalah:

1. **Sensor fallback cacat**: setelah tree-walk menemukan objek GUI by-name,
   nilai dibaca lewat kunci GPO-spesifik (`Fishing.ReelFlag` dsb.) yang memang
   tidak ada di dump publik → fallback selalu gagal.
2. **Bug `DecodeUtf16`** (reader): loop berhenti saat byte tinggi = 0 — untuk
   teks UTF-16LE ASCII itu selalu karakter pertama → **semua nama string terbaca
   kosong**, tree-walk by-name tak pernah bisa bekerja sama sekali.
3. **Alias `StringLength` → `Misc.StringLength` hilang** (ada di SupaZero,
   belum diport) → panjang string salah.

Perbaikan (meniru cara SupaZero yang terbukti berhasil):

* **Fallback tree-walk membaca via offset engine dump nyata**: objek ditemukan
  by-name di PlayerGui (DataModel → "Players" → LocalPlayer → PlayerGui),
  nilai via `GuiObject.Visible` (reel, loot flags), `GuiObject.Text` (bait →
  parse angka, spawn notices → parser template, fruits → split daftar,
  preferred/last chest), kamera via rantai `DataModel.Workspace` →
  `Workspace.CurrentCamera` → `Camera.Position`. Jalur flat `Fishing.*/Loot.*`
  tetap ada (aktif bila provider Astro mengirimkannya).
* **Nama objek GUI dikonfigurasi**: `GuiSensorSettings` (defaults: "reel",
  "Bait", "SpawnNotices", dll.) di `AppSettings.GuiSensors`; dikalibrasi saat
  runtime observation.
* **Default provider diganti ke imtheo** (`offsets.imtheo.lol/offsets.json`,
  format dumper langsung seperti SupaZero); wrapper GitHub Astro tetap didukung
  sebagai `LegacyProviderUrl`.
* Reader baru: `ReadWorkspace`, `ReadCurrentCamera`, `TryReadGuiVisible`,
  `ReadGuiText`, `AttachForTests` (hook test); `RawMemory.ReadOverride` +
  `InternalsVisibleTo` untuk address space palsu di test.
* Tests: 9 test baru `SensorsTreeFallbackTests` (fake memory world lengkap:
  DataModel→Players→PlayerGui→GUI objects + Workspace→Camera), total
  **77/77 hijau**; build 0 error/warning.
* Catatan: nama GUI GPO yang sebenarnya baru final setelah runtime observation
  (Roblox jalan) — mekanisme sudah benar, nama dikalibrasi via settings.

## Update 2026-11-02 - Realignment + fix koneksi

- Bug attach: RuntimeHost.AttachRoblox hanya memanggil ProcessAttacher; RobloxMemoryReader tidak pernah di-attach (semua read mati, version unknown). Fix: attach/detach lewat reader; versi via QueryFullProcessImageName (tahan Fishstrap).
- Fallback tree-walk by-name dihapus total (bukan desain Grand Piece.exe): helper GUI reader, GuiSensorSettings, SensorsTreeFallbackTests.
- Loot flags (AutoOpenChests/AutoStoreFruits) = settings macro dari Settings.Loot (evidence 10), bukan sensor memori.
- Macro baru GpoFishingMacro: cast klik singkat, tunggu bite (timeout recast + self-heal equip tiap timeout genap), reel via deteksi piksel (ReelBarAnalyzer: pasangan bar kiri biru/kanan hijau; ReelController bang-bang + belajar arah), cooldown, ulangi. Hotkey default F3.
- UI 3 tab (Halloween/Fishing/Webhooks) meniru layout asli; sub-tab General + Auto buy leg bait; tombol Start fishing F3; timer + penghitung minigame; badge CONNECTED. Warna tetap Of*.
- Test: 78/78 hijau (ReelBarAnalyzerTests frame sintetis, GpoFishingMacroTests state machine).
- Belum diuji live di GPO (deteksi piksel butuh capture beruntun saat memancing).
