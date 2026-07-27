// WebGL-only bridge: Unity's own Screen.fullScreen setter does not reliably
// trigger a real browser requestFullscreen() call synchronously within the
// originating click's call stack, so browsers silently refuse it (the
// user-gesture requirement is broken by Unity's internal indirection). These
// functions call requestFullscreen()/exitFullscreen() directly against the
// existing #unity-container/#unity-canvas element from this WebGL template,
// so they must only ever be invoked directly from a Unity C# click handler -
// never from a coroutine or delayed callback - to preserve the user gesture.
mergeInto(LibraryManager.library, {
    YagmurRotasi_RequestFullscreen: function () {
        try {
            var target = document.getElementById('unity-container');
            if (!target) target = document.getElementById('unity-canvas');
            if (!target && typeof Module !== 'undefined' && Module.canvas) target = Module.canvas;
            if (!target) return;

            var request = target.requestFullscreen
                || target.webkitRequestFullscreen
                || target.mozRequestFullScreen
                || target.msRequestFullscreen;

            if (request) {
                request.call(target);
            }
        } catch (e) {
            console.warn('YagmurRotasi_RequestFullscreen failed:', e);
        }
    },

    YagmurRotasi_ExitFullscreen: function () {
        try {
            var exit = document.exitFullscreen
                || document.webkitExitFullscreen
                || document.mozCancelFullScreen
                || document.msExitFullscreen;

            if (exit) {
                exit.call(document);
            }
        } catch (e) {
            console.warn('YagmurRotasi_ExitFullscreen failed:', e);
        }
    },

    YagmurRotasi_IsFullscreen: function () {
        var element = document.fullscreenElement
            || document.webkitFullscreenElement
            || document.mozFullScreenElement
            || document.msFullscreenElement;
        return element ? 1 : 0;
    }
});
