using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models;
using SpecimenCheckIn.Models.Errors;

namespace SpecimenCheckIn.Commands.Manifests;

/// <summary>
/// The write side of check-in: what a technician does at the receiving desk.
/// </summary>
/// <remarks>
/// <para>
/// Like the read side, nothing here takes a lab. Loads go through the tenant-filtered
/// context, so a manifest from another lab is simply not found, and writes are stamped
/// with the session's lab by the database.
/// </para>
/// <para>
/// Every action that changes something appends to the audit log in the same SaveChanges as
/// the change itself, so a check-in and its record land together or not at all. Actions
/// that change nothing append nothing: a repeat scan has to leave the system identical,
/// audit log included, or receiving would not actually be idempotent. The log narrates what
/// happened to the manifest, not which buttons were pressed.
/// </para>
/// </remarks>
/// <param name="database">The database.</param>
/// <param name="user">The technician performing the action.</param>
/// <param name="clock">Supplies the current time.</param>
public class CheckInCommands(SpecimenCheckInContext database, IUserContext user, TimeProvider clock)
{
    /// <summary>
    /// Marks a bottle as physically received.
    /// </summary>
    /// <param name="manifestId">The manifest the bottle is listed on.</param>
    /// <param name="specimenId">The bottle.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the check-in is recorded.</returns>
    /// <exception cref="NotFoundException">Thrown when the manifest or bottle is not in this lab.</exception>
    /// <exception cref="RuleViolationException">Thrown when the manifest is already closed.</exception>
    public async Task ReceiveSpecimenAsync(Guid manifestId, Guid specimenId, CancellationToken cancellationToken = default)
    {
        Manifest manifest = await this.LoadManifestAsync(manifestId, cancellationToken);
        manifest.GuardIsOpen();

        Specimen specimen = FindSpecimen(manifest, specimenId);

        DateTime at = clock.GetUtcNow().UtcDateTime;

        if (!specimen.Receive(user.LabTech, at))
        {
            // Already received: the desk scanned it twice. Saying so is not an error, and
            // writing nothing is what keeps the counts from drifting.
            return;
        }

        this.Append(AuditAction.SpecimenReceived, manifest.Id, at, specimen.Id);

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reports a listed bottle as missing, raising a discrepancy.
    /// </summary>
    /// <param name="manifestId">The manifest the bottle is listed on.</param>
    /// <param name="specimenId">The bottle.</param>
    /// <param name="notes">Optional context from the technician.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the discrepancy is raised.</returns>
    /// <exception cref="NotFoundException">Thrown when the manifest or bottle is not in this lab.</exception>
    /// <exception cref="RuleViolationException">Thrown when the manifest is already closed.</exception>
    public async Task FlagSpecimenAsync(
        Guid manifestId,
        Guid specimenId,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        Manifest manifest = await this.LoadManifestAsync(manifestId, cancellationToken);
        manifest.GuardIsOpen();

        Specimen specimen = FindSpecimen(manifest, specimenId);

        if (!specimen.Flag())
        {
            // Already flagged, and an open discrepancy already exists for it.
            return;
        }

        DateTime at = clock.GetUtcNow().UtcDateTime;

        // Reuses an open discrepancy rather than stacking a second one for the same
        // bottle: flagging is a statement about the bottle, not an event log.
        bool alreadyRaised = await database.Discrepancies.AnyAsync(
            discrepancy => discrepancy.SpecimenId == specimenId
                && discrepancy.Status == DiscrepancyStatus.Open,
            cancellationToken);

        if (!alreadyRaised)
        {
            database.Discrepancies.Add(new Discrepancy
            {
                Id = Guid.NewGuid(),
                ManifestId = manifest.Id,
                SpecimenId = specimen.Id,
                Type = DiscrepancyType.Missing,
                Status = DiscrepancyStatus.Open,
                RaisedAt = at,
                Notes = notes,
            });
        }

        this.Append(AuditAction.SpecimenFlagged, manifest.Id, at, specimen.Id);

        // One SaveChanges, so the flag, its discrepancy and the audit entry land together
        // or not at all — a flagged bottle with no discrepancy would be a mismatch nobody
        // is tracking, and a change with no record is one nobody can account for later.
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Closes a manifest, once every bottle has been accounted for.
    /// </summary>
    /// <param name="manifestId">The manifest to close.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the manifest is closed.</returns>
    /// <exception cref="NotFoundException">Thrown when the manifest is not in this lab.</exception>
    /// <exception cref="RuleViolationException">
    /// Thrown when bottles are still pending, the manifest is already closed, or another
    /// technician changed it first.
    /// </exception>
    public async Task CloseManifestAsync(Guid manifestId, CancellationToken cancellationToken = default)
    {
        Manifest manifest = await this.LoadManifestAsync(manifestId, cancellationToken);

        manifest.Close();

        // The outcome is the one thing the action alone does not tell you: "closed" and
        // "closed, and something was missing" are different facts about the shipment.
        this.Append(
            AuditAction.ManifestClosed,
            manifest.Id,
            clock.GetUtcNow().UtcDateTime,
            details: manifest.Status.ToString());

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two technicians closed the same manifest at once. RowVersion means the
            // second write is refused instead of quietly overwriting the first.
            throw new RuleViolationException(
                "manifest_changed_concurrently",
                "Another technician changed this manifest while you were closing it. Reload it and try again.");
        }
    }

    /// <summary>
    /// Records that something happened, to be saved alongside the change itself.
    /// </summary>
    /// <remarks>
    /// The actor comes from the request context rather than from anything the caller sent,
    /// so the log records who was acting, not who a request claimed to be. No patient
    /// details are copied in: the ids are enough to reconstruct the picture from the
    /// tenant's own data, and an audit log is the last place PHI should accumulate.
    /// </remarks>
    private void Append(AuditAction action, Guid manifestId, DateTime at, Guid? specimenId = null, string? details = null) =>
        database.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            ManifestId = manifestId,
            SpecimenId = specimenId,
            Action = action,
            Actor = user.LabTech,
            At = at,
            Details = details,
        });

    private static Specimen FindSpecimen(Manifest manifest, Guid specimenId) =>
        manifest.Specimens.FirstOrDefault(specimen => specimen.Id == specimenId)
            ?? throw new NotFoundException("specimen", specimenId);

    private async Task<Manifest> LoadManifestAsync(Guid manifestId, CancellationToken cancellationToken) =>
        await database.Manifests
            .Include(manifest => manifest.Specimens)
            .FirstOrDefaultAsync(manifest => manifest.Id == manifestId, cancellationToken)
        ?? throw new NotFoundException("manifest", manifestId);
}
