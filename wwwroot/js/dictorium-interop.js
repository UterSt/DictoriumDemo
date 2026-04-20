/**
 * dictorium-interop.js
 *
 * Bridge between the Dictorium WebAssembly module and Blazor's JSInterop.
 * Loaded after dictorium.js in index.html.
 *
 * All functions are exposed under window.DictoriumInterop and called from
 * C# via IJSRuntime.InvokeAsync("DictoriumInterop.<method>", args...).
 *
 * String protocol:
 *   - To C:   allocate UTF-8 in WASM heap, pass pointer, free after call.
 *   - From C: static buffer — copy immediately via UTF8ToString.
 */

window.DictoriumInterop = (() => {
    let mod = null;

    // ── String helpers ─────────────────────────────────────────────────────
    function allocStr(s) {
        const bytes = mod.lengthBytesUTF8(s) + 1;
        const ptr   = mod._malloc(bytes);
        mod.stringToUTF8(s, ptr, bytes);
        return ptr;
    }

    function readStr(ptr) {
        return mod.UTF8ToString(ptr);
    }

    function withStr(s, fn) {
        const ptr = allocStr(s);
        try { return fn(ptr); }
        finally { mod._free(ptr); }
    }

    function withStr2(a, b, fn) {
        const pa = allocStr(a);
        const pb = allocStr(b);
        try { return fn(pa, pb); }
        finally { mod._free(pa); mod._free(pb); }
    }

    // ── Module initialisation ──────────────────────────────────────────────
    async function init() {
        if (mod !== null) return;
        mod = await DictoriumModule();
    }

    function isReady() { return mod !== null; }

    function lastError() { return readStr(mod._dtr_last_error()); }

    // ══════════════════════════════════════════════════════════════════════
    //  LinearDictionary
    // ══════════════════════════════════════════════════════════════════════

    function linearCreate() {
        return mod._linear_create();
    }

    function linearFree(h) {
        mod._linear_free(h);
    }

    /** Returns true on success, false if key already exists. */
    function linearAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) =>
            mod._linear_add(h, kp, vp) === 1
        );
    }

    function linearInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) =>
            mod._linear_insert_or_assign(h, kp, vp)
        );
    }

    /** Returns true / false. */
    function linearContains(h, key) {
        return withStr(key, kp =>
            mod._linear_contains(h, kp) === 1
        );
    }

    /** Returns value string or "" if not found. */
    function linearGet(h, key) {
        return withStr(key, kp =>
            readStr(mod._linear_get(h, kp))
        );
    }

    /** Returns true if key was present and removed. */
    function linearRemove(h, key) {
        return withStr(key, kp =>
            mod._linear_remove(h, kp) === 1
        );
    }

    function linearClear(h)  { mod._linear_clear(h); }
    function linearCount(h)  { return mod._linear_count(h); }

    /**
     * Returns a JSON array snapshot of the internal _dict vector.
     * Used to update the visualisation after each operation.
     * Parsed on the C# side via System.Text.Json.
     */
    function linearSnapshot(h) {
        return readStr(mod._linear_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PerfectHashDictionary
    // ══════════════════════════════════════════════════════════════════════

    /**
     * Builds a PerfectHashDictionary from an array of {k,v} objects.
     * Encodes as "key1\x01val1\x01key2\x01val2\x01..." for the C wrapper.
     * Returns integer handle or -1 on error.
     */
    // flat: plain string "key1\x01val1\x01key2\x01val2\x01..." built on the C# side.
    // Previously received an object array and called .map() — but Blazor JSInterop
    // serializes C# arrays as JSON objects (not JS Arrays), so .map() threw.
    // Accepting a pre-built string avoids all serialization edge cases.
    function phCreate(flat) {
        return withStr(flat, fp => mod._ph_create(fp));
    }

    function phFree(h) { mod._ph_free(h); }

    /** Returns true / false. O(1) worst-case. */
    function phContains(h, key) {
        return withStr(key, kp => mod._ph_contains(h, kp) === 1);
    }

    /** Returns value string or "" if not found. Uses TryGetValidatedValue. */
    function phGet(h, key) {
        return withStr(key, kp => readStr(mod._ph_get(h, kp)));
    }

    function phCount(h) { return mod._ph_count(h); }

    /**
     * Returns a JSON array of all live key-value pairs.
     * Iterates via PerfectHashIterator (skips tombstone slots).
     */
    function phSnapshot(h) {
        return readStr(mod._ph_snapshot(h));
    }

    // ── Public API ─────────────────────────────────────────────────────────
    return {
        init, isReady, lastError,

        linearCreate, linearFree, linearAdd, linearInsertOrAssign,
        linearContains, linearGet, linearRemove, linearClear,
        linearCount, linearSnapshot,

        phCreate, phFree, phContains, phGet, phCount, phSnapshot
    };
})();
