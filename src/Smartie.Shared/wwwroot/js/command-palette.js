window.smartieCommandPalette = {
    _handler: null,
    _dotNetRef: null,

    register: function (dotNetRef) {
        this.dispose();
        this._dotNetRef = dotNetRef;

        var handler = function (event) {
            if (!(event.ctrlKey || event.metaKey) || String(event.key).toLowerCase() !== "k") {
                return;
            }

            var target = event.target;
            if (!target) {
                return;
            }

            var tag = String(target.tagName || "").toUpperCase();
            if (target.isContentEditable || tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") {
                return;
            }

            event.preventDefault();
            dotNetRef.invokeMethodAsync("OpenFromShortcut");
        };

        document.addEventListener("keydown", handler, true);
        this._handler = handler;
    },

    dispose: function () {
        if (this._handler) {
            document.removeEventListener("keydown", this._handler, true);
            this._handler = null;
        }

        this._dotNetRef = null;
    },

    focusElement: function (element) {
        if (element && typeof element.focus === "function") {
            element.focus();
            if (typeof element.select === "function") {
                element.select();
            }
        }
    }
};
