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

    /// <summary>
    /// Loads the WebAssembly module (dictorium.js + dictorium.wasm).
    /// Idempotent — safe to call multiple times.
    /// </summary>
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

    /// <summary>Creates a new LinearDictionary instance. Returns handle ID.</summary>
    public async Task<int> LinearCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.linearCreate");

    public async Task LinearFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.linearFree", handle);

    /// <summary>
    /// Calls dtr::LinearDictionary::Add.
    /// Returns true on success, false if key already exists (ArgumentException in C++).
    /// Complexity: O(n)
    /// </summary>
    public async Task<bool> LinearAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.linearAdd", handle, key, val);

    /// <summary>
    /// Calls dtr::LinearDictionary::InsertOrAssign.
    /// Updates value if key exists, appends otherwise.
    /// Complexity: O(n)
    /// </summary>
    public async Task LinearInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.linearInsertOrAssign", handle, key, val);

    /// <summary>
    /// Calls dtr::LinearDictionary::ContainsKey — linear scan.
    /// Complexity: O(n)
    /// </summary>
    public async Task<bool> LinearContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.linearContains", handle, key);

    /// <summary>
    /// Calls dtr::LinearDictionary::TryGetValue.
    /// Returns value string or empty string if not found.
    /// Complexity: O(n)
    /// </summary>
    public async Task<string> LinearGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.linearGet", handle, key);

    /// <summary>
    /// Calls dtr::LinearDictionary::Remove — finds and erases with element shift.
    /// Returns true if key was present.
    /// Complexity: O(n)
    /// </summary>
    public async Task<bool> LinearRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.linearRemove", handle, key);

    /// <summary>Calls dtr::LinearDictionary::Clear. Complexity: O(n)</summary>
    public async Task LinearClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.linearClear", handle);

    /// <summary>Calls dtr::LinearDictionary::Count. Complexity: O(1)</summary>
    public async Task<int> LinearCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.linearCount", handle);

    /// <summary>
    /// Returns all items in the internal _dict vector as a JSON snapshot.
    /// Used to refresh the visualisation after each operation.
    /// Parsed here into a list of (Key, Value) tuples.
    /// </summary>
    public async Task<List<DictItem>> LinearSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.linearSnapshot", handle);
        return ParseSnapshot(json);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PerfectHashDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calls dtr::PerfectHashDictionary constructor — two-level perfect hashing build.
    /// Complexity: O(n^1.5) upper bound, O(n) expected.
    /// Returns handle ID, or -1 on error.
    /// </summary>
    public async Task<int> PhCreateAsync(List<DictItem> pairs)
    {
        // C signature: void* ph_create(int count, const char* flat_keys, const char* flat_vals)
        // Pass count + two string[] arrays; JS interop builds flat null-terminated buffers.
        var keys = pairs.Select(p => p.Key).ToArray();
        var vals = pairs.Select(p => p.Value).ToArray();
        return await js.InvokeAsync<int>("DictoriumInterop.phCreate", pairs.Count, keys, vals);
    }

    public async Task PhFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.phFree", handle);

    /// <summary>
    /// Calls dtr::PerfectHashDictionary::ContainsKey — strictly O(1)/Ω(1).
    /// Two hash computations + one memory access, no collisions.
    /// </summary>
    public async Task<bool> PhContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.phContains", handle, key);

    /// <summary>
    /// Calls dtr::PerfectHashDictionary::TryGetValidatedValue — O(1), full key check.
    /// Returns value string or empty if not found.
    /// </summary>
    public async Task<string> PhGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.phGet", handle, key);

    /// <summary>Calls dtr::PerfectHashDictionary::Count — O(1).</summary>
    public async Task<int> PhCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.phCount", handle);

    /// <summary>
    /// Returns all live key-value pairs via PerfectHashIterator (skips tombstones).
    /// </summary>
    public async Task<List<DictItem>> PhSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.phSnapshot", handle);
        return ParseSnapshot(json);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ChainingDictionary<string, string>
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Creates a new ChainingDictionary instance. Returns handle ID.</summary>
    public async Task<int> ChainingCreateAsync()
        => await js.InvokeAsync<int>("DictoriumInterop.chainingCreate");

    public async Task ChainingFreeAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.chainingFree", handle);

    /// <summary>Returns true if key exists. Complexity: O(1+α)</summary>
    public async Task<bool> ChainingContainsAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.chainingContains", handle, key);

    /// <summary>Returns value string, or empty string if key absent. Complexity: O(1+α)</summary>
    public async Task<string> ChainingGetAsync(int handle, string key)
        => await js.InvokeAsync<string>("DictoriumInterop.chainingGet", handle, key) ?? string.Empty;

    /// <summary>Returns true when key was new, false if key already existed. Complexity: O(1+α)</summary>
    public async Task<bool> ChainingAddAsync(int handle, string key, string val)
        => await js.InvokeAsync<bool>("DictoriumInterop.chainingAdd", handle, key, val);

    /// <summary>Inserts or updates key-value pair. Complexity: O(1+α)</summary>
    public async Task ChainingInsertOrAssignAsync(int handle, string key, string val)
        => await js.InvokeVoidAsync("DictoriumInterop.chainingInsertOrAssign", handle, key, val);

    /// <summary>Returns true if key was found and removed. Complexity: O(1+α)</summary>
    public async Task<bool> ChainingRemoveAsync(int handle, string key)
        => await js.InvokeAsync<bool>("DictoriumInterop.chainingRemove", handle, key);

    /// <summary>Clears all entries. Complexity: O(m)</summary>
    public async Task ChainingClearAsync(int handle)
        => await js.InvokeVoidAsync("DictoriumInterop.chainingClear", handle);

    /// <summary>Returns number of elements. Complexity: O(1)</summary>
    public async Task<int> ChainingCountAsync(int handle)
        => await js.InvokeAsync<int>("DictoriumInterop.chainingCount", handle);

    /// <summary>
    /// Returns a full snapshot of the hash table as a list of ChainBucket.
    /// The C function returns JSON:
    ///   {"bucketCount":N,"buckets":[[["k1","v1"],["k2","v2"]],[],["k3","v3"],...]}
    /// </summary>
    public async Task<List<ChainBucket>> ChainingSnapshotAsync(int handle)
    {
        var json = await js.InvokeAsync<string>("DictoriumInterop.chainingSnapshot", handle);
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var bucketsJson = doc.RootElement.GetProperty("buckets");
            var result = new List<ChainBucket>();
            int slot = 0;
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the WASM snapshot format: [["key1","val1"],["key2","val2"],...]
    /// The C++ _build_snapshot() template emits an array of 2-element arrays —
    /// NOT the old {"k":...,"v":...} object format.
    /// </summary>
    private static List<DictItem> ParseSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return new();
        try
        {
            // Each element is a 2-element string array: ["key", "value"]
            var rows = JsonSerializer.Deserialize<string[][]>(json)!;
            return rows.Select(r => new DictItem(r[0], r[1])).ToList();
        }
        catch { return new(); }
    }
}

/// <summary>A key-value pair returned from the Dictorium WASM snapshot.</summary>
public record DictItem(string Key, string Value);

// (ChainBucket model — placed here to keep it near the chaining methods)
/// <summary>One bucket in the ChainingDictionary hash table.</summary>
public class ChainBucket
{
    public int Slot { get; set; }
    public List<DictItem> Chain { get; set; } = new();
}
