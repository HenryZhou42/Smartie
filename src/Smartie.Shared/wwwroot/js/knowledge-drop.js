// Drag-and-drop upload for the Knowledge Base page.
window.smartieKnowledgeDrop = {
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
            apiBaseUrl: (options.apiBaseUrl || "").replace(/\/$/, "")
        };

        function setDragActive(active) {
            if (active) {
                element.classList.add("knowledge-drag-active");
            } else {
                element.classList.remove("knowledge-drag-active");
            }
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

            var files = Array.from(e.dataTransfer && e.dataTransfer.files ? e.dataTransfer.files : []);
            if (files.length === 0) {
                return;
            }

            var unsupported = [];
            var tooLarge = [];
            var toUpload = [];

            for (var i = 0; i < files.length; i++) {
                var file = files[i];
                var name = file.name || "unknown";
                var dot = name.lastIndexOf(".");
                var ext = dot >= 0 ? name.slice(dot).toLowerCase() : "";

                if (state.allowed.indexOf(ext) < 0) {
                    unsupported.push(name);
                    continue;
                }

                if (file.size > state.maxFileSizeBytes) {
                    tooLarge.push(name);
                    continue;
                }

                toUpload.push(file);
            }

            var uploadedCount = 0;
            var errorMessage = null;

            if (toUpload.length > 0) {
                if (!state.apiBaseUrl) {
                    errorMessage = "Smartie API is not ready yet. Try again in a moment.";
                } else {
                    for (var j = 0; j < toUpload.length; j++) {
                        try {
                            await uploadFile(toUpload[j], state.apiBaseUrl);
                            uploadedCount++;
                        } catch (err) {
                            errorMessage = err && err.message ? err.message : "Upload failed.";
                            break;
                        }
                    }
                }
            }

            if (uploadedCount > 0 || unsupported.length > 0 || tooLarge.length > 0 || errorMessage) {
                await dotNetRef.invokeMethodAsync("HandleDropUploadResultAsync", {
                    uploadedCount: uploadedCount,
                    unsupportedFiles: unsupported,
                    tooLargeFiles: tooLarge,
                    error: errorMessage
                });
            }
        }

        // Document-level dragover is required for WebView2 / Windows drops to work.
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
    },

    setDragActive: function (elementId, active) {
        var element = document.getElementById(elementId);
        if (!element) {
            return;
        }

        if (active) {
            element.classList.add("knowledge-drag-active");
        } else {
            element.classList.remove("knowledge-drag-active");
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

    // WebView2 / Explorer sometimes omits types until drop; allow drag if items exist.
    if (e.dataTransfer.items && e.dataTransfer.items.length > 0) {
        for (var j = 0; j < e.dataTransfer.items.length; j++) {
            if (e.dataTransfer.items[j].kind === "file") {
                return true;
            }
        }
    }

    return types.length === 0;
}

async function uploadFile(file, apiBaseUrl) {
    var formData = new FormData();
    formData.append("file", file, file.name);

    var response = await fetch(apiBaseUrl + "/api/documents/upload", {
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
}
