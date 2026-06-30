window.smartieTheme = (function () {
    const cacheKey = "smartie-appearance-cache";

    function applyEntries(entries) {
        if (!entries || !entries.length) {
            return;
        }

        const root = document.documentElement;
        for (const entry of entries) {
            const name = entry.name || entry[0];
            const value = entry.value || entry[1];
            if (!name) {
                continue;
            }

            if (name.startsWith("data-")) {
                root.setAttribute(name, value);
            } else if (name.startsWith("--")) {
                root.style.setProperty(name, value);
            }
        }

        try {
            localStorage.setItem(cacheKey, JSON.stringify(entries));
        } catch {
            // Ignore storage failures in private browsing.
        }
    }

    function applyCached() {
        try {
            const raw = localStorage.getItem(cacheKey);
            if (!raw) {
                return;
            }

            applyEntries(JSON.parse(raw));
        } catch {
            // Ignore malformed cache.
        }
    }

    return {
        apply: applyEntries,
        applyCached: applyCached
    };
})();
