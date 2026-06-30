using Smartie.Application.Abstractions;

namespace Smartie.Application.Services;

public static class CommandCatalog
{
    public static IReadOnlyList<PaletteCommandDefinition> GetStaticCommands() =>
    [
        // Chat
        new("chat.new", "New Chat", "Chat", "plus", null,
            ["new", "chat", "conversation", "start"], "/chat", true),
        new("chat.search", "Search Conversations", "Chat", "search", null,
            ["search", "conversations", "find", "chat"], "/chat?conversations=1", true),

        // Knowledge Base
        new("knowledge.open", "Open Knowledge Base", "Knowledge Base", "knowledge", null,
            ["knowledge", "base", "kb", "documents", "library"], "/knowledge", true),
        new("knowledge.upload", "Upload Document", "Knowledge Base", "upload", "Ctrl+U",
            ["upload", "document", "doc", "file", "add"], "/knowledge?upload=1", true),
        new("knowledge.search", "Search Documents", "Knowledge Base", "search", null,
            ["search", "documents", "doc", "find", "knowledge"], "/knowledge?search=1", true),
        new("knowledge.rebuild-embeddings", "Rebuild Embeddings", "Knowledge Base", "refresh", null,
            ["rebuild", "embeddings", "embedding", "reindex", "vectors"], "/knowledge", true),

        // Memory
        new("memory.search", "Search Memory", "Memory", "search", null,
            ["search", "memory", "memories", "find", "recall"], "/memory?search=1", true),
        new("memory.pinned", "Pinned Memories", "Memory", "pin", null,
            ["pinned", "memory", "memories", "pin", "favorite"], "/memory?pinned=1", true),
        new("memory.recent", "Recent Memories", "Memory", "memory", null,
            ["recent", "memory", "memories", "latest"], "/memory", true),

        // Tasks
        new("tasks.create", "Create Task", "Tasks", "plus", null,
            ["create", "task", "tasks", "todo", "reminder"], "/tasks", true),
        new("tasks.view", "View Tasks", "Tasks", "tasks", null,
            ["view", "task", "tasks", "list", "todos"], "/tasks", true),
        new("tasks.completed", "Completed Tasks", "Tasks", "check", null,
            ["completed", "done", "task", "tasks", "finished"], "/tasks?filter=Completed", true),

        // Settings
        new("settings.providers", "Providers", "Settings", "settings", null,
            ["providers", "ai", "api", "keys", "models", "settings"], "/settings", true),
        new("settings.themes", "Themes", "Settings", "theme", null,
            ["theme", "themes", "appearance", "dark", "light", "settings"], "/settings#appearance", true),
        new("settings.developer", "Developer Settings", "Settings", "command", null,
            ["developer", "dev", "debug", "diagnostics", "settings"], "/settings", true),
        new("settings.edition", "Community Edition", "Settings", "info", null,
            ["community", "edition", "local", "about", "settings"], "/about", true),

        // Navigation
        new("nav.dashboard", "Dashboard", "Navigation", "dashboard", null,
            ["dashboard", "home", "start"], "/", true),
        new("nav.chat", "Chat", "Navigation", "chat", null,
            ["chat", "conversation", "messages"], "/chat", true),
        new("nav.knowledge", "Knowledge Base", "Navigation", "knowledge", null,
            ["knowledge", "base", "kb", "documents"], "/knowledge", true),
        new("nav.memory", "Memory", "Navigation", "memory", null,
            ["memory", "memories", "recall"], "/memory", true),
        new("nav.tasks", "Tasks", "Navigation", "tasks", null,
            ["tasks", "todo", "todos"], "/tasks", true),
        new("nav.settings", "Settings", "Navigation", "settings", null,
            ["settings", "preferences", "config"], "/settings", true),

        // Future
        new("plugins.open", "Plugin Manager", "Plugins", "plugins", null,
            ["plugins", "extensions", "addons", "manager"], "/plugins", true),
        new("automations.open", "Automation", "Automations", "automations", null,
            ["automation", "automations", "workflow", "trigger"], "/automations", true),
        new("automations.create", "Create Automation", "Automations", "plus", null,
            ["create", "automation", "workflow", "new"], "/automations?create=1", true),
        new("nav.files", "Files", "Navigation", "files", null,
            ["files", "file", "browser", "explorer"], "/files", true),
    ];
}
