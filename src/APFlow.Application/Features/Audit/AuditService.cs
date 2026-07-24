using APFlow.Application.Common;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Audit;

/// <summary>
/// Default implementation of <see cref="IAuditService"/>. Depends only on
/// <see cref="IAuditLogRepository"/> (a plain, EF-Core-free interface), so this
/// class is fully unit-testable with a fake repository - no database, no EF Core
/// provider required.
/// </summary>
public sealed class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;
    private readonly ILogger<AuditService> _logger;

    /// <summary>Creates a new <see cref="AuditService"/>.</summary>
    public AuditService(IAuditLogRepository repository, ILogger<AuditService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> LogAsync(RecordAuditLogRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result.Failure<Guid>(validationError);
        }

        var auditLog = new AuditLog
        {
            Action = request.Action,
            EntityName = request.EntityName,
            EntityId = request.EntityId,
            PreviousValue = request.PreviousValue,
            NewValue = request.NewValue,
        };

        await _repository.AddAsync(auditLog, cancellationToken);

        // Deliberately no SaveChangesAsync here - see IAuditService.LogAsync's doc
        // comment and docs/WP-013-Audit-Logging-Decisions.md. The caller commits
        // this staged entry together with whatever change it describes.
        _logger.LogInformation(
            "Staged audit log entry {AuditLogId}: {Action} on {EntityName} {EntityId}.",
            auditLog.Id, auditLog.Action, auditLog.EntityName, auditLog.EntityId);

        return Result.Success(auditLog.Id);
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> LogAndSaveAsync(RecordAuditLogRequest request, CancellationToken cancellationToken = default)
    {
        var result = await LogAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>Validates required fields before anything touches the repository, mirroring every other service's validate-before-repository pattern.</summary>
    private static Error? Validate(RecordAuditLogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return new Error("AuditLog.InvalidAction", "Action must not be empty.");
        }

        if (request.Action.Length > FieldLimits.AuditLogAction)
        {
            return new Error("AuditLog.InvalidAction", $"Action must not exceed {FieldLimits.AuditLogAction} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.EntityName))
        {
            return new Error("AuditLog.InvalidEntityName", "EntityName must not be empty.");
        }

        if (request.EntityName.Length > FieldLimits.AuditLogEntityName)
        {
            return new Error("AuditLog.InvalidEntityName", $"EntityName must not exceed {FieldLimits.AuditLogEntityName} characters.");
        }

        if (request.EntityId == Guid.Empty)
        {
            return new Error("AuditLog.InvalidEntityId", "EntityId must not be an empty guid.");
        }

        if (request.PreviousValue is { Length: > 0 } && request.PreviousValue.Length > FieldLimits.AuditLogValue)
        {
            return new Error("AuditLog.InvalidPreviousValue", $"PreviousValue must not exceed {FieldLimits.AuditLogValue} characters.");
        }

        if (request.NewValue is { Length: > 0 } && request.NewValue.Length > FieldLimits.AuditLogValue)
        {
            return new Error("AuditLog.InvalidNewValue", $"NewValue must not exceed {FieldLimits.AuditLogValue} characters.");
        }

        return null;
    }
}
