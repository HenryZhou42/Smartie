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

    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

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
            entity.HasIndex(d => d.UserId);
            entity.HasIndex(d => d.UploadedAt);

            entity.HasOne(d => d.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(d => d.UserId)
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
