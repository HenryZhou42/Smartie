using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

/// <summary>
/// EF Core context for Smartie. SQLite-backed in Phase 1; the model is shaped to
/// grow (tasks, documents, memories, ...) as modules arrive.
/// </summary>
public sealed class SmartieDbContext : DbContext
{
    public SmartieDbContext(DbContextOptions<SmartieDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<AiProviderCredential> AiProviderCredentials => Set<AiProviderCredential>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    public DbSet<Memory> Memories => Set<Memory>();

    public DbSet<RecentCommand> RecentCommands => Set<RecentCommand>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    public DbSet<RecentFile> RecentFiles => Set<RecentFile>();

    public DbSet<FavoriteFolder> FavoriteFolders => Set<FavoriteFolder>();

    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    public DbSet<PluginInstallation> PluginInstallations => Set<PluginInstallation>();

    public DbSet<PluginLogEntry> PluginLogEntries => Set<PluginLogEntry>();

    public DbSet<AutomationRule> Automations => Set<AutomationRule>();

    public DbSet<AutomationRunLog> AutomationRunLogs => Set<AutomationRunLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot ORDER BY a DateTimeOffset; persist as UTC ticks (INTEGER),
        // which sorts correctly and round-trips since we always store UTC values.
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.SelectedAiProvider).IsRequired().HasMaxLength(64).HasDefaultValue("google");
            entity.Property(u => u.MemoryEnabled).HasDefaultValue(true);
            entity.Property(u => u.MaxMemories).HasDefaultValue(200);
            entity.Property(u => u.MemoryRetentionDays).HasDefaultValue(365);
            entity.Property(u => u.TaskDefaultSort).HasConversion<string>().HasMaxLength(32).HasDefaultValue(TaskSortOption.DueDate);
            entity.Property(u => u.TaskDefaultPriority).HasConversion<string>().HasMaxLength(16).HasDefaultValue(TaskPriority.Medium);
            entity.Property(u => u.TaskShowCompleted).HasDefaultValue(true);
            entity.Property(u => u.FileMaxRecentFiles).HasDefaultValue(50);
            entity.Property(u => u.FileShowHiddenFiles).HasDefaultValue(false);
            entity.Property(u => u.HasCompletedOnboarding).HasDefaultValue(false);
        });

        modelBuilder.Entity<AiProviderCredential>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Provider).IsRequired().HasMaxLength(64);
            entity.Property(c => c.ChatModel).HasMaxLength(128);
            entity.Property(c => c.Endpoint).HasMaxLength(512);
            entity.HasIndex(c => new { c.UserId, c.Provider }).IsUnique();

            entity.HasOne(c => c.User)
                .WithMany(u => u.AiProviderCredentials)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(512);
            entity.HasIndex(c => c.UserId);
            entity.Property(c => c.IsPinned).HasDefaultValue(false);

            entity.HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation!)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(m => m.GenerationStatus).HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.IsEdited).HasDefaultValue(false);
            entity.HasIndex(m => m.ConversationId);

            entity.HasMany(m => m.Attachments)
                .WithOne(a => a.Message!)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.MessageId);
            entity.HasIndex(a => a.ConversationId);
            entity.HasIndex(a => a.DocumentId);

            entity.Property(a => a.OriginalFileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.StoredFileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.FilePath).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.Extension).IsRequired().HasMaxLength(32);
            entity.Property(a => a.SourceType).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(a => a.Document)
                .WithMany()
                .HasForeignKey(a => a.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Conversation)
                .WithMany()
                .HasForeignKey(a => a.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(512);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(512);
            entity.Property(d => d.Extension).IsRequired().HasMaxLength(32);
            entity.Property(d => d.RelativePath).IsRequired().HasMaxLength(1024);
            entity.Property(d => d.IsIndexed).HasDefaultValue(false);
            entity.Property(d => d.TagCount).HasDefaultValue(0);
            entity.Property(d => d.ExtractionStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(DocumentExtractionStatus.Pending);
            entity.Property(d => d.ExtractorUsed).HasMaxLength(128);
            entity.Property(d => d.ExtractionError).HasMaxLength(2048);
            entity.Property(d => d.IsChunked).HasDefaultValue(false);
            entity.Property(d => d.ChunkCount).HasDefaultValue(0);
            entity.Property(d => d.IsEmbedded).HasDefaultValue(false);
            entity.Property(d => d.EmbeddedChunkCount).HasDefaultValue(0);
            entity.Property(d => d.EmbeddingModel).HasMaxLength(128);
            entity.Property(d => d.IsSample).HasDefaultValue(false);
            entity.HasIndex(d => d.UserId);
            entity.HasIndex(d => d.UploadedAt);

            entity.HasMany(d => d.Chunks)
                .WithOne(c => c.Document!)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.DocumentId);
            entity.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique();
            entity.Property(c => c.Content).IsRequired();
            entity.Property(c => c.EmbeddingStatus)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(ChunkEmbeddingStatus.Pending);
            entity.Property(c => c.EmbeddingModel).HasMaxLength(128);
        });

        modelBuilder.Entity<Memory>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.HasIndex(m => m.UserId);
            entity.HasIndex(m => new { m.UserId, m.UpdatedAt });
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(m => m.Importance).HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.EmbeddingModel).HasMaxLength(128);
            entity.Property(m => m.Pinned).HasDefaultValue(false);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecentCommand>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.UserId, c.CommandName }).IsUnique();
            entity.HasIndex(c => new { c.UserId, c.LastUsed });
            entity.Property(c => c.CommandName).IsRequired().HasMaxLength(128);

            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("Tasks");
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => new { t.UserId, t.UpdatedAt });
            entity.HasIndex(t => new { t.UserId, t.DueDate });
            entity.Property(t => t.Title).IsRequired().HasMaxLength(512);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);
            entity.Property(t => t.Pinned).HasDefaultValue(false);
            entity.Property(t => t.Archived).HasDefaultValue(false);

            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecentFile>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => f.UserId);
            entity.HasIndex(f => new { f.UserId, f.FilePath }).IsUnique();
            entity.HasIndex(f => new { f.UserId, f.LastOpenedAt });
            entity.Property(f => f.FilePath).IsRequired();
            entity.Property(f => f.FileName).IsRequired().HasMaxLength(512);
            entity.Property(f => f.Extension).IsRequired().HasMaxLength(32);
            entity.Property(f => f.Location).IsRequired();
            entity.Property(f => f.Pinned).HasDefaultValue(false);
            entity.Property(f => f.IsFavorite).HasDefaultValue(false);

            entity.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FavoriteFolder>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.UserId, f.FolderPath }).IsUnique();
            entity.Property(f => f.FolderPath).IsRequired();
            entity.Property(f => f.Label).IsRequired().HasMaxLength(256);

            entity.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.UserId).IsUnique();
            entity.Property(p => p.Theme).IsRequired().HasMaxLength(32).HasDefaultValue("Default");
            entity.Property(p => p.AccentColor).IsRequired().HasMaxLength(32).HasDefaultValue("Purple");
            entity.Property(p => p.CustomAccentHex).HasMaxLength(16);
            entity.Property(p => p.Density).IsRequired().HasMaxLength(32).HasDefaultValue("Default");
            entity.Property(p => p.FontSize).IsRequired().HasMaxLength(16).HasDefaultValue("Medium");
            entity.Property(p => p.SidebarMode).IsRequired().HasMaxLength(32).HasDefaultValue("Expanded");
            entity.Property(p => p.AnimationMode).IsRequired().HasMaxLength(32).HasDefaultValue("Enabled");
            entity.Property(p => p.WindowEffect).IsRequired().HasMaxLength(32).HasDefaultValue("Disabled");
            entity.Property(p => p.BubbleRadius).IsRequired().HasMaxLength(16).HasDefaultValue("Medium");
            entity.Property(p => p.BubbleWidth).IsRequired().HasMaxLength(16).HasDefaultValue("Standard");
            entity.Property(p => p.MessageSpacing).IsRequired().HasMaxLength(16).HasDefaultValue("Normal");
            entity.Property(p => p.CodeBlockTheme).IsRequired().HasMaxLength(32).HasDefaultValue("Default");
            entity.Property(p => p.MarkdownTheme).IsRequired().HasMaxLength(32).HasDefaultValue("Default");
            entity.Property(p => p.TypingSpeedMs).HasDefaultValue(20);
            entity.Property(p => p.TransitionSpeedMs).HasDefaultValue(200);
            entity.Property(p => p.TransparencyEnabled).HasDefaultValue(false);

            entity.HasOne(p => p.User)
                .WithOne(u => u.Preferences)
                .HasForeignKey<UserPreferences>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PluginInstallation>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.UserId, p.PluginKey }).IsUnique();
            entity.Property(p => p.PluginKey).IsRequired().HasMaxLength(128);
            entity.Property(p => p.FolderName).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Description).IsRequired();
            entity.Property(p => p.Version).IsRequired().HasMaxLength(32);
            entity.Property(p => p.Author).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Category).IsRequired().HasMaxLength(64);
            entity.Property(p => p.EntryAssembly).IsRequired().HasMaxLength(256);
            entity.Property(p => p.Enabled).HasDefaultValue(true);
            entity.Property(p => p.IsLoaded).HasDefaultValue(false);

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PluginLogEntry>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.PluginInstallationId, l.CreatedAt });
            entity.Property(l => l.Level).IsRequired().HasMaxLength(16);
            entity.Property(l => l.Message).IsRequired();

            entity.HasOne(l => l.PluginInstallation)
                .WithMany(p => p.Logs)
                .HasForeignKey(l => l.PluginInstallationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AutomationRule>(entity =>
        {
            entity.ToTable("Automations");
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.UserId, a.Enabled });
            entity.HasIndex(a => new { a.UserId, a.NextRun });
            entity.Property(a => a.Name).IsRequired().HasMaxLength(256);
            entity.Property(a => a.Description).IsRequired();
            entity.Property(a => a.TriggerType).HasConversion<string>().HasMaxLength(64);
            entity.Property(a => a.ActionType).HasConversion<string>().HasMaxLength(64);
            entity.Property(a => a.ConfigJson).IsRequired();
            entity.Property(a => a.Enabled).HasDefaultValue(true);
            entity.Property(a => a.RunCount).HasDefaultValue(0);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AutomationRunLog>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.AutomationRuleId, l.StartedAt });
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(l => l.Message).IsRequired();

            entity.HasOne(l => l.AutomationRule)
                .WithMany(a => a.RunLogs)
                .HasForeignKey(l => l.AutomationRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>Stores a <see cref="DateTimeOffset"/> as UTC ticks so SQLite can order by it.</summary>
internal sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToTicksConverter()
        : base(value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}
