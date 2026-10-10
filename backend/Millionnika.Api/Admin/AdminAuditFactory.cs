using System.Text.Json;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Admin;

public static class AdminAuditFactory
{
    public static AdminAuditLog AdminCreated(Guid adminId, DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        AdminUserId = adminId,
        Action = "admin_created",
        TargetType = "user",
        TargetId = adminId.ToString(),
        OldValue = JsonSerializer.Serialize(new { Exists = false }),
        NewValue = JsonSerializer.Serialize(new { Id = adminId, Role = "Admin" }),
        Reason = "Локальная команда создания администратора",
        CreatedAtUtc = createdAtUtc
    };
}
