using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

// No Unity state: persistence and interchange can be exercised in isolated temporary directories.
public sealed class SaveRepository
{
    public const int FormatVersion = 1;
    public readonly string Root;
    public readonly List<string> Warnings = new();
    public SaveRepository(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }
    private static string Id(string id) => Guid.TryParseExact(id, "N", out _) ? id : throw new InvalidDataException("无效的存档标识");
    private string Folder(string id) => Path.Combine(Root, Id(id));
    private string Manifest(string id) => Path.Combine(Folder(id), "run.json");
    private string Snapshot(string run, string point) => Path.Combine(Folder(run), Id(point) + ".json");
    private static string Hash(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
    }
    public static void AtomicWrite(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
    private static T Decode<T>(string json) => JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 128 }) ?? throw new InvalidDataException("存档数据为空");
    public List<RunData> List()
    {
        Warnings.Clear();
        var result = new List<RunData>();
        foreach (string folder in Directory.GetDirectories(Root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(folder), "N", out _)) continue;
            if (File.Exists(Path.Combine(folder, "dead"))) continue;
            string path = Path.Combine(folder, "run.json");
            if (!File.Exists(path)) continue;
            try
            {
                var run = Decode<RunData>(File.ReadAllText(path));
                if (run.version != FormatVersion || run.id != Path.GetFileName(folder) || run.snapshots == null) throw new InvalidDataException("索引不匹配");
                NormalizeKinds(run);
                if (!run.dead) result.Add(run);
            }
            catch (Exception e) { Warnings.Add(Path.GetFileName(folder) + ": " + e.Message); }
        }
        return result.OrderByDescending(r => r.lastPlayedUtc).ThenByDescending(r => r.createdUtc).ToList();
    }
    private static void NormalizeKinds(RunData run)
    {
        foreach (var point in run.snapshots) if (point.kind == SaveKind.Exit) point.kind = SaveKind.Auto;
    }
    public void Update(RunData run)
    {
        if (File.Exists(Path.Combine(Folder(run.id), "dead"))) throw new InvalidOperationException("该世界线已结束");
        NormalizeKinds(run);
        AtomicWrite(Manifest(run.id), JsonConvert.SerializeObject(run, Formatting.Indented));
    }
    public void Commit(RunData run, SavePoint point, SavePayload payload, int autoLimit)
    {
        if (run.dead) throw new InvalidOperationException("死亡后不能保存");
        NormalizeKinds(run);
        if (point.kind == SaveKind.Exit) point.kind = SaveKind.Auto;
        string json = JsonConvert.SerializeObject(payload);
        point.checksum = Hash(json);
        AtomicWrite(Snapshot(run.id, point.id), json);
        // The manifest is the commit point. Never remove the previous snapshot before publishing it.
        var previous = run.snapshots;
        var oldLatest = run.latestSnapshotId;
        var next = run.hardcore ? new List<SavePoint>() : new List<SavePoint>(previous);
        next.Add(point);
        var remove = next.Where(s => s.kind == SaveKind.Auto).OrderByDescending(s => s.id == point.id).ThenByDescending(s => s.savedUtc).Skip(Math.Max(1, autoLimit)).ToList();
        next.RemoveAll(remove.Contains);
        run.snapshots = next;
        run.latestSnapshotId = point.id;
        try { Update(run); }
        catch { run.snapshots = previous; run.latestSnapshotId = oldLatest; throw; }
        foreach (var old in previous.Where(s => !next.Any(n => n.id == s.id))) TryRemove(Snapshot(run.id, old.id));
    }
    public SavePayload Read(RunData run, SavePoint point)
    {
        if (!run.IsCompatible) throw new InvalidDataException(SaveDataContract.IncompatibleMessage);
        if (run.dead || File.Exists(Path.Combine(Folder(run.id), "dead"))) throw new InvalidDataException("该世界线已结束");
        if (!run.snapshots.Any(s => s.id == point.id)) throw new InvalidDataException("保存点不属于此世界线");
        if (run.hardcore && point.id != run.latestSnapshotId) throw new InvalidDataException("硬核模式不能回档");
        string json = File.ReadAllText(Snapshot(run.id, point.id));
        if (Hash(json) != point.checksum) throw new InvalidDataException("保存点校验失败，请选择其他记录");
        var data = Decode<SavePayload>(json);
        if (data.worldSchemaVersion != SaveDataContract.WorldSchemaVersion) throw new InvalidDataException(SaveDataContract.IncompatibleMessage);
        if (data.version != FormatVersion || data.files == null) throw new InvalidDataException("不支持此存档版本");
        return data;
    }
    public void DeletePoint(RunData run, SavePoint point)
    {
        if (run.hardcore || run.snapshots.Count <= 1) throw new InvalidOperationException("不能删除最后一个保存点，请删除整个世界线");
        var previous = run.snapshots;
        string latest = run.latestSnapshotId;
        run.snapshots = previous.Where(s => s.id != point.id).ToList();
        if (latest == point.id) run.latestSnapshotId = run.snapshots.OrderByDescending(s => s.savedUtc).First().id;
        try { Update(run); } catch { run.snapshots = previous; run.latestSnapshotId = latest; throw; }
        TryRemove(Snapshot(run.id, point.id));
    }
    public void DeleteRun(RunData run)
    {
        // Tombstone first, including migrated slots: leave the original legacy files untouched.
        AtomicWrite(Path.Combine(Folder(run.id), "dead"), DateTime.UtcNow.ToString("O"));
        run.dead = true;
        foreach (string file in Directory.GetFiles(Folder(run.id)))
            if (Path.GetFileName(file) != "dead" && Path.GetFileName(file) != "legacy-slot") TryRemove(file);
    }
    private void TryRemove(string file) { try { if (File.Exists(file)) File.Delete(file); } catch (IOException e) { Warnings.Add(e.Message); } }
    public bool LegacyImported(int slot) => Directory.GetDirectories(Root).Any(d => File.Exists(Path.Combine(d, "legacy-slot")) && File.ReadAllText(Path.Combine(d, "legacy-slot")) == slot.ToString()) || List().Any(r => r.legacySlot == slot);
    public void MarkLegacy(RunData run, int slot) => AtomicWrite(Path.Combine(Folder(run.id), "legacy-slot"), slot.ToString());

    public void Export(RunData run, string path)
    {
        if (!run.IsCompatible) throw new InvalidDataException(SaveDataContract.IncompatibleMessage);
        var copy = Decode<RunData>(JsonConvert.SerializeObject(run));
        copy.fromHardcore |= copy.hardcore;
        copy.hardcore = false;
        copy.legacySlot = -1;
        string tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        if (File.Exists(tmp)) File.Delete(tmp);
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "run.json", JsonConvert.SerializeObject(copy));
            foreach (var point in run.snapshots)
            {
                Read(run, point);
                WriteEntry(zip, point.id + ".json", File.ReadAllText(Snapshot(run.id, point.id)));
            }
        }
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
    private static void WriteEntry(ZipArchive zip, string name, string json)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(json);
    }
    // Never extract arbitrary archive paths. Read only manifest-declared, GUID-named entries.
    public RunData InspectImport(string path, Action<SavePayload> validate, out Dictionary<string, SavePayload> payloads)
    {
        payloads = new();
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 10001 || zip.Entries.Sum(e => e.Length) > 512L * 1024 * 1024 || zip.Entries.Any(e => e.Length > 32L * 1024 * 1024)) throw new InvalidDataException("分享文件过大");
        if (zip.Entries.Select(e => e.FullName).Distinct().Count() != zip.Entries.Count) throw new InvalidDataException("分享文件有重复条目");
        string ReadEntry(string name)
        {
            using var reader = new StreamReader((zip.GetEntry(name) ?? throw new InvalidDataException("分享文件不完整")).Open());
            var text = new StringBuilder(); var buffer = new char[8192]; int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (text.Length + count > 32 * 1024 * 1024) throw new InvalidDataException("分享文件内容过大");
                text.Append(buffer, 0, count);
            }
            return text.ToString();
        }
        var run = Decode<RunData>(ReadEntry("run.json"));
        if (!run.IsCompatible) throw new InvalidDataException(SaveDataContract.IncompatibleMessage);
        if (run.version != FormatVersion || run.dead || run.snapshots == null || run.snapshots.Count == 0 || run.snapshots.Select(s => s.id).Distinct().Count() != run.snapshots.Count) throw new InvalidDataException("无效的世界线记录");
        var allowed = new HashSet<string>(run.snapshots.Select(s => Id(s.id) + ".json")) { "run.json" };
        if (zip.Entries.Any(e => !allowed.Contains(e.FullName))) throw new InvalidDataException("分享文件包含不支持的条目");
        foreach (var point in run.snapshots)
        {
            if (!Enum.IsDefined(typeof(SaveKind), point.kind)) throw new InvalidDataException("未知保存点类型");
            string json = ReadEntry(point.id + ".json");
            if (Hash(json) != point.checksum) throw new InvalidDataException("分享文件校验失败");
            var payload = Decode<SavePayload>(json);
            if (payload.version != FormatVersion) throw new InvalidDataException("不支持此存档版本");
            validate(payload);
            payloads.Add(point.id, payload);
        }
        if (!payloads.ContainsKey(run.latestSnapshotId ?? "")) throw new InvalidDataException("缺少继续游戏的保存点");
        NormalizeKinds(run);
        return run;
    }
    public RunData Import(string path, Action<SavePayload> validate)
    {
        var source = InspectImport(path, validate, out var payloads);
        return Clone(source, p => payloads[p.id]);
    }
    public RunData Copy(RunData source) => Clone(source, p => Read(source, p));
    private RunData Clone(RunData source, Func<SavePoint, SavePayload> read)
    {
        if (!source.IsCompatible) throw new InvalidDataException(SaveDataContract.IncompatibleMessage);
        var copy = Decode<RunData>(JsonConvert.SerializeObject(source));
        copy.id = Guid.NewGuid().ToString("N"); copy.name = source.name + "（副本）";
        copy.hardcore = false; copy.fromHardcore |= source.hardcore; copy.legacySlot = -1;
        copy.createdUtc = DateTime.UtcNow; copy.lastPlayedUtc = DateTime.MinValue;
        copy.snapshots = new();
        // Publish a single manifest after the entire copy succeeds.
        foreach (var point in source.snapshots)
        {
            var p = Decode<SavePoint>(JsonConvert.SerializeObject(point));
            p.id = Guid.NewGuid().ToString("N");
            string json = JsonConvert.SerializeObject(read(point)); p.checksum = Hash(json);
            AtomicWrite(Snapshot(copy.id, p.id), json);
            copy.snapshots.Add(p);
            if (point.id == source.latestSnapshotId) copy.latestSnapshotId = p.id;
        }
        Update(copy);
        return copy;
    }
}
