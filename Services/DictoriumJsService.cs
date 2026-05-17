using Microsoft.JSInterop;
using System.Linq;
using System.Text.Json;

namespace DictoriumDemo.Services;

/// <summary>
/// C# service that wraps the Dictorium WebAssembly module via IJSRuntime.
///
/// All methods map 1-to-1 to the C exports in dtr_exports.cpp, which in turn
/// call the real dtr::LinearDictionary and dtr::PerfectHashDictionary methods.
///
/// Registration: builder.Services.AddScoped&lt;DictoriumJsService&gt;()
/// Usage: inject into Razor pages, call InitAsync() on first use.
/// </summary>
public class DictoriumJsService(IJSRuntime js)
{
    private bool _initialized;

    // ── Initialisation ────────────────────────────────────────────────────────

    public async Task InitAsync()
    {
        if (_initialized) return;
        await js.InvokeVoidAsync("DictoriumInterop.init");
        _initialized = true;
    }

    public async Task<bool> IsReadyAsync()
        => await js.InvokeAsync<bool>("DictoriumInterop.isReady");

    public async Task<string> LastErrorAsync()
        => await js.InvokeAsync<string>("DictoriumInterop.lastError");

    // ══════════════════════════════════════════════════════════════════════════
    //  LinearDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> LinearCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.linearCreate");

    public async Task LinearFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.linearFree", handle);

    public async Task<bool> LinearAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.linearAdd", handle, key, val);

    public async Task LinearInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.linearInsertOrAssign", handle, key, val);

    public async Task<bool> LinearContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.linearContains", handle, key);

    public async Task<string> LinearGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.linearGet", handle, key);

    public async Task<bool> LinearRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.linearRemove", handle, key);

    public async Task LinearClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.linearClear", handle);

    public async Task<int> LinearCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.linearCount", handle);

    public async Task<List<DictItem>> LinearSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.linearSnapshot", handle);
        return ParseSnapshot(json);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PerfectHashDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> PhCreateAsync(List<DictItem> pairs)
    {
        var keys = pairs.Select(p => p.Key).ToArray();
        var vals = pairs.Select(p => p.Value).ToArray();
        return await js.InvokeAsync<int>("DictoriumInterop.phCreate", pairs.Count, keys, vals);
    }

    public async Task PhFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.phFree", handle);

    public async Task<bool> PhContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.phContains", handle, key);

    public async Task<string> PhGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.phGet", handle, key);

    public async Task<int> PhCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.phCount", handle);

    public async Task<List<DictItem>> PhSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.phSnapshot", handle);
        return ParseSnapshot(json);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LinearProbingDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> LpCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.lpCreate");

    public async Task LpFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.lpFree", handle);

    public async Task<bool> LpContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.lpContains", handle, key);

    public async Task<string> LpGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.lpGet", handle, key) ?? string.Empty;

    public async Task<bool> LpAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.lpAdd", handle, key, val);

    public async Task LpInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.lpInsertOrAssign", handle, key, val);

    public async Task<bool> LpRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.lpRemove", handle, key);

    public async Task LpClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.lpClear", handle);

    public async Task<int> LpCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.lpCount", handle);

    public async Task<LpSnapshot> LpSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.lpSnapshot", handle);
        if (string.IsNullOrWhiteSpace(json)) return new LpSnapshot();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;
            var snap      = new LpSnapshot();

            if (root.TryGetProperty("capacity", out var capEl))
                snap.Capacity = capEl.GetInt32();

            if (!root.TryGetProperty("slots", out var slotsEl))
                return snap;

            int idx = 0;
            foreach (var el in slotsEl.EnumerateArray())
            {
                var slot = new LpSlot { Index = idx++ };
                if (el.ValueKind == JsonValueKind.Object &&
                    el.TryGetProperty("state", out var stateEl))
                {
                    switch (stateEl.GetInt32())
                    {
                        case 1:
                            slot.State = LpSlotState.Occupied;
                            if (el.TryGetProperty("key",   out var kEl)) slot.Key   = kEl.GetString() ?? "";
                            if (el.TryGetProperty("value", out var vEl)) slot.Value = vEl.GetString() ?? "";
                            break;
                        case 2:
                            slot.State = LpSlotState.Deleted;
                            break;
                        default:
                            slot.State = LpSlotState.Empty;
                            break;
                    }
                }
                snap.Slots.Add(slot);
            }

            snap.Count = snap.Slots.Count(s => s.State == LpSlotState.Occupied);
            foreach (var s in snap.Slots.Where(s => s.State == LpSlotState.Occupied))
                s.HomeSlot = snap.EstimateHomeSlot(s.Index);

            return snap;
        }
        catch { return new LpSnapshot(); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  QuadraticProbingDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> QpCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.qpCreate");

    public async Task QpFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.qpFree", handle);

    public async Task<bool> QpContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.qpContains", handle, key);

    public async Task<string> QpGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.qpGet", handle, key) ?? string.Empty;

    public async Task<bool> QpAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.qpAdd", handle, key, val);

    public async Task QpInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.qpInsertOrAssign", handle, key, val);

    public async Task<bool> QpRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.qpRemove", handle, key);

    public async Task QpClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.qpClear", handle);

    public async Task<int> QpCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.qpCount", handle);

    public async Task<QpSnapshot> QpSnapshotAsync(int handle)
    {
        string json;
        try
        {
            json = await js.InvokeAsync<string>("DictoriumInterop.qpSnapshot", handle) ?? "";
        }
        catch (Exception ex)
        {
            return new QpSnapshot { ParseError = $"JS interop exception: {ex.Message}" };
        }

        if (string.IsNullOrWhiteSpace(json))
            return new QpSnapshot { ParseError = "_qp_snapshot returned null/empty." };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;
            var snap      = new QpSnapshot { RawJson = json };

            if (root.TryGetProperty("capacity", out var capEl))
                snap.Capacity = capEl.GetInt32();

            if (!root.TryGetProperty("slots", out var slotsEl))
                return snap;

            int idx = 0;
            foreach (var el in slotsEl.EnumerateArray())
            {
                var slot = new LpSlot { Index = idx++ };
                if (el.ValueKind == JsonValueKind.Object &&
                    el.TryGetProperty("state", out var stateEl))
                {
                    switch (stateEl.GetInt32())
                    {
                        case 1:
                            slot.State = LpSlotState.Occupied;
                            if (el.TryGetProperty("key",   out var kEl)) slot.Key   = kEl.GetString() ?? "";
                            if (el.TryGetProperty("value", out var vEl)) slot.Value = vEl.GetString() ?? "";
                            break;
                        case 2:
                            slot.State = LpSlotState.Deleted;
                            break;
                        default:
                            slot.State = LpSlotState.Empty;
                            break;
                    }
                }
                snap.Slots.Add(slot);
            }

            snap.Count = snap.Slots.Count(s => s.State == LpSlotState.Occupied);
            foreach (var s in snap.Slots.Where(s => s.State == LpSlotState.Occupied))
                s.HomeSlot = snap.EstimateQpHomeSlot(s.Index);

            return snap;
        }
        catch (Exception ex)
        {
            return new QpSnapshot { RawJson = json, ParseError = $"Parse exception: {ex.Message}" };
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ChainingDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> ChainingCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.chainingCreate");

    public async Task ChainingFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.chainingFree", handle);

    public async Task<bool> ChainingContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.chainingContains", handle, key);

    public async Task<string> ChainingGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.chainingGet", handle, key) ?? string.Empty;

    public async Task<bool> ChainingAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.chainingAdd", handle, key, val);

    public async Task ChainingInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.chainingInsertOrAssign", handle, key, val);

    public async Task<bool> ChainingRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.chainingRemove", handle, key);

    public async Task ChainingClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.chainingClear", handle);

    public async Task<int> ChainingCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.chainingCount", handle);

    /// <summary>
    /// JSON: {"bucketCount":N,"buckets":[[["k1","v1"],["k2","v2"]],[],["k3","v3"],...]}
    /// </summary>
    public async Task<List<ChainBucket>> ChainingSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.chainingSnapshot", handle);
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc       = JsonDocument.Parse(json);
            var bucketsJson     = doc.RootElement.GetProperty("buckets");
            var result          = new List<ChainBucket>();
            int slot            = 0;
            foreach (var bucketEl in bucketsJson.EnumerateArray())
            {
                var bucket = new ChainBucket { Slot = slot++ };
                foreach (var pairEl in bucketEl.EnumerateArray())
                {
                    var arr = pairEl.EnumerateArray().ToArray();
                    if (arr.Length >= 2)
                        bucket.Chain.Add(new DictItem(
                            arr[0].GetString() ?? string.Empty,
                            arr[1].GetString() ?? string.Empty));
                }
                result.Add(bucket);
            }
            return result;
        }
        catch { return new(); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AvlDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> AvlCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.avlCreate");

    public async Task AvlFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.avlFree", handle);

    public async Task<bool> AvlAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.avlAdd", handle, key, val);

    public async Task AvlInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.avlInsertOrAssign", handle, key, val);

    public async Task<bool> AvlContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.avlContains", handle, key);

    /// <summary>avl_get returns an internal pointer — not malloc'd, no free.</summary>
    public async Task<string> AvlGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.avlGet", handle, key) ?? string.Empty;

    public async Task<bool> AvlRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.avlRemove", handle, key);

    public async Task AvlClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.avlClear", handle);

    public async Task<int> AvlCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.avlCount", handle);

    public async Task<int> AvlHeightAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.avlHeight", handle);

    /// <summary>Returns in-order (sorted) snapshot as a flat key-value list.</summary>
    public async Task<List<DictItem>> AvlSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.avlSnapshot", handle);
        return ParseSnapshot(json);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  CuckooDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════

    public async Task<int> CuckooCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.cuckooCreate");

    public async Task CuckooFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.cuckooFree", handle);

    public async Task<bool> CuckooContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.cuckooContains", handle, key);

    public async Task<string> CuckooGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.cuckooGet", handle, key) ?? string.Empty;

    public async Task<bool> CuckooAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.cuckooAdd", handle, key, val);

    public async Task CuckooInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.cuckooInsertOrAssign", handle, key, val);

    public async Task<bool> CuckooRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.cuckooRemove", handle, key);

    public async Task CuckooClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.cuckooClear", handle);

    public async Task<int> CuckooCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.cuckooCount", handle);

    public async Task<CuckooSnapshot> CuckooSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.cuckooSnapshot", handle);
        var snap = new CuckooSnapshot { RawJson = json ?? string.Empty };
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]") return snap;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root      = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                int idx = 0;
                foreach (var el in root.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Array) continue;
                    var arr = el.EnumerateArray().ToArray();
                    if (arr.Length < 2) continue;
                    snap.Entries.Add(new CuckooSlot
                    {
                        Index = idx++,
                        State = CuckooSlotState.Occupied,
                        Key   = arr[0].GetString() ?? "",
                        Value = arr[1].GetString() ?? "",
                    });
                }
            }
            snap.Count = snap.Entries.Count;
        }
        catch (Exception ex) { snap.ParseError = ex.Message; }
        return snap;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SkipListDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<int> SlCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.slCreate");

    public async Task SlFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.slFree", handle);

    public async Task<bool> SlAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.slAdd", handle, key, val);

    public async Task SlInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.slInsertOrAssign", handle, key, val);

    public async Task<bool> SlContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.slContains", handle, key);

    /// <summary>sl_get returns an internal pointer — not malloc'd, no free.</summary>
    public async Task<string> SlGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.slGet", handle, key) ?? string.Empty;

    public async Task<bool> SlRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.slRemove", handle, key);

    public async Task SlClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.slClear", handle);

    public async Task<int> SlCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.slCount", handle);

    public async Task<int> SlMaxLevelAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.slMaxLevel", handle);

    /// <summary>Returns sorted snapshot as a flat key-value list.</summary>
    public async Task<List<DictItem>> SlSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.slSnapshot", handle);
        return ParseSnapshot(json);
    }

    /// <summary>Returns the raw JSON string from sl_snapshot for diagnostics.</summary>
    public async Task<string> SlSnapshotRawAsync(int handle)
        => await js.InvokeAsync<string>("DictoriumInterop.slSnapshot", handle) ?? string.Empty;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<DictItem> ParseSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return new();
        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var rows = JsonSerializer.Deserialize<string[][]>(json)!;
                return rows.Select(r => new DictItem(r[0], r[1])).ToList();
            }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("entries", out var entries))
            {
                var list = new List<DictItem>();
                foreach (var row in entries.EnumerateArray())
                {
                    var arr = row.EnumerateArray().ToArray();
                    if (arr.Length >= 2)
                        list.Add(new DictItem(arr[0].GetString()!, arr[1].GetString()!));
                }
                return list;
            }
            return new();
        }
        catch { return new(); }
    }

    public static (List<DictItem> Items, int MaxLevel) ParseSnapshotWithLevel(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return (new(), -1);
        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var rows = JsonSerializer.Deserialize<string[][]>(json)!;
                return (rows.Select(r => new DictItem(r[0], r[1])).ToList(), -1);
            }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int maxLevel = root.TryGetProperty("maxLevel", out var ml) ? ml.GetInt32() : -1;
            var list = new List<DictItem>();
            if (root.TryGetProperty("entries", out var entries))
                foreach (var row in entries.EnumerateArray())
                {
                    var arr = row.EnumerateArray().ToArray();
                    if (arr.Length >= 2)
                        list.Add(new DictItem(arr[0].GetString()!, arr[1].GetString()!));
                }
            return (list, maxLevel);
        }
        catch { return (new(), -1); }
    }

    /// <summary>Public accessor used by SkipListDemo.</summary>
    public static List<DictItem> ParseSnapshotPublic(string json) => ParseSnapshot(json);
}

