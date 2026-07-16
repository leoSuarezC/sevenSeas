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
/// Like the read side, nothing here takes a lab. Loads go through the tenant-filtered
/// context, so a manifest from another lab is simply not found, and writes are stamped
/// with the session's lab by the database.
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

        if (!specimen.Receive(user.LabTech, clock.GetUtcNow().UtcDateTime))
        {
            // Already received: the desk scanned it twice. Saying so is not an error, and
            // writing nothing is what keeps the counts from drifting.
            return;
        }

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
                RaisedAt = clock.GetUtcNow().UtcDateTime,
                Notes = notes,
            });
        }

        // One SaveChanges, so the flag and its discrepancy land together or not at all —
        // a flagged bottle with no discrepancy would be a mismatch nobody is tracking.
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

    private static Specimen FindSpecimen(Manifest manifest, Guid specimenId) =>
        manifest.Specimens.FirstOrDefault(specimen => specimen.Id == specimenId)
            ?? throw new NotFoundException("specimen", specimenId);

    private async Task<Manifest> LoadManifestAsync(Guid manifestId, CancellationToken cancellationToken) =>
        await database.Manifests
            .Include(manifest => manifest.Specimens)
            .FirstOrDefaultAsync(manifest => manifest.Id == manifestId, cancellationToken)
        ?? throw new NotFoundException("manifest", manifestId);
}
