using Vograph.Core.Services;
using Vograph.Core.Models;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
string tempXml = @"C:\Users\NiLle\AppData\Local\Temp\opencode\TimetableGroup50.xml";
string baseDir = Path.Combine(Path.GetTempPath(), "vograph_sync_test");
Directory.CreateDirectory(baseDir);
string dbA = Path.Combine(baseDir, "A_vograph.db");
string dbB = Path.Combine(baseDir, "B_vograph.db");
foreach (var f in Directory.GetFiles(baseDir)) File.Delete(f);
Console.WriteLine($"Test dir {baseDir}");

string xml = File.ReadAllText(tempXml, Encoding.UTF8);

// Machine A
using (var dbAConn = new Database(dbA))
{
    var parserA = new ParserService(dbAConn);
    await parserA.RefreshAsync(xmlOverride: xml);
    var settingsA = dbAConn.GetSettings();
    settingsA.MyGroupId = "3313";
    dbAConn.SaveSettings(settingsA);
    // Add overrides and homework on A
    var ovA = new OverrideService(dbAConn);
    ovA.AddOrUpdate("лек ВЫСШ. МАТЕМАТ", "global", "МатАн А", "note A");
    ovA.AddOrUpdate("пр ОСН РОС ГОС", "weekday:1", "ОРГ А Monday", null);
    var hwA = new HomeworkService(dbAConn);
    long id1 = hwA.AddHomework("лек ВЫСШ. МАТЕМАТ", "ДЗ А задачи", 2, new DateTime(2026,9,2));
    long id2 = hwA.AddHomework("лек ИСТОРИЯ", "ДЗ А история", 1, new DateTime(2026,9,2));
    // Add friend
    dbAConn.InsertFriend(new FriendGroup { GroupName = "09С33", ColorHex = "#FF6CA5E0", Enabled = true });
    var sA = dbAConn.GetSettings();
    sA.NotifyTime1 = "20:00"; sA.NotifyTime2 = "07:30"; sA.IntersectionStrictness = 73;
    dbAConn.SaveSettings(sA);

    Console.WriteLine($"Machine A: overrides {dbAConn.GetOverrides().Count}, hw {hwA.GetAll().Count}, friends {dbAConn.GetFriends().Count}");
    foreach (var ov in dbAConn.GetOverrides()) Console.WriteLine($"  OV {ov.SubjectRawNormalized} {ov.Scope} -> {ov.DisplayName}");
    foreach (var hw in hwA.GetAll()) Console.WriteLine($"  HW {hw.SubjectRawNormalized} {hw.Text} due {hw.DueDateComputed:yyyy-MM-dd} status {hw.Status}");

    // Export
    var syncA = new SyncService(dbAConn);
    string exportPath = Path.Combine(baseDir, $"vograph-sync-{DateTime.Now:yyyyMMdd}.json");
    syncA.ExportToFile(exportPath);
    Console.WriteLine($"Exported to {exportPath} {new FileInfo(exportPath).Length} bytes");
    string json = File.ReadAllText(exportPath, Encoding.UTF8);
    Console.WriteLine($"JSON preview {json.Substring(0, Math.Min(500, json.Length))}");
    // QR
    string qrContent = syncA.GenerateQrContent(json);
    Console.WriteLine($"QR content length {qrContent.Length} preview {qrContent.Substring(0, Math.Min(100, qrContent.Length))}");
    string qrPath = Path.Combine(baseDir, "qr.png");
    syncA.SaveQrImage(qrContent, qrPath);
    Console.WriteLine($"QR saved {qrPath} {new FileInfo(qrPath).Length} bytes");

    // Also test HTTP host/client simulation: start host on A, join from B via http
    // For verification without needing two machines, we simulate by exporting json and importing via http
}

