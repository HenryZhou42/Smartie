namespace Smartie.Infrastructure.Storage;

/// <summary>Local filesystem paths for Smartie Community Edition data under %LOCALAPPDATA%/Smartie.</summary>
public static class SmartiePaths
{
    public static string AppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartie");

    /// <summary>Creates the standard Community Edition folder layout on first launch.</summary>
    public static void EnsureAppDataLayout()
    {
        Directory.CreateDirectory(AppDataRoot);
        MigrateLegacyDocumentsFolder();

        _ = KnowledgeBaseRoot;
        _ = ChatAttachmentsRoot;
        _ = MemoryRoot;
        _ = TasksRoot;
        _ = PluginsRoot;
        _ = LogsRoot;
        _ = CacheRoot;
        _ = AutomationsExportRoot;

        var dbPath = DefaultDatabasePath();
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }
    }

    private static void MigrateLegacyDocumentsFolder()
    {
        var legacy = Path.Combine(AppDataRoot, "Documents");
        var knowledgeBase = Path.Combine(AppDataRoot, "KnowledgeBase");
        if (Directory.Exists(legacy) && !Directory.Exists(knowledgeBase))
        {
            try
            {
                Directory.Move(legacy, knowledgeBase);
            }
            catch
            {
                // If move fails (e.g. files in use), continue using legacy path via fallback below.
            }
        }
    }

    public static string DefaultDatabasePath()
    {
        Directory.CreateDirectory(AppDataRoot);
        return Path.Combine(AppDataRoot, "smartie.db");
    }

    public static string KnowledgeBaseRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "KnowledgeBase");
            if (!Directory.Exists(path))
            {
                var legacy = Path.Combine(AppDataRoot, "Documents");
                if (Directory.Exists(legacy))
                {
                    return legacy;
                }
            }

            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string DocumentsRoot => KnowledgeBaseRoot;

    public static string GetDocumentDirectory(Guid documentId)
    {
        var path = Path.Combine(DocumentsRoot, documentId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetAbsolutePath(string relativePath) =>
        Path.Combine(DocumentsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string ChatAttachmentsRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "ChatAttachments");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string GetChatAttachmentDirectory(Guid conversationId, Guid folderId)
    {
        var path = Path.Combine(ChatAttachmentsRoot, conversationId.ToString("N"), folderId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetChatAttachmentAbsolutePath(string relativePath) =>
        Path.Combine(ChatAttachmentsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public static string MemoryRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "Memory");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string TasksRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "Tasks");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string PluginsRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "Plugins");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string LogsRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "Logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string CacheRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "Cache");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string AutomationsExportRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "AutomationExports");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
