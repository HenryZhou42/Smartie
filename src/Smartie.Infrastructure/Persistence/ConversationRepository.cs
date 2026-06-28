using Microsoft.EntityFrameworkCore;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IConversationRepository"/>.
/// </summary>
public sealed class ConversationRepository : IConversationRepository
{
    private readonly SmartieDbContext _db;

    public ConversationRepository(SmartieDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Conversation>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.Conversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.IsPinned ? (c.PinnedAt ?? c.UpdatedAt) : c.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Conversation?> FindAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        await _db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
                .ThenInclude(m => m.Attachments)
                    .ThenInclude(a => a.Document)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<Conversation> CreateAsync(Guid userId, string title, CancellationToken cancellationToken = default)
    {
        var conversation = new Conversation { UserId = userId, Title = title };
        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return conversation;
    }

    public async Task<Message> AddMessageAsync(
        Guid conversationId,
        MessageRole role,
        string content,
        CancellationToken cancellationToken = default,
        MessageGenerationStatus generationStatus = MessageGenerationStatus.Complete)
    {
        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            Role = role,
            Content = content,
            GenerationStatus = role == MessageRole.Assistant ? generationStatus : MessageGenerationStatus.Complete,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Messages.Add(message);

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is not null)
        {
            conversation.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return message;
    }

    public async Task UpdateTitleAsync(Guid conversationId, string title, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        conversation.Title = title;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetPinnedAsync(Guid conversationId, bool isPinned, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        conversation.IsPinned = isPinned;
        conversation.PinnedAt = isPinned ? now : null;
        conversation.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Message?> EditUserMessageAndTruncateAsync(
        Guid conversationId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var index = messages.FindIndex(m => m.Id == messageId);
        if (index < 0)
        {
            return null;
        }

        var target = messages[index];
        if (target.Role != MessageRole.User)
        {
            throw new InvalidOperationException("Only user messages can be edited.");
        }

        var now = DateTimeOffset.UtcNow;
        target.Content = newContent.Trim();
        target.IsEdited = true;
        target.EditedAt = now;
        target.UpdatedAt = now;

        var toRemove = messages.Skip(index + 1).ToList();
        if (toRemove.Count > 0)
        {
            _db.Messages.RemoveRange(toRemove);
        }

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is not null)
        {
            conversation.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return target;
    }

    public async Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (conversation is null)
        {
            return false;
        }

        _db.Conversations.Remove(conversation);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
