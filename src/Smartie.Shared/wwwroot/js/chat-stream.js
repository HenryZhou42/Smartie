// Chat helpers (clipboard, open URL, file upload). Scroll: see chatScroll.js.
window.smartieChat = {
    copyText: async function (text) {
        if (!text) {
            return false;
        }
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },
    openUrl: function (url) {
        window.open(url, "_blank", "noopener,noreferrer");
    },
    clickFileInput: function (elementId) {
        var el = document.getElementById(elementId);
        if (el) {
            el.click();
        }
    }
};
