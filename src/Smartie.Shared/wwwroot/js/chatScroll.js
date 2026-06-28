// ChatGPT-style scroll manager for Smartie Chat (Blazor Hybrid + Web).
window.smartieChatScroll = (function () {
    const handlers = new Map();
    var debugScroll = false;

    function getElement(containerId) {
        return document.getElementById(containerId);
    }

    function scrollToBottom(containerId, smooth) {
        var el = getElement(containerId);
        if (!el) {
            if (debugScroll) {
                console.warn("smartieChatScroll: container not found", containerId);
            }
            return;
        }

        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                el.scrollTo({
                    top: el.scrollHeight,
                    behavior: smooth ? "smooth" : "auto"
                });

                if (debugScroll) {
                    console.log("smartieChatScroll: scrolling", {
                        containerId: containerId,
                        scrollTop: el.scrollTop,
                        scrollHeight: el.scrollHeight,
                        clientHeight: el.clientHeight,
                        distanceFromBottom: el.scrollHeight - el.scrollTop - el.clientHeight
                    });
                }
            });
        });
    }

    function isNearBottom(containerId, threshold) {
        threshold = threshold ?? 100;
        var el = getElement(containerId);
        if (!el) {
            return true;
        }

        var distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
        return distanceFromBottom <= threshold;
    }

    function initialize(containerId, dotNetRef, threshold) {
        threshold = threshold ?? 100;
        dispose(containerId);

        var el = getElement(containerId);
        if (!el || !dotNetRef) {
            return;
        }

        var scrollRaf = 0;
        var onScroll = function () {
            if (scrollRaf) {
                return;
            }

            scrollRaf = requestAnimationFrame(function () {
                scrollRaf = 0;
                var distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
                var nearBottom = distanceFromBottom <= threshold;
                dotNetRef.invokeMethodAsync("OnChatScrollChanged", nearBottom);
            });
        };

        el.addEventListener("scroll", onScroll, { passive: true });
        handlers.set(containerId, { el: el, onScroll: onScroll });

        if (debugScroll) {
            console.log("smartieChatScroll: initialized", {
                containerId: containerId,
                clientHeight: el.clientHeight,
                scrollHeight: el.scrollHeight,
                overflowY: window.getComputedStyle(el).overflowY
            });
        }
    }

    function dispose(containerId) {
        var entry = handlers.get(containerId);
        if (!entry) {
            return;
        }

        entry.el.removeEventListener("scroll", entry.onScroll);
        handlers.delete(containerId);
    }

    function setDebug(enabled) {
        debugScroll = !!enabled;
    }

    return {
        scrollToBottom: scrollToBottom,
        isNearBottom: isNearBottom,
        initialize: initialize,
        dispose: dispose,
        setDebug: setDebug
    };
})();