/// <summary>A key-value pair returned from the Dictorium WASM snapshot.</summary>
public record DictItem(string Key, string Value);

/// <summary>One bucket in the ChainingDictionary hash table.</summary>
public class ChainBucket
{
    public int Slot { get; set; }
    public List<DictItem> Chain { get; set; } = new();
}


// ── CuckooDictionary models ──────────────────────────────────────────────────

public enum CuckooSlotState { Empty, Occupied }

public class CuckooSlot
{
    public int             Index { get; set; }
    public CuckooSlotState State { get; set; }
    public string          Key   { get; set; } = string.Empty;
    public string          Value { get; set; } = string.Empty;
}

public class CuckooSnapshot
{
    public int              Capacity   { get; set; }
    public int              Count      { get; set; }
    public List<CuckooSlot> Entries    { get; set; } = new();
    public string           RawJson    { get; set; } = string.Empty;
    public string           ParseError { get; set; } = string.Empty;

    public List<CuckooSlot> T1 => Entries;
    public List<CuckooSlot> T2 => new();

    public double LoadFactor => Count > 0 ? (double)Count / Math.Max(Count * 2, 8) : 0;

    public int FindInT1(string key) =>
        Entries.FirstOrDefault(s => s.State == CuckooSlotState.Occupied && s.Key == key)?.Index ?? -1;
    public int FindInT2(string key) => -1;
}

public enum LpSlotState { Empty, Occupied, Deleted }

public class LpSlot
{
    public int         Index    { get; set; }
    public LpSlotState State    { get; set; }
    public string      Key      { get; set; } = string.Empty;
    public string      Value    { get; set; } = string.Empty;
    public int         HomeSlot { get; set; } = -1;
}

public class LpSnapshot
{
    public int          Capacity { get; set; }
    public int          Count    { get; set; }
    public List<LpSlot> Slots    { get; set; } = new();

    public double LoadFactor     => Capacity > 0 ? (double)Count / Capacity : 0;
    public int    TombstoneCount => Slots.Count(s => s.State == LpSlotState.Deleted);

    public int FindKey(string key) =>
        Slots.FirstOrDefault(s => s.State == LpSlotState.Occupied && s.Key == key)?.Index ?? -1;

    public int EstimateHomeSlot(int slotIndex)
    {
        if (Capacity == 0) return slotIndex;
        int cap  = Capacity;
        int home = slotIndex;
        for (int i = 1; i < cap; i++)
        {
            int prev = (slotIndex - i + cap) % cap;
            var s = Slots.FirstOrDefault(x => x.Index == prev);
            if (s == null || s.State == LpSlotState.Empty)
                break;
            home = prev;
        }
        return home;
    }

