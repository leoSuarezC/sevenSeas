namespace SpecimenCheckIn.Models;

/// <summary>
/// The running tally a technician works against: what the manifest promised versus what
/// has actually been accounted for.
/// </summary>
/// <param name="Expected">Bottles the manifest lists.</param>
/// <param name="Received">Bottles physically checked in.</param>
/// <param name="Pending">Bottles still unaccounted for.</param>
/// <param name="Flagged">Bottles reported missing.</param>
public readonly record struct ManifestCounts(int Expected, int Received, int Pending, int Flagged)
{
    /// <summary>
    /// Gets a value indicating whether every bottle has been accounted for — received or
    /// flagged missing, with none left pending.
    /// </summary>
    /// <remarks>
    /// Being reconciled is not the same as everything having arrived. A manifest whose
    /// missing bottles are all flagged is reconciled: the lab knows where it stands.
    /// What blocks closing is the unknown — a bottle nobody has ruled on.
    /// </remarks>
    public bool IsReconciled => this.Pending == 0;
}
