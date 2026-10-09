var LibraryEnjoylixWindowManager = {
    Enjoylix_SendPortalLoginRequest: function (codeChallengePtr, targetOriginPtr, listenerPtr) {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][SendPortalLoginRequest]";

        try {
            const codeChallenge = UTF8ToString(codeChallengePtr);
            const targetOrigin = UTF8ToString(targetOriginPtr);

            console.log(`${LOG_TAG} called`, {
                codeChallenge: codeChallenge,
                targetOrigin: targetOrigin,
                listenerPtr: listenerPtr
            });

            if (!window.parent || window.parent === window) {
                console.error(`${LOG_TAG} window.parent is unavailable or same window`);
                return;
            }

            const listener = function (event) {
                try {
                    if (event.source !== window.parent) {
                        console.log(`${LOG_TAG} ignored message from unexpected source`, {
                            origin: event.origin,
                            rawData: event.data
                        });
                        return;
                    }

                    //if (targetOrigin && event.origin !== targetOrigin) {
                    //    console.warn(`${LOG_TAG} ignored message due to origin mismatch`, {
                    //        actualOrigin: event.origin,
                    //        expectedOrigin: targetOrigin
                    //    });
                    //    return;
                    //}

                    console.log(`${LOG_TAG} message received from parent`, {
                        origin: event.origin,
                        dataType: typeof event.data,
                        rawData: event.data
                    });

                    let data = event.data;
                    if (typeof data === "string") {
                        try { data = JSON.parse(data); } catch (_) { data = null; }
                    }

                    if (!data || typeof data !== "object") {
                        console.log(`${LOG_TAG} ignored message with unsupported data`, event.data);
                        return;
                    }

                    // The parent page also emits service messages (e.g. {type:"iframe-pos"}).
                    // Only the login result may complete the C# task; anything else keeps us listening.
                    if (data.type !== "enjoylix_portal_login_result") {
                        console.log(`${LOG_TAG} ignored non-login message`, { type: data.type });
                        return;
                    }

                    const payload = JSON.stringify(data);

                    window.removeEventListener("message", listener, false);

                    var dataPtr = stringToNewUTF8(payload);
                    try {
                        console.log(`${LOG_TAG} invoking C# callback`, {
                            listenerPtr: listenerPtr,
                            payload: payload
                        });

                        {{{ makeDynCall('vi', 'listenerPtr') }}}(dataPtr);
                    } finally {
                        _free(dataPtr);
                    }
                } catch (e) {
                    console.error(`${LOG_TAG} failed to process message`, e);
                }
            };

            window.addEventListener("message", listener, false);

            const request = {
                type: "enjoylix_portal_login_request",
                code_challenge: codeChallenge
            };

            console.log(`${LOG_TAG} sending request to parent`, request);
            window.parent.postMessage(request, targetOrigin || "*");
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
        }
    },

    Enjoylix_OpenWindow: function (urlPtr, windowNamePtr, windowSizePtr, listenerPtr) {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][OpenWindow]";

        try {
            const url = UTF8ToString(urlPtr);
            const windowName = UTF8ToString(windowNamePtr);
            const windowSize = UTF8ToString(windowSizePtr);

            console.log(`${LOG_TAG} called`, {
                url: url,
                windowName: windowName,
                windowSize: windowSize,
                listenerPtr: listenerPtr,
                hasUtils: !!window.EnjoylixWebExternalServiceUtils
            });

            if (!window.EnjoylixWebExternalServiceUtils) {
                console.error(`${LOG_TAG} EnjoylixWebExternalServiceUtils is missing on window`);
                return -1;
            }

            const win = window.open(url, windowName, windowSize);

            if (!win) {
                console.error(`${LOG_TAG} window.open failed or was blocked`);
                return -1;
            }

            console.log(`${LOG_TAG} window opened successfully`, {
                closed: !!win.closed
            });

            const utils = window.EnjoylixWebExternalServiceUtils;
            const windowId = utils.WindowData.addWindow(win);

            const listener = function (event) {
                try {
                    if (event.source !== win) {
                        console.log(`${LOG_TAG} ignored message from another window`, {
                            windowId: windowId,
                            origin: event.origin,
                            rawData: event.data
                        });
                        return;
                    }

                    console.log(`${LOG_TAG} message event received from target window`, {
                        windowId: windowId,
                        origin: event.origin,
                        dataType: typeof event.data,
                        rawData: event.data
                    });

                    let data = event.data;
                    if (typeof data === "string") {
                        try { data = JSON.parse(data); } catch (_) { data = null; }
                    }

                    if (!data || typeof data !== "object") {
                        console.log(`${LOG_TAG} ignored message with unsupported data`, {
                            windowId: windowId,
                            rawData: event.data
                        });
                        return;
                    }

                    // Ignore anything that isn't a recognized SDK result so that service
                    // messages from the target window can't complete the C# task early.
                    var acceptedTypes = [
                        "enjoylix_portal_login_result",
                        "auth_result",
                        "age_verification_result",
                        "payment_result"
                    ];
                    if (acceptedTypes.indexOf((data.type || "").toLowerCase()) === -1) {
                        console.log(`${LOG_TAG} ignored non-result message`, {
                            windowId: windowId,
                            type: data.type
                        });
                        return;
                    }

                    var payload = JSON.stringify(data);

                    console.log(`${LOG_TAG} payload prepared`, {
                        windowId: windowId,
                        payload: payload
                    });

                    var dataPtr = stringToNewUTF8(payload);
                    try {
                        console.log(`${LOG_TAG} invoking C# callback`, {
                            windowId: windowId,
                            listenerPtr: listenerPtr,
                            dataPtr: dataPtr
                        });

                        {{{ makeDynCall('vi', 'listenerPtr') }}}(dataPtr);

                        console.log(`${LOG_TAG} C# callback finished successfully`, {
                            windowId: windowId
                        });
                    } finally {
                        _free(dataPtr);
                        console.log(`${LOG_TAG} freed dataPtr`, {
                            windowId: windowId
                        });
                    }
                } catch (e) {
                    console.error(`${LOG_TAG} failed to process message`, e);
                }
            };

            utils.WindowData.addListener(windowId, listener);
            console.log(`${LOG_TAG} listener added -> id=${windowId}`);

            if (typeof utils.DebugDump === "function") {
                utils.DebugDump();
            }

            return windowId;
        } catch (e) {
            console.error("[Enjoylix][ExternalService][JSLIB][OpenWindow] fatal error", e);
            return -1;
        }
    },

    Enjoylix_IsWindowClosed: function (windowId) {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][IsWindowClosed]";

        try {
            console.log(`${LOG_TAG} called -> id=${windowId}`, {
                hasUtils: !!window.EnjoylixWebExternalServiceUtils
            });

            if (!window.EnjoylixWebExternalServiceUtils) {
                console.warn(`${LOG_TAG} EnjoylixWebExternalServiceUtils missing, treat as closed`);
                return 1;
            }

            const result = window.EnjoylixWebExternalServiceUtils.WindowData.checkWindowClosed(windowId) ? 1 : 0;
            console.log(`${LOG_TAG} result -> ${result}`);
            return result;
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
            return 1;
        }
    },

    Enjoylix_CloseWindow: function (windowId) {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][CloseWindow]";

        try {
            console.log(`${LOG_TAG} called -> id=${windowId}`, {
                hasUtils: !!window.EnjoylixWebExternalServiceUtils
            });

            if (!window.EnjoylixWebExternalServiceUtils) {
                console.warn(`${LOG_TAG} EnjoylixWebExternalServiceUtils missing`);
                return;
            }

            window.EnjoylixWebExternalServiceUtils.CloseWindow(windowId);
            console.log(`${LOG_TAG} CloseWindow forwarded successfully`);
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
        }
    },

    Enjoylix_FreeMemory: function (ptr) {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][FreeMemory]";

        try {
            console.log(`${LOG_TAG} freeing ptr=${ptr}`);
            _free(ptr);
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
        }
    },

    Enjoylix_IsEmbeddedInIframe: function () {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][IsEmbeddedInIframe]";
        try {
            // window.top throws in a sandboxed frame without allow-same-origin; treat that as embedded.
            let embedded;
            try {
                embedded = window.self !== window.top;
            } catch (_) {
                embedded = true;
            }
            console.log(`${LOG_TAG} embedded=${embedded}`);
            return embedded ? 1 : 0;
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
            return 0;
        }
    },

    Enjoylix_GetAncestorOrigins: function () {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][GetAncestorOrigins]";
        try {
            // Chromium/WebKit only; Firefox has no location.ancestorOrigins — returns "" there.
            const list = window.location.ancestorOrigins;
            const origins = list && list.length ? Array.prototype.join.call(list, " ") : "";
            console.log(`${LOG_TAG} origins="${origins}"`);
            return stringToNewUTF8(origins);
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
            return 0;
        }
    },

    Enjoylix_GetDocumentReferrer: function () {
        const LOG_TAG = "[Enjoylix][ExternalService][JSLIB][GetDocumentReferrer]";
        try {
            console.log(`${LOG_TAG} called`);
            const referrer = document.referrer || "";
            const dataPtr = stringToNewUTF8(referrer);
            console.log(`${LOG_TAG} returning ptr=${dataPtr}, length=${referrer.length}`);
            return dataPtr;
        } catch (e) {
            console.error(`${LOG_TAG} fatal error`, e);
            return 0;
        }
    }
};

mergeInto(LibraryManager.library, LibraryEnjoylixWindowManager);