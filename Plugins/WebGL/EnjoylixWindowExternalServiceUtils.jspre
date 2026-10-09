window.EnjoylixWebExternalServiceUtils = (function () {
    const LOG_TAG = "[Enjoylix][ExternalService][JSPRE]";

    console.log(`${LOG_TAG} Initializing...`);

    const WindowData = (function () {
        let windowIdCounter = 0;
        const windows = new Map();

        function addWindow(win) {
            const id = windowIdCounter++;
            windows.set(id, { instance: win, listeners: [] });

            console.log(`${LOG_TAG} addWindow -> id=${id}`, {
                closed: !!(win && win.closed),
                totalWindows: windows.size
            });

            return id;
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

        function checkWindowClosed(id) {
            const win = getWindow(id);

            if (!win) {
                console.warn(`${LOG_TAG} checkWindowClosed -> id=${id}, window not found, treat as closed`);
                deleteWindowWithListeners(id);
                return true;
            }

            if (win.closed) {
                console.log(`${LOG_TAG} checkWindowClosed -> id=${id}, window is closed`);
                deleteWindowWithListeners(id);
                return true;
            }

            console.log(`${LOG_TAG} checkWindowClosed -> id=${id}, window still open`);
            return false;
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
            getWindow,
            addListener,
            deleteWindowWithListeners,
            checkWindowClosed,
            debugDump
        };
    })();

    const api = {
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