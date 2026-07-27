// WebGL-only bridge: Unity's WebGL PlayerPrefs backend writes into an
// in-memory IDBFS mount that Emscripten only flushes to the browser's real
// IndexedDB asynchronously. PlayerPrefs.Save() alone queues that flush but
// does not guarantee it lands before a tab close/hard refresh.
//
// Two safe points are covered:
// 1. SyncGameProgressToIndexedDB(), called from GameProgress2D.Save() right
//    after PlayerPrefs.Save() - so every progress write (current level,
//    highest unlocked, per-level stars) is durably flushed immediately, not
//    just queued.
// 2. A one-time listener install (via __postset, run once when this library
//    is linked at startup - never per call) that also flushes on
//    visibilitychange (tab hidden/backgrounded) and beforeunload, as a
//    safety net for any write that happens right before the page closes.
mergeInto(LibraryManager.library, {
    $YagmurRotasi2DSync: function () {
        function syncNow() {
            if (typeof FS !== 'undefined' && typeof FS.syncfs === 'function') {
                FS.syncfs(false, function (err) {
                    if (err) {
                        console.error('YagmurRotasi2D: PlayerPrefs IndexedDB sync failed', err);
                    }
                });
            }
        }

        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                syncNow();
            }
        });
        window.addEventListener('beforeunload', syncNow);

        window.__yagmurRotasi2DSyncNow = syncNow;
    },

    SyncGameProgressToIndexedDB__deps: ['$YagmurRotasi2DSync'],
    SyncGameProgressToIndexedDB__postset: 'YagmurRotasi2DSync();',
    SyncGameProgressToIndexedDB: function () {
        if (window.__yagmurRotasi2DSyncNow) {
            window.__yagmurRotasi2DSyncNow();
        } else if (typeof FS !== 'undefined' && typeof FS.syncfs === 'function') {
            FS.syncfs(false, function (err) {
                if (err) {
                    console.error('YagmurRotasi2D: PlayerPrefs IndexedDB sync failed', err);
                }
            });
        }
    }
});
