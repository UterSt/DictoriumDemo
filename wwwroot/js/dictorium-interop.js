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
    //  QuadraticProbingDictionary
    // ══════════════════════════════════════════════════════════════════════

    function qpCreate()  { return mod._qp_create(); }
    function qpFree(h)   { mod._qp_free(h); }
    function qpClear(h)  { mod._qp_clear(h); }
    function qpCount(h)  { return mod._qp_count(h); }

    function qpContains(h, key) {
        return withStr(key, kp => mod._qp_contains(h, kp) === 1);
    }
    function qpGet(h, key) {
        return withStr(key, kp => readAndFree(mod._qp_get(h, kp)));
    }
    function qpAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._qp_add(h, kp, vp) === 1);
    }
    function qpInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) => mod._qp_insert_or_assign(h, kp, vp));
    }
    function qpRemove(h, key) {
        return withStr(key, kp => mod._qp_remove(h, kp) === 1);
    }
    function qpSnapshot(h) {
        try {
            const ptr = mod._qp_snapshot(h);
            if (!ptr) return null;
            const json = mod.UTF8ToString(ptr);
            mod._free(ptr);
            return json || null;
        } catch (e) {
            return null;
        }
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

    // ══════════════════════════════════════════════════════════════════════
    //  CuckooDictionary
    // ══════════════════════════════════════════════════════════════════════

    function cuckooCreate()  { return mod._cuckoo_create(); }
    function cuckooFree(h)   { mod._cuckoo_free(h); }
    function cuckooClear(h)  { mod._cuckoo_clear(h); }
    function cuckooCount(h)  { return mod._cuckoo_count(h); }

    function cuckooContains(h, key) {
        return withStr(key, kp => mod._cuckoo_contains(h, kp) === 1);
    }
    // C returns malloc'd char* — must readAndFree.
    function cuckooGet(h, key) {
        return withStr(key, kp => readAndFree(mod._cuckoo_get(h, kp)));
    }
    function cuckooAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._cuckoo_add(h, kp, vp) === 1);
    }
    function cuckooInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) => mod._cuckoo_insert_or_assign(h, kp, vp));
    }
    function cuckooRemove(h, key) {
        return withStr(key, kp => mod._cuckoo_remove(h, kp) === 1);
    }
    // Snapshot returns malloc'd JSON — must readAndFree.
    function cuckooSnapshot(h) {
        return readAndFree(mod._cuckoo_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  AvlDictionary
    // ══════════════════════════════════════════════════════════════════════

    function avlCreate()  { return mod._avl_create(); }
    function avlFree(h)   { mod._avl_free(h); }
    function avlClear(h)  { mod._avl_clear(h); }
    function avlCount(h)  { return mod._avl_count(h); }
    function avlHeight(h) { return mod._avl_height(h); }

    function avlContains(h, key) {
        return withStr(key, kp => mod._avl_contains(h, kp) === 1);
    }
    // avl_get returns an internal pointer — use readStr, do NOT free.
    function avlGet(h, key) {
        return withStr(key, kp => readStr(mod._avl_get(h, kp)));
    }
    function avlAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._avl_add(h, kp, vp) === 1);
    }
    function avlInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) => mod._avl_insert_or_assign(h, kp, vp));
    }
    function avlRemove(h, key) {
        return withStr(key, kp => mod._avl_remove(h, kp) === 1);
    }
    // Snapshot returns malloc'd JSON [["k","v"],...] sorted in-order — must readAndFree.
    function avlSnapshot(h) {
        return readAndFree(mod._avl_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  SkipListDictionary
    // ══════════════════════════════════════════════════════════════════════

    function slCreate()    { return mod._sl_create(); }
    function slFree(h)     { mod._sl_free(h); }
    function slClear(h)    { mod._sl_clear(h); }
    function slCount(h)    { return mod._sl_count(h); }
    function slMaxLevel(h) { return mod._sl_max_level(h); }

    function slContains(h, key) {
        return withStr(key, kp => mod._sl_contains(h, kp) === 1);
    }
    // sl_get returns an internal pointer — use readStr, do NOT free.
    function slGet(h, key) {
        return withStr(key, kp => readStr(mod._sl_get(h, kp)));
    }
    function slAdd(h, key, val) {
        return withStr2(key, val, (kp, vp) => mod._sl_add(h, kp, vp) === 1);
    }
    function slInsertOrAssign(h, key, val) {
        withStr2(key, val, (kp, vp) => mod._sl_insert_or_assign(h, kp, vp));
    }
    function slRemove(h, key) {
        return withStr(key, kp => mod._sl_remove(h, kp) === 1);
    }
    // Snapshot returns malloc'd JSON [["k","v"],...] sorted — must readAndFree.
    function slSnapshot(h) {
        return readAndFree(mod._sl_snapshot(h));
    }

    // ══════════════════════════════════════════════════════════════════════
    //  TreapDictionary  (JavaScript layer, WASM module must be initialised)
    //  Cartesian tree: BST by key, max-heap by random priority.
    //  String data lives in plain JS objects; WASM module is still the
    //  runtime host (init / isReady / lastError delegate to it).
    // ══════════════════════════════════════════════════════════════════════

    // ── Internal helpers ──────────────────────────────────────────────
    function _tNewNode(key, val) {
        return { key, value: val, priority: (Math.random() * 0xFFFFFFFF) >>> 0, left: null, right: null };
    }
    function _tRotR(y) { const x = y.left; y.left = x.right; x.right = y; return x; }
    function _tRotL(x) { const y = x.right; x.right = y.left; y.left = x; return y; }

    // Returns [newRoot, inserted:bool]
    function _tInsert(n, key, val) {
        if (!n) return [_tNewNode(key, val), true];
        if (key === n.key) return [n, false];
        if (key < n.key) {
            const [nl, ok] = _tInsert(n.left, key, val);
            n.left = nl;
            if (n.left && n.left.priority > n.priority) n = _tRotR(n);
            return [n, ok];
        } else {
            const [nr, ok] = _tInsert(n.right, key, val);
            n.right = nr;
            if (n.right && n.right.priority > n.priority) n = _tRotL(n);
            return [n, ok];
        }
    }

    function _tInsertOrAssign(n, key, val) {
        if (!n) return _tNewNode(key, val);
        if (key === n.key) { n.value = val; return n; }
        if (key < n.key) {
            n.left = _tInsertOrAssign(n.left, key, val);
            if (n.left && n.left.priority > n.priority) n = _tRotR(n);
        } else {
            n.right = _tInsertOrAssign(n.right, key, val);
            if (n.right && n.right.priority > n.priority) n = _tRotL(n);
        }
        return n;
    }

    // Returns [newRoot, removed:bool]
    function _tRemove(n, key) {
        if (!n) return [null, false];
        if (key < n.key) { const [nl, ok] = _tRemove(n.left, key);  n.left  = nl; return [n, ok]; }
        if (key > n.key) { const [nr, ok] = _tRemove(n.right, key); n.right = nr; return [n, ok]; }
        // Found
        if (!n.left && !n.right) return [null, true];
        if (!n.left)  { n = _tRotL(n); const [nl] = _tRemove(n.left,  key); n.left  = nl; return [n, true]; }
        if (!n.right) { n = _tRotR(n); const [nr] = _tRemove(n.right, key); n.right = nr; return [n, true]; }
        if (n.left.priority > n.right.priority) {
            n = _tRotR(n); const [nr] = _tRemove(n.right, key); n.right = nr;
        } else {
            n = _tRotL(n); const [nl] = _tRemove(n.left,  key); n.left  = nl;
        }
        return [n, true];
    }

    function _tContains(n, key) {
        while (n) { if (key === n.key) return true; n = key < n.key ? n.left : n.right; }
        return false;
    }
    function _tGet(n, key) {
        while (n) { if (key === n.key) return n.value; n = key < n.key ? n.left : n.right; }
        return null;
    }
    function _tCount(n)  { return n ? 1 + _tCount(n.left)  + _tCount(n.right)  : 0; }
    function _tHeight(n) { return n ? 1 + Math.max(_tHeight(n.left), _tHeight(n.right)) : 0; }
    function _tSnap(n)   { return n ? { key: n.key, value: n.value, priority: n.priority, left: _tSnap(n.left), right: _tSnap(n.right) } : null; }

    // ── Handle store ──────────────────────────────────────────────────
    const _treapStore = {};
    let   _treapSeq   = 1;

    function treapCreate()           { const id = _treapSeq++; _treapStore[id] = null; return id; }
    function treapFree(h)            { delete _treapStore[h]; }
    function treapClear(h)           { _treapStore[h] = null; }
    function treapCount(h)           { return _tCount(_treapStore[h]); }
    function treapHeight(h)          { return _tHeight(_treapStore[h]); }
    function treapContains(h, key)   { return _tContains(_treapStore[h], key); }
    function treapGet(h, key)        { return _tGet(_treapStore[h], key) ?? ''; }
    function treapAdd(h, key, val)   { const [r, ok] = _tInsert(_treapStore[h], key, val); _treapStore[h] = r; return ok; }
    function treapInsertOrAssign(h, key, val) { _treapStore[h] = _tInsertOrAssign(_treapStore[h], key, val); }
    function treapRemove(h, key)     { const [r, ok] = _tRemove(_treapStore[h], key); _treapStore[h] = r; return ok; }
    function treapSnapshot(h)        { return JSON.stringify(_tSnap(_treapStore[h])); }

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

        qpCreate, qpFree, qpAdd, qpInsertOrAssign,
        qpContains, qpGet, qpRemove, qpClear,
        qpCount, qpSnapshot,

        chainingCreate, chainingFree, chainingAdd, chainingInsertOrAssign,
        chainingContains, chainingGet, chainingRemove, chainingClear,
        chainingCount, chainingSnapshot,

        cuckooCreate, cuckooFree, cuckooAdd, cuckooInsertOrAssign,
        cuckooContains, cuckooGet, cuckooRemove, cuckooClear,
        cuckooCount, cuckooSnapshot,

        avlCreate, avlFree, avlAdd, avlInsertOrAssign,
        avlContains, avlGet, avlRemove, avlClear,
        avlCount, avlHeight, avlSnapshot,

        slCreate, slFree, slAdd, slInsertOrAssign,
        slContains, slGet, slRemove, slClear,
        slCount, slMaxLevel, slSnapshot,

        treapCreate, treapFree, treapAdd, treapInsertOrAssign,
        treapContains, treapGet, treapRemove, treapClear,
        treapCount, treapHeight, treapSnapshot,
    };
})();
