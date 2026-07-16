using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.WebAPI.Seeding;

/// <summary>
/// Puts the database in a state a reviewer can open the app against and see the workflow.
/// </summary>
/// <remarks>
/// <para>
/// Every patient here is invented. No real patient data appears in this repository.
/// </para>
/// <para>
/// Ids and timestamps are fixed rather than random, so the same manifest is always the
/// same manifest: a link keeps working, and a failing test names something findable.
/// </para>
/// <para>
/// Note what this cannot do: tenant rows are written through a context acting as a lab,
/// because seeding is bound by the same isolation as everything else. There is no
/// privileged path that writes across labs — not even for setup.
/// </para>
/// </remarks>
public static class DatabaseSeeder
{
    /// <summary>The lab a reviewer sees by default.</summary>
    public const int CentralLabId = 1;

    /// <summary>A second lab, whose data the first must never see.</summary>
    public const int WestsideLabId = 2;

    private static readonly DateTime Sent = new(2026, 5, 26, 10, 48, 0, DateTimeKind.Utc);

    /// <summary>
    /// Applies migrations and seeds the labs and their manifests, if not already present.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            SpecimenCheckInContext database = scope.ServiceProvider.GetRequiredService<SpecimenCheckInContext>();

            await database.Database.MigrateAsync(cancellationToken);

            if (!await database.Labs.AnyAsync(cancellationToken))
            {
                database.Labs.AddRange(
                    new Lab { Id = CentralLabId, Name = "Central Lab" },
                    new Lab { Id = WestsideLabId, Name = "Westside Pathology Lab" });

                await database.SaveChangesAsync(cancellationToken);
            }
        }

        await SeedLabAsync(services, CentralLabId, SeedCentralLab, cancellationToken);
        await SeedLabAsync(services, WestsideLabId, SeedWestsideLab, cancellationToken);
    }

    private static async Task SeedLabAsync(
        IServiceProvider services,
        int labId,
        Action<SpecimenCheckInContext> seed,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        // Bind the scope to the lab before touching the context, so the interceptor
        // publishes it and the database stamps every row that follows.
        scope.ServiceProvider.GetRequiredService<TenantContext>().Resolve(labId);

        SpecimenCheckInContext database = scope.ServiceProvider.GetRequiredService<SpecimenCheckInContext>();

        if (await database.Manifests.AnyAsync(cancellationToken))
        {
            return;
        }

        seed(database);

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Three manifests covering the states the screen has to render: one mid-check-in, one
    /// ready to close, and one with a missing bottle.
    /// </summary>
    private static void SeedCentralLab(SpecimenCheckInContext database)
    {
        // Part way through: what a technician opens the app to work on.
        Manifest inProgress = NewManifest("MF-2026-0042", "Riverside Clinic — Bay 2", Sent);
        AddSpecimens(inProgress,
            ("SP-2026-A0041", "Sarah Lin", "Right cheek", "Dr. Patel", SpecimenStatus.Received),
            ("SP-2026-A0042", "Sarah Lin", "Left cheek", "Dr. Patel", SpecimenStatus.Received),
            ("SP-2026-A0043", "Marcus Reed", "Back, upper", "Dr. Chen", SpecimenStatus.Received),
            ("SP-2026-A0044", "Marcus Reed", "Right shoulder", "Dr. Chen", SpecimenStatus.Pending),
            ("SP-2026-A0045", "Priya Shah", "Scalp", "Dr. Reed", SpecimenStatus.Pending),
            ("SP-2026-A0046", "Tom Alvarez", "Left forearm", "Dr. Patel", SpecimenStatus.Pending),
            ("SP-2026-A0047", "Jane Doe", "Left forearm", "Dr. Patel", SpecimenStatus.Pending));

        // Everything accounted for: closing this one should succeed.
        Manifest readyToClose = NewManifest("MF-2026-0040", "Riverside Clinic — Bay 2", Sent.AddDays(-1));
        AddSpecimens(readyToClose,
            ("SP-2026-A0031", "Ana Duarte", "Left cheek", "Dr. Patel", SpecimenStatus.Received),
            ("SP-2026-A0032", "Ana Duarte", "Right cheek", "Dr. Patel", SpecimenStatus.Received),
            ("SP-2026-A0033", "Owen Blake", "Scalp", "Dr. Reed", SpecimenStatus.Received),
            ("SP-2026-A0034", "Mia Novak", "Left forearm", "Dr. Chen", SpecimenStatus.Received),
            ("SP-2026-A0035", "Mia Novak", "Right forearm", "Dr. Chen", SpecimenStatus.Received));

        // A bottle never turned up: closing this one lands on ClosedWithDiscrepancy.
        Manifest withDiscrepancy = NewManifest("MF-2026-0039", "Northgate Derm", Sent.AddDays(-2));
        AddSpecimens(withDiscrepancy,
            ("SP-2026-A0021", "Liam Ortiz", "Back, lower", "Dr. Reed", SpecimenStatus.Received),
            ("SP-2026-A0022", "Liam Ortiz", "Back, upper", "Dr. Reed", SpecimenStatus.Received),
            ("SP-2026-A0023", "Grace Kim", "Scalp", "Dr. Patel", SpecimenStatus.Received),
            ("SP-2026-A0024", "Grace Kim", "Right cheek", "Dr. Patel", SpecimenStatus.Flagged));

        Specimen missing = withDiscrepancy.Specimens.Single(specimen => specimen.Code == "SP-2026-A0024");

        database.Manifests.AddRange(inProgress, readyToClose, withDiscrepancy);
        database.Discrepancies.Add(new Discrepancy
        {
            Id = Guid.NewGuid(),
            ManifestId = withDiscrepancy.Id,
            SpecimenId = missing.Id,
            Type = DiscrepancyType.Missing,
            Status = DiscrepancyStatus.Open,
            RaisedAt = Sent.AddDays(-2).AddHours(2),
            Notes = "Bottle not in shipment; clinic contacted.",
        });
    }

    /// <summary>
    /// The second lab's data exists to be invisible: nothing here should ever appear while
    /// acting as the Central Lab.
    /// </summary>
    private static void SeedWestsideLab(SpecimenCheckInContext database)
    {
        Manifest manifest = NewManifest("MF-2026-0101", "Eastside Family Practice", Sent.AddHours(-3));
        AddSpecimens(manifest,
            ("SP-2026-B0011", "Noor Haddad", "Left cheek", "Dr. Okafor", SpecimenStatus.Received),
            ("SP-2026-B0012", "Noor Haddad", "Right cheek", "Dr. Okafor", SpecimenStatus.Pending),
            ("SP-2026-B0013", "Ethan Cole", "Scalp", "Dr. Okafor", SpecimenStatus.Pending));

        database.Manifests.Add(manifest);
    }

    private static Manifest NewManifest(string code, string originClinic, DateTime sentAt) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        OriginClinic = originClinic,
        Status = ManifestStatus.Open,
        SentAt = sentAt,
    };

    private static void AddSpecimens(
        Manifest manifest,
        params (string Code, string Patient, string Site, string Provider, SpecimenStatus Status)[] specimens)
    {
        foreach ((string code, string patient, string site, string provider, SpecimenStatus status) in specimens)
        {
            bool received = status == SpecimenStatus.Received;

            manifest.Specimens.Add(new Specimen
            {
                Id = Guid.NewGuid(),
                Code = code,
                Patient = patient,
                Site = site,
                Provider = provider,
                Status = status,
                ReceivedBy = received ? UserContext.DefaultLabTech : null,
                ReceivedAt = received ? manifest.SentAt.AddHours(1) : null,
            });
        }
    }
}
