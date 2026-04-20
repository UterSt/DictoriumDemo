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
        // Build the flat string on the C# side to avoid JS array serialization issues.
        // Blazor JSInterop serializes C# arrays as JSON objects, not JS Arrays,
        // so pairs.map() would fail. Passing a plain string is always safe.
        // Format: "key1val1key2val2..."
        var flat = string.Join("", pairs.SelectMany(p => new[] { p.Key, p.Value })) + "";
        return await js.InvokeAsync<int>("DictoriumInterop.phCreate", flat);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<DictItem> ParseSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return new();
        try
        {
            var docs = JsonSerializer.Deserialize<JsonElement[]>(json)!;
            return docs.Select(d => new DictItem(
                d.GetProperty("k").GetString()!,
                d.GetProperty("v").GetString()!
            )).ToList();
        }
        catch { return new(); }
    }
}

/// <summary>A key-value pair returned from the Dictorium WASM snapshot.</summary>
public record DictItem(string Key, string Value);
