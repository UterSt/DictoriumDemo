using Microsoft.JSInterop;
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

    /// <summary>
    /// Returns a snapshot of the hash table as LpSnapshot.
    ///
    /// Actual WASM JSON format (verified against dictorium.wasm at runtime):
    ///   {"capacity":8,"slots":[
    ///     {"state":0},
    ///     {"state":1,"key":"apple","value":"яблоко"},
    ///     {"state":2,"key":"banana"},
    ///     ...
    ///   ]}
    ///
    ///   state 0 = empty
    ///   state 1 = occupied  (has "key" and "value" fields)
    ///   state 2 = tombstone (has "key", no "value")
    ///
    /// WASM does NOT include a top-level "count" field — computed from slots.
    /// WASM does NOT include a home-slot "h" field — estimated via backward walk.
    /// </summary>
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
                        case 1: // Occupied
                            slot.State = LpSlotState.Occupied;
                            if (el.TryGetProperty("key",   out var kEl)) slot.Key   = kEl.GetString() ?? "";
                            if (el.TryGetProperty("value", out var vEl)) slot.Value = vEl.GetString() ?? "";
                            break;
                        case 2: // Tombstone
                            slot.State = LpSlotState.Deleted;
                            break;
                        default: // 0 = empty
                            slot.State = LpSlotState.Empty;
                            break;
                    }
                }
                // else: malformed element → treat as empty
                snap.Slots.Add(slot);
            }

            // Count occupied slots (WASM JSON has no top-level count field)
            snap.Count = snap.Slots.Count(s => s.State == LpSlotState.Occupied);

            // Estimate home slot for each occupied entry via backward-walk
            foreach (var s in snap.Slots.Where(s => s.State == LpSlotState.Occupied))
                s.HomeSlot = snap.EstimateHomeSlot(s.Index);

            return snap;
        }
        catch { return new LpSnapshot(); }
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<DictItem> ParseSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return new();
        try
        {
            var rows = JsonSerializer.Deserialize<string[][]>(json)!;
            return rows.Select(r => new DictItem(r[0], r[1])).ToList();
        }
        catch { return new(); }
    }
}

/// <summary>A key-value pair returned from the Dictorium WASM snapshot.</summary>
public record DictItem(string Key, string Value);

/// <summary>One bucket in the ChainingDictionary hash table.</summary>
public class ChainBucket
{
    public int Slot { get; set; }
    public List<DictItem> Chain { get; set; } = new();
}

// ── LinearProbing models ──────────────────────────────────────────────────────

public enum LpSlotState { Empty, Occupied, Deleted }

public class LpSlot
{
    public int         Index    { get; set; }
    public LpSlotState State    { get; set; }
    public string      Key      { get; set; } = string.Empty;
    public string      Value    { get; set; } = string.Empty;
    /// <summary>
    /// Estimated home slot — start of the contiguous run in the current table.
    /// Set by LpSnapshot.EstimateHomeSlot() after parsing.  -1 = not computed.
    /// </summary>
    public int         HomeSlot { get; set; } = -1;
}

public class LpSnapshot
{
    public int          Capacity { get; set; }
    public int          Count    { get; set; }
    public List<LpSlot> Slots    { get; set; } = new();

    public double LoadFactor    => Capacity > 0 ? (double)Count / Capacity : 0;
    public int    TombstoneCount => Slots.Count(s => s.State == LpSlotState.Deleted);

    /// <summary>Find slot index where key lives, -1 if not found.</summary>
    public int FindKey(string key) =>
        Slots.FirstOrDefault(s => s.State == LpSlotState.Occupied && s.Key == key)?.Index ?? -1;

    /// <summary>
    /// Estimate the home slot for the entry at <paramref name="slotIndex"/> by
    /// walking backward through the table until an empty slot is reached.
    /// The first slot in the contiguous occupied/tombstone run is the home estimate.
    /// </summary>
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

    /// <summary>
    /// Reconstruct the probe path for a key starting at homeSlot.
    /// Returns slot indices visited until the key is found or an empty slot is hit.
    /// </summary>
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