    public List<int> ProbePath(int homeSlot, string key)
    {
        var path = new List<int>();
        if (Capacity == 0) return path;
        int cap = Capacity;
        for (int i = 0; i < cap; i++)
        {
            int si = (homeSlot + i) % cap;
            path.Add(si);
            var s = Slots.FirstOrDefault(x => x.Index == si);
            if (s == null || s.State == LpSlotState.Empty) break;
            if (s.State == LpSlotState.Occupied && s.Key == key) break;
        }
        return path;
    }
}

// ── QuadraticProbingDictionary models ────────────────────────────────────────

public class QpSnapshot
{
    public int          Capacity   { get; set; }
    public int          Count      { get; set; }
    public List<LpSlot> Slots      { get; set; } = new();
    public string       RawJson    { get; set; } = string.Empty;
    public string       ParseError { get; set; } = string.Empty;

    public double LoadFactor     => Capacity > 0 ? (double)Count / Capacity : 0;
    public int    TombstoneCount => Slots.Count(s => s.State == LpSlotState.Deleted);

    public int FindKey(string key) =>
        Slots.FirstOrDefault(s => s.State == LpSlotState.Occupied && s.Key == key)?.Index ?? -1;

    /// <summary>
    /// Try all possible home slots; pick the one whose QP path reaches
    /// slotIndex with the shortest probe length.
    /// </summary>
    public int EstimateQpHomeSlot(int slotIndex)
    {
        if (Capacity == 0) return slotIndex;
        int cap      = Capacity;
        int bestHome = slotIndex;
        int bestLen  = int.MaxValue;

        for (int home = 0; home < cap; home++)
        {
            for (int i = 0; i < cap; i++)
            {
                int si = (home + i * i) % cap;
                var s  = Slots.FirstOrDefault(x => x.Index == si);
                if (s == null || s.State == LpSlotState.Empty) break;
                if (si == slotIndex && s.State == LpSlotState.Occupied)
                {
                    if (i < bestLen) { bestLen = i; bestHome = home; }
                    break;
                }
            }
        }
        return bestHome;
    }

    /// <summary>
    /// Returns (slotIndex, probeStep i, quadratic offset i²) for each visited slot.
    /// </summary>
    public List<(int SlotIdx, int Step, int Offset)> QpProbePath(int homeSlot, string key)
    {
        var path = new List<(int, int, int)>();
        if (Capacity == 0) return path;
        int cap = Capacity;
        for (int i = 0; i < cap; i++)
        {
            int offset = i * i;
            int si     = (homeSlot + offset) % cap;
            path.Add((si, i, offset));
            var s = Slots.FirstOrDefault(x => x.Index == si);
            if (s == null || s.State == LpSlotState.Empty) break;
            if (s.State == LpSlotState.Occupied && s.Key == key) break;
        }
        return path;
    }
}
