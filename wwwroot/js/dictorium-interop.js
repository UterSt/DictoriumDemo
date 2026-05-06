/**
 * dictorium-interop.js
 *
 * Bridge between the Dictorium WebAssembly module and Blazor's JSInterop.
 * Loaded after dictorium.js in index.html.
 *
 * String protocol:
 *   - To C:   allocStr() → WASM heap, freed after call.
 *   - From C (static):   readStr(ptr)      — UTF8ToString only, do NOT free.
 *   - From C (malloc'd): readAndFree(ptr)  — UTF8ToString then _free(ptr).
 *     malloc'd returns: linear_get, ph_get, *_snapshot.
 *
 * ph_create protocol:
 *   C: void* ph_create(int count, const char* flat_keys, const char* flat_vals)
 *   flat_keys/flat_vals = N consecutive null-terminated UTF-8 strings.
 *   Built by allocFlatStrings(jsArray).
 */

window.DictoriumInterop = (() => {
    let mod = null;

    // ── String helpers ──────────────────────────────────────────────────────

    function allocStr(s) {
        const bytes = mod.lengthBytesUTF8(s) + 1;
        const ptr   = mod._malloc(bytes);
        mod.stringToUTF8(s, ptr, bytes);
        return ptr;
    }

    // Builds flat null-terminated buffer "s1\0s2\0s3\0" for _parse_flat() in C.
    function allocFlatStrings(arr) {
        let totalBytes = 0;
        for (const s of arr) totalBytes += mod.lengthBytesUTF8(s) + 1;
        if (totalBytes === 0) totalBytes = 1;
        const ptr = mod._malloc(totalBytes);
        let offset = 0;
        for (const s of arr) {
            const len = mod.lengthBytesUTF8(s);
            mod.stringToUTF8(s, ptr + offset, len + 1);
            offset += len + 1;
        }
        return ptr;
    }

    // Static/non-owned WASM string — do NOT free (e.g. dtr_last_error).
    function readStr(ptr) {
        return ptr ? mod.UTF8ToString(ptr) : "";
    }

    // malloc'd WASM string — read then free immediately.
    function readAndFree(ptr) {
        if (!ptr) return "";
        const s = mod.UTF8ToString(ptr);
        mod._free(ptr);
        return s;
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

    // ── Module init ─────────────────────────────────────────────────────────

    async function init() {
        if (mod !== null) return;
        mod = await DictoriumModule();
    }

    function isReady() { return mod !== null; }
    function lastError() { return readStr(mod._dtr_last_error()); }

    // ══════════════════════════════════════════════════════════════════════
    //  LinearDictionary
    // ══════════════════════════════════════════════════════════════════════

    function linearCreate()    { return mod._linear_create(); }
    function linearFree(h)     { mod._linear_free(h); }
    function linearClear(h)    { mod._linear_clear(h); }
    function linearCount(h)    { return mod._linear_count(h); }

    function linearAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._linear_add(h, kp, vp) === 1);
    }
    function linearInsertOrAssign(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._linear_insert_or_assign(h, kp, vp) === 1);
    }
    function linearContains(h, key) {
        return withStr(key, kp => mod._linear_contains(h, kp) === 1);
    }
    // C returns malloc'd char* — must readAndFree.
    function linearGet(h, key) {
        return withStr(key, kp => readAndFree(mod._linear_get(h, kp)));
    }
    function linearRemove(h, key) {
        return withStr(key, kp => mod._linear_remove(h, kp) === 1);
    }
    // Snapshot returns malloc'd JSON [["k","v"],...] — must readAndFree.
    function linearSnapshot(h) {
        return readAndFree(mod._linear_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  PerfectHashDictionary
    //
    //  C signature: void* ph_create(int count, const char* flat_keys, const char* flat_vals)
    //
    //  JS receives count (number) + keys (string[]) + vals (string[]) from C#.
    //  Blazor serializes string[] as a JSON array, which JS receives as Array.
    // ══════════════════════════════════════════════════════════════════════

    function phCreate(count, keys, vals) {
        const kp = allocFlatStrings(keys);
        const vp = allocFlatStrings(vals);
        try {
            return mod._ph_create(count, kp, vp);
        } finally {
            mod._free(kp);
            mod._free(vp);
        }
    }

    function phFree(h)  { mod._ph_free(h); }
    function phCount(h) { return mod._ph_count(h); }

    function phContains(h, key) {
        return withStr(key, kp => mod._ph_contains(h, kp) === 1);
    }
    // C returns malloc'd char* — must readAndFree.
    function phGet(h, key) {
        return withStr(key, kp => readAndFree(mod._ph_get(h, kp)));
    }
    // Snapshot returns malloc'd JSON [["k","v"],...] — must readAndFree.
    function phSnapshot(h) {
        return readAndFree(mod._ph_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  LinearProbingDictionary
    // ══════════════════════════════════════════════════════════════════════

    function lpCreate()  { return mod._lp_create(); }
    function lpFree(h)   { mod._lp_free(h); }
    function lpClear(h)  { mod._lp_clear(h); }
    function lpCount(h)  { return mod._lp_count(h); }

    function lpContains(h, key) {
        return withStr(key, kp => mod._lp_contains(h, kp) === 1);
    }
    // C returns malloc'd char* — must readAndFree.
    function lpGet(h, key) {
        return withStr(key, kp => readAndFree(mod._lp_get(h, kp)));
    }
    function lpAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._lp_add(h, kp, vp) === 1);
    }
    function lpInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) => mod._lp_insert_or_assign(h, kp, vp));
    }
    function lpRemove(h, key) {
        return withStr(key, kp => mod._lp_remove(h, kp) === 1);
    }
    // Snapshot returns malloc'd JSON — must readAndFree.
    function lpSnapshot(h) {
        return readAndFree(mod._lp_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ChainingDictionary
    // ══════════════════════════════════════════════════════════════════════

    function chainingCreate()  { return mod._chaining_create(); }
    function chainingFree(h)   { mod._chaining_free(h); }
    function chainingClear(h)  { mod._chaining_clear(h); }
    function chainingCount(h)  { return mod._chaining_count(h); }

    function chainingContains(h, key) {
        return withStr(key, kp => mod._chaining_contains(h, kp) === 1);
    }
    // C returns malloc'd char* — must readAndFree.
    function chainingGet(h, key) {
        return withStr(key, kp => readAndFree(mod._chaining_get(h, kp)));
    }
    function chainingAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._chaining_add(h, kp, vp) === 1);
    }
    function chainingInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) => mod._chaining_insert_or_assign(h, kp, vp));
    }
    function chainingRemove(h, key) {
        return withStr(key, kp => mod._chaining_remove(h, kp) === 1);
    }
    // Snapshot returns malloc'd JSON {"bucketCount":N,"buckets":[[["k","v"],...],[],...]}
    function chainingSnapshot(h) {
        return readAndFree(mod._chaining_snapshot(h));
    }

    // ── Public API ──────────────────────────────────────────────────────────
    return {
        init, isReady, lastError,

        linearCreate, linearFree, linearAdd, linearInsertOrAssign,
        linearContains, linearGet, linearRemove, linearClear,
        linearCount, linearSnapshot,

        phCreate, phFree, phContains, phGet, phCount, phSnapshot,

        lpCreate, lpFree, lpAdd, lpInsertOrAssign,
        lpContains, lpGet, lpRemove, lpClear,
        lpCount, lpSnapshot,

        chainingCreate, chainingFree, chainingAdd, chainingInsertOrAssign,
        chainingContains, chainingGet, chainingRemove, chainingClear,
        chainingCount, chainingSnapshot,
    };
})();
