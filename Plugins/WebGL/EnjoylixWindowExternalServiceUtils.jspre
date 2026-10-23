window.EnjoylixWebExternalServiceUtils = (function () {
    const LOG_TAG = "[Enjoylix][ExternalService][JSPRE]";

    console.log(`${LOG_TAG} Initializing...`);

    // Must match WindowState* constants in WebGlPopupHandler.cs.
    const WindowState = { Open: 0, Closed: 1, Blocked: 2, Cancelled: 3 };

    const WindowData = (function () {
        let windowIdCounter = 0;
        const windows = new Map();

        function addWindow(win) {
            const id = windowIdCounter++;
            windows.set(id, { instance: win, listeners: [], pending: false, finalState: null });

            console.log(`${LOG_TAG} addWindow -> id=${id}`, {
                closed: !!(win && win.closed),
                totalWindows: windows.size
            });

            return id;
        }

        // Reserves an id for a window that will be opened later from a user gesture
        // (see EnjoylixConfirmOpen). Reported as open until resolved or deleted.
        function addPendingWindow() {
            const id = windowIdCounter++;
            windows.set(id, { instance: null, listeners: [], pending: true, finalState: null });

            console.log(`${LOG_TAG} addPendingWindow -> id=${id}, totalWindows=${windows.size}`);

            return id;
        }

        function isPending(id) {
            const entry = windows.get(id);
            return !!(entry && entry.pending);
        }

        // win === null means window.open was blocked again; reported as Blocked once.
        function resolvePendingWindow(id, win) {
            const entry = windows.get(id);
            if (!entry || !entry.pending) {
                console.warn(`${LOG_TAG} resolvePendingWindow -> id=${id} is not pending`);
                return;
            }

            entry.pending = false;
            entry.instance = win;
            entry.finalState = win ? null : WindowState.Blocked;

            console.log(`${LOG_TAG} resolvePendingWindow -> id=${id}`, { blocked: !win });
        }

        // User declined the game's confirmation; reported as Cancelled once.
        function cancelPendingWindow(id) {
            const entry = windows.get(id);
            if (!entry || !entry.pending) {
                console.warn(`${LOG_TAG} cancelPendingWindow -> id=${id} is not pending`);
                return;
            }

            entry.pending = false;
            entry.finalState = WindowState.Cancelled;

            console.log(`${LOG_TAG} cancelPendingWindow -> id=${id}`);
        }

        function getWindow(id) {
            const entry = windows.get(id);
            const result = entry ? entry.instance : null;

            console.log(`${LOG_TAG} getWindow -> id=${id}`, {
                found: !!result,
                hasEntry: windows.has(id)
            });

            return result;
        }

        function addListener(id, listener) {
            console.log(`${LOG_TAG} addListener -> id=${id}`, {
                hasWindow: windows.has(id),
                listenerType: typeof listener
            });

            window.addEventListener("message", listener, false);

            if (windows.has(id)) {
                windows.get(id).listeners.push(listener);
                console.log(`${LOG_TAG} addListener success -> id=${id}, listeners=${windows.get(id).listeners.length}`);
            } else {
                console.warn(`${LOG_TAG} addListener called for unknown window id=${id}`);
            }
        }

        function deleteWindowWithListeners(id) {
            console.log(`${LOG_TAG} deleteWindowWithListeners -> id=${id}`, {
                hasWindow: windows.has(id)
            });

            if (windows.has(id)) {
                const entry = windows.get(id);
                const listeners = entry.listeners || [];

                listeners.forEach(listener => {
                    window.removeEventListener("message", listener, false);
                });

                console.log(`${LOG_TAG} removed listeners -> id=${id}, count=${listeners.length}`);

                entry.listeners = [];
                windows.delete(id);

                console.log(`${LOG_TAG} window deleted -> id=${id}, totalWindows=${windows.size}`);
            }
        }

        function getWindowState(id) {
            const entry = windows.get(id);

            if (entry && entry.pending) {
                console.log(`${LOG_TAG} getWindowState -> id=${id}, waiting for user confirmation`);
                return WindowState.Open;
            }

            if (entry && entry.finalState !== null) {
                const finalState = entry.finalState;
                console.warn(`${LOG_TAG} getWindowState -> id=${id}, confirmation ended`, {
                    blocked: finalState === WindowState.Blocked,
                    cancelled: finalState === WindowState.Cancelled
                });
                deleteWindowWithListeners(id);
                return finalState;
            }

            const win = entry ? entry.instance : null;

            if (!win) {
                console.warn(`${LOG_TAG} getWindowState -> id=${id}, window not found, treat as closed`);
                deleteWindowWithListeners(id);
                return WindowState.Closed;
            }

            if (win.closed) {
                console.log(`${LOG_TAG} getWindowState -> id=${id}, window is closed`);
                deleteWindowWithListeners(id);
                return WindowState.Closed;
            }

            console.log(`${LOG_TAG} getWindowState -> id=${id}, window still open`);
            return WindowState.Open;
        }

        function debugDump() {
            const ids = Array.from(windows.keys());
            console.log(`${LOG_TAG} debugDump`, {
                totalWindows: windows.size,
                windowIds: ids
            });
        }

        return {
            addWindow,
            addPendingWindow,
            isPending,
            resolvePendingWindow,
            cancelPendingWindow,
            getWindow,
            addListener,
            deleteWindowWithListeners,
            getWindowState,
            debugDump
        };
    })();

    const api = {
        WindowState: WindowState,
        WindowData: WindowData,

        CloseWindow: function (windowId) {
            console.log(`${LOG_TAG} CloseWindow called -> id=${windowId}`);

            const win = WindowData.getWindow(windowId);
            if (win && !win.closed) {
                console.log(`${LOG_TAG} Closing actual browser window -> id=${windowId}`);
                win.close();
            } else {
                console.warn(`${LOG_TAG} CloseWindow -> window missing or already closed -> id=${windowId}`);
            }

            WindowData.deleteWindowWithListeners(windowId);
            WindowData.debugDump();
        },

        DebugDump: function () {
            WindowData.debugDump();
        }
    };

    console.log(`${LOG_TAG} Initialized`, {
        keys: Object.keys(api),
        windowDataKeys: Object.keys(WindowData)
    });

    return api;
})();