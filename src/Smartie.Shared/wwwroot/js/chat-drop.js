// Drag-and-drop upload for the Chat page (browser / Web hosts).
window.smartieChatDrop = {
    _zones: new Map(),

    init: function (elementId, dotNetRef, options) {
        this.dispose(elementId);

        var element = document.getElementById(elementId);
        if (!element || !dotNetRef) {
            return;
        }

        var state = {
            depth: 0,
            allowed: (options.allowedExtensions || []).map(function (e) {
                return e.toLowerCase();
            }),
            maxFileSizeBytes: options.maxFileSizeBytes || 52428800,
            conversationId: options.conversationId || null,
            apiBaseUrl: (options.apiBaseUrl || "").replace(/\/$/, "")
        };

        function setDragActive(active) {
            element.classList.toggle("chat-drag-active", !!active);
        }

        function onEnter(e) {
            if (!hasFiles(e)) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();
            state.depth++;
            if (state.depth === 1) {
                setDragActive(true);
            }
        }

        function onLeave(e) {
            if (!hasFiles(e)) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();
            state.depth--;
            if (state.depth <= 0) {
                state.depth = 0;
                setDragActive(false);
            }
        }

        function onOver(e) {
            if (!hasFiles(e)) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();
            if (e.dataTransfer) {
                e.dataTransfer.dropEffect = "copy";
            }
        }

        async function onDrop(e) {
            e.preventDefault();
            e.stopPropagation();
            state.depth = 0;
            setDragActive(false);

            if (!state.conversationId || !state.apiBaseUrl) {
                await dotNetRef.invokeMethodAsync("HandleDropErrorAsync", "Start or select a conversation before attaching files.");
                return;
            }

            var files = Array.from(e.dataTransfer && e.dataTransfer.files ? e.dataTransfer.files : []);
            if (files.length === 0) {
                return;
            }

            var rejected = [];
            var toUpload = [];

            for (var i = 0; i < files.length; i++) {
                var file = files[i];
                var name = file.name || "unknown";
                var dot = name.lastIndexOf(".");
                var ext = dot >= 0 ? name.slice(dot).toLowerCase() : "";

                if (state.allowed.indexOf(ext) < 0) {
                    rejected.push(name);
                    continue;
                }

                if (file.size > state.maxFileSizeBytes) {
                    rejected.push(name + " (too large)");
                    continue;
                }

                toUpload.push(file);
            }

            if (rejected.length > 0) {
                await dotNetRef.invokeMethodAsync(
                    "HandleDropErrorAsync",
                    rejected.length === 1
                        ? "\"" + rejected[0] + "\" is not supported."
                        : rejected.length + " files were skipped (unsupported or too large).");
            }

            for (var j = 0; j < toUpload.length; j++) {
                try {
                    var staged = await uploadStagingFile(toUpload[j], state.apiBaseUrl, state.conversationId);
                    await dotNetRef.invokeMethodAsync("HandleStagedAttachmentAsync", staged);
                } catch (err) {
                    var message = err && err.message ? err.message : "Upload failed.";
                    await dotNetRef.invokeMethodAsync("HandleDropErrorAsync", message);
                    break;
                }
            }
        }

        function onDocumentDragOver(e) {
            if (!element.isConnected || !hasFiles(e)) {
                return;
            }

            e.preventDefault();
        }

        element.addEventListener("dragenter", onEnter);
        element.addEventListener("dragleave", onLeave);
        element.addEventListener("dragover", onOver);
        element.addEventListener("drop", onDrop);
        document.addEventListener("dragover", onDocumentDragOver);

        this._zones.set(elementId, {
            dispose: function () {
                element.removeEventListener("dragenter", onEnter);
                element.removeEventListener("dragleave", onLeave);
                element.removeEventListener("dragover", onOver);
                element.removeEventListener("drop", onDrop);
                document.removeEventListener("dragover", onDocumentDragOver);
                setDragActive(false);
            },
            updateOptions: function (nextOptions) {
                state.allowed = (nextOptions.allowedExtensions || []).map(function (e) {
                    return e.toLowerCase();
                });
                state.maxFileSizeBytes = nextOptions.maxFileSizeBytes || 52428800;
                state.conversationId = nextOptions.conversationId || null;
                state.apiBaseUrl = (nextOptions.apiBaseUrl || "").replace(/\/$/, "");
            }
        });
    },

    updateOptions: function (elementId, options) {
        var zone = this._zones.get(elementId);
        if (zone && zone.updateOptions) {
            zone.updateOptions(options);
        }
    },

    dispose: function (elementId) {
        var zone = this._zones.get(elementId);
        if (zone) {
            zone.dispose();
            this._zones.delete(elementId);
        }
    }
};

function hasFiles(e) {
    if (!e.dataTransfer) {
        return false;
    }

    var types = e.dataTransfer.types ? Array.from(e.dataTransfer.types) : [];
    for (var i = 0; i < types.length; i++) {
        var type = String(types[i]).toLowerCase();
        if (type === "files" || type === "application/x-moz-file") {
            return true;
        }
    }

    if (e.dataTransfer.items && e.dataTransfer.items.length > 0) {
        for (var j = 0; j < e.dataTransfer.items.length; j++) {
            if (e.dataTransfer.items[j].kind === "file") {
                return true;
            }
        }
    }

    return types.length === 0;
}

async function uploadStagingFile(file, apiBaseUrl, conversationId) {
    var formData = new FormData();
    formData.append("file", file, file.name);

    var response = await fetch(
        apiBaseUrl + "/api/conversations/" + conversationId + "/attachments/staging",
        {
            method: "POST",
            body: formData
        });

    if (!response.ok) {
        var message = "Upload failed.";
        try {
            message = await response.text();
        } catch (ignored) {
        }

        throw new Error(message || "Upload failed.");
    }

    return await response.json();
}
