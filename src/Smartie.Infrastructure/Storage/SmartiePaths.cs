namespace Smartie.Infrastructure.Storage;

/// <summary>Local filesystem paths for Smartie Community Edition data.</summary>
public static class SmartiePaths
{
    public static string AppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartie");

    public static string DefaultDatabasePath()
    {
        Directory.CreateDirectory(AppDataRoot);
        return Path.Combine(AppDataRoot, "smartie.db");
    }

    public static string DocumentsRoot
    {
        get
        {
            var path = Path.Combine(AppDataRoot, "Documents");
            Directory.CreateDirectory(path);
            return path;
        }
    }

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
}
