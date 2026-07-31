using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APFlow.Workers;

/// <summary>
/// WP-067: the single background worker this solution runs. Polls the one
/// configured Graph mailbox (WP-004's already-accepted single-tenant shape -
/// no per-tenant iteration here; that's explicitly gated behind the future
/// Per-Tenant Graph Configuration work) on a timer and calls
/// <see cref="IInvoiceProcessingService.ProcessUnreadEmailsAsync"/>, the
/// existing, already-correct WP-012 pipeline that was fully built but never
/// actually invoked by anything outside tests until now.
/// A new DI scope is created per cycle: <see cref="IInvoiceProcessingService"/>
/// and everything it depends on (AppDbContext included) are Scoped, while this
/// class itself is a Singleton (standard BackgroundService lifetime) - resolving
/// them once at startup would reuse a single AppDbContext instance forever,
/// which is not how EF Core is meant to be used.
/// </summary>
public sealed class EmailIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkersOptions> options,
    ILogger<EmailIngestionWorker> logger) : BackgroundService
{
    private readonly WorkersOptions _options = options.Value;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.EmailPollingIntervalSeconds);
        logger.LogInformation(
            "EmailIngestionWorker starting - polling every {IntervalSeconds}s",
            _options.EmailPollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOneCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Runs exactly one poll cycle. Deliberately swallows every exception here -
    /// an unhandled exception escaping <see cref="ExecuteAsync"/> would, by
    /// .NET's own default <c>BackgroundServiceExceptionBehavior</c>, stop the
    /// whole host, taking the API down with it. A single bad cycle (a transient
    /// Graph/Document Intelligence/SQL failure) must not do that - log it and
    /// let the next cycle retry, matching the pipeline's own existing per-item
    /// error handling (a failed attachment doesn't abort the rest of the run
    /// either).
    /// </summary>
    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Email ingestion poll cycle starting");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<IInvoiceProcessingService>();
            var result = await processingService.ProcessUnreadEmailsAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogError(
                    "Email ingestion poll cycle failed: {ErrorCode} - {ErrorMessage}",
                    result.Error.Code,
                    result.Error.Message);
                return;
            }

            var summary = result.Value;
            var processed = summary.Items.Count(i => i.Outcome == InvoiceProcessingOutcome.Processed);
            var alreadyProcessed = summary.Items.Count(i => i.Outcome == InvoiceProcessingOutcome.AlreadyProcessed);
            var failed = summary.Items.Count(i => i.Outcome == InvoiceProcessingOutcome.Failed);
            var duplicates = summary.Items.Count(i => i.IsPotentialDuplicate == true);

            logger.LogInformation(
                "Email ingestion poll cycle complete: {EmailsSynced} email(s) synced, " +
                "{EmailsMarkedProcessed} marked processed, {Processed} invoice(s) created, " +
                "{AlreadyProcessed} already processed, {Failed} failed, " +
                "{Duplicates} flagged as potential duplicates",
                summary.EmailsSynced,
                summary.EmailsMarkedProcessed,
                processed,
                alreadyProcessed,
                failed,
                duplicates);

            foreach (var item in summary.Items.Where(i => i.Outcome == InvoiceProcessingOutcome.Failed))
            {
                logger.LogWarning(
                    "Email ingestion: attachment {FileName} on message {MessageId} failed - {ErrorCode}: {ErrorMessage}",
                    item.FileName,
                    item.MessageId,
                    item.ErrorCode,
                    item.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email ingestion poll cycle threw an unhandled exception - will retry next cycle");
        }
    }
}