// Machine B starts empty, imports from A's export
using (var dbBConn = new Database(dbB))
{
    var parserB = new ParserService(dbBConn);
    await parserB.RefreshAsync(xmlOverride: xml);
    Console.WriteLine($"\nMachine B before import: overrides {dbBConn.GetOverrides().Count}, hw {new HomeworkService(dbBConn).GetAll().Count}, friends {dbBConn.GetFriends().Count}");
    var syncB = new SyncService(dbBConn);
    string importPath = Directory.GetFiles(baseDir, "vograph-sync-*.json").First();
    syncB.ImportFromFile(importPath);
    Console.WriteLine($"Machine B after import: overrides {dbBConn.GetOverrides().Count}, hw {new HomeworkService(dbBConn).GetAll().Count}, friends {dbBConn.GetFriends().Count}");
    foreach (var ov in dbBConn.GetOverrides()) Console.WriteLine($"  OV B {ov.SubjectRawNormalized} {ov.Scope} -> {ov.DisplayName}");
    foreach (var hw in new HomeworkService(dbBConn).GetAll()) Console.WriteLine($"  HW B {hw.SubjectRawNormalized} {hw.Text} due {hw.DueDateComputed:yyyy-MM-dd}");

    // Verify same as A
    using var dbAConn2 = new Database(dbA);
    var ovA2 = dbAConn2.GetOverrides();
    var ovB = dbBConn.GetOverrides();
    var hwA2 = new HomeworkService(dbAConn2).GetAll();
    var hwB = new HomeworkService(dbBConn).GetAll();
    bool ovEqual = ovA2.Count == ovB.Count && ovA2.All(a => ovB.Any(b => b.SubjectRawNormalized == a.SubjectRawNormalized && b.Scope == a.Scope && b.DisplayName == a.DisplayName));
    bool hwEqual = hwA2.Count == hwB.Count && hwA2.All(a => hwB.Any(b => b.SubjectRawNormalized == a.SubjectRawNormalized && b.Text == a.Text));
    bool frEqual = dbAConn2.GetFriends().Count == dbBConn.GetFriends().Count;
    Console.WriteLine($"\nCompare A vs B: ovEqual {ovEqual}, hwEqual {hwEqual}, frEqual {frEqual}");
    if (ovEqual && hwEqual && frEqual) Console.WriteLine("PASS sync file: two machines same overrides+homework, no server used");
    else Console.WriteLine("FAIL sync mismatch");

    // Verify settings strictness etc. also synced?
    var sB = dbBConn.GetSettings();
    Console.WriteLine($"Settings B strictness {sB.IntersectionStrictness} notify {sB.NotifyTime1}/{sB.NotifyTime2} parityInvert {sB.ParityInvert}");

    // Verify lastWriteWins: modify A and B with conflicting override, re-export/import with newer timestamp wins
    Console.WriteLine("\n--- Conflict lastWriteWins ---");
    // Create conflict: same subject/scope but different display name, B newer
    System.Threading.Thread.Sleep(1100);
    var ovServiceB = new OverrideService(dbBConn);
    ovServiceB.AddOrUpdate("лек ВЫСШ. МАТЕМАТ", "global", "МатАн B NEWER", null);
    var syncB2 = new SyncService(dbBConn);
    string exportB = Path.Combine(baseDir, "vograph-sync-B.json");
    syncB2.ExportToFile(exportB);
    // Now import B's newer file into A
    var syncA2 = new SyncService(dbAConn2);
    syncA2.ImportFromFile(exportB);
    var finalOvA = dbAConn2.GetOverrides().First(x => x.SubjectRawNormalized == "лек высш. математ" && x.Scope == "global");
    Console.WriteLine($"After conflict B newer imported to A: display {finalOvA.DisplayName} (expected МатАн B NEWER) {(finalOvA.DisplayName == "МатАн B NEWER" ? "PASS" : "FAIL")}");

    // Also test QR http fallback for large json
    var largeJson = new string('x', 2000);
    var smallJson = "{\"test\":1}";
    var syncTest = new SyncService(dbBConn);
    Console.WriteLine($"\nQR small {syncTest.GenerateQrContent(smallJson).Length} (should be json itself)");
    Console.WriteLine($"QR large {syncTest.GenerateQrContent(largeJson).Substring(0,30)} (should be http://ip:8765/sync#token)");
    Console.WriteLine($"Local IP {syncTest.GetLocalIp()}");

    // Save copy to docs for verification
    Directory.CreateDirectory(@"C:\Users\NiLle\Desktop\projects\vograph\docs\verify_phase5");
    File.Copy(importPath, @"C:\Users\NiLle\Desktop\projects\vograph\docs\verify_phase5\vograph-sync-20260901.json", true);
    File.Copy(Path.Combine(baseDir, "qr.png"), @"C:\Users\NiLle\Desktop\projects\vograph\docs\verify_phase5\qr.png", true);
    // Also copy logs
    File.WriteAllText(@"C:\Users\NiLle\Desktop\projects\vograph\docs\verify_phase5\sync_log.txt", $"A overrides {ovA2.Count} hw {hwA2.Count}\nB after import overrides {ovB.Count} hw {hwB.Count}\nPASS {ovEqual && hwEqual}", Encoding.UTF8);
}

Console.WriteLine("\nPhase5 verification COMPLETE - no server used, LAN file sync only");
