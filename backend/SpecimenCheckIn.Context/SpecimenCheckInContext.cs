using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using SpecimenCheckIn.Context.Sql;
using SpecimenCheckIn.Models;

namespace SpecimenCheckIn.Context;

/// <summary>
/// The database for the specimen check-in slice. All schema changes go through this
/// class and the migrations generated from it.
/// </summary>
/// <param name="options">The options used to configure the instance.</param>
public class SpecimenCheckInContext(DbContextOptions<SpecimenCheckInContext> options) : DbContext(options)
{
    /// <summary>The database schema name for this context.</summary>
    public const string Schema = RowLevelSecurity.Schema;

    /// <summary>Gets the labs — the tenants.</summary>
    public virtual DbSet<Lab> Labs => this.Set<Lab>();

    /// <summary>Gets the manifests.</summary>
    public virtual DbSet<Manifest> Manifests => this.Set<Manifest>();

    /// <summary>Gets the specimens.</summary>
    public virtual DbSet<Specimen> Specimens => this.Set<Specimen>();

    /// <summary>Gets the discrepancies.</summary>
    public virtual DbSet<Discrepancy> Discrepancies => this.Set<Discrepancy>();

    /// <summary>Gets the immutable check-in audit log.</summary>
    public virtual DbSet<AuditEvent> AuditEvents => this.Set<AuditEvent>();

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.GuardAuditLogIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        this.GuardAuditLogIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Lab>(lab =>
        {
            lab.HasKey(x => x.Id);

            // Seeded, stable ids: a lab id is also the tenant id that appears in
            // connection session context and in the X-Lab-Id header.
            lab.Property(x => x.Id).ValueGeneratedNever();
            lab.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Manifest>(manifest =>
        {
            manifest.Property(x => x.Code).HasMaxLength(50).IsRequired();
            manifest.Property(x => x.OriginClinic).HasMaxLength(200).IsRequired();
            manifest.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            manifest.Property(x => x.RowVersion).IsRowVersion();

            // A manifest code is unique to the lab that received it, not globally:
            // two labs may legitimately use the same code.
            manifest.HasIndex(x => new { x.LabId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<Specimen>(specimen =>
        {
            specimen.Property(x => x.Code).HasMaxLength(50).IsRequired();
            specimen.Property(x => x.Patient).HasMaxLength(200).IsRequired();
            specimen.Property(x => x.Site).HasMaxLength(100).IsRequired();
            specimen.Property(x => x.Provider).HasMaxLength(100).IsRequired();
            specimen.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            specimen.Property(x => x.ReceivedBy).HasMaxLength(100);

            // The same bottle cannot be listed twice on one manifest — this is what
            // makes receiving idempotent at the storage level rather than by convention.
            specimen.HasIndex(x => new { x.ManifestId, x.Code }).IsUnique();

            specimen.HasOne(x => x.Manifest)
                .WithMany(x => x.Specimens)
                .HasForeignKey(x => x.ManifestId);
        });

        modelBuilder.Entity<Discrepancy>(discrepancy =>
        {
            discrepancy.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            discrepancy.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            discrepancy.Property(x => x.Notes).HasMaxLength(1000);

            discrepancy.HasIndex(x => x.ManifestId);

            discrepancy.HasOne(x => x.Manifest)
                .WithMany(x => x.Discrepancies)
                .HasForeignKey(x => x.ManifestId);

            discrepancy.HasOne(x => x.Specimen)
                .WithMany()
                .HasForeignKey(x => x.SpecimenId);
        });

        modelBuilder.Entity<AuditEvent>(auditEvent =>
        {
            auditEvent.Property(x => x.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
            auditEvent.Property(x => x.Actor).HasMaxLength(100).IsRequired();
            auditEvent.Property(x => x.Details).HasMaxLength(1000);

            // The log is read back as a manifest's history, newest first.
            auditEvent.HasIndex(x => new { x.ManifestId, x.At });

            // No navigation properties: an audit row must outlive whatever it describes.
        });

        ConfigureTenantOwnedEntities(modelBuilder);

        // Receiving desks correct mistakes; they do not erase history. Deleting a
        // manifest should fail loudly rather than quietly take its specimens with it.
        foreach (IMutableForeignKey relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Applies the conventions every tenant-owned table shares: a non-clustered Guid key,
    /// an identity clustering key, and a LabId the database stamps from session context.
    /// </summary>
    private static void ConfigureTenantOwnedEntities(ModelBuilder modelBuilder)
    {
        List<Type> tenantOwned = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType => typeof(TenantOwnedEntity).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (Type clrType in tenantOwned)
        {
            modelBuilder.Entity(clrType, builder =>
            {
                // Guid key, so it carries no meaning and cannot be guessed across tenants.
                // ClusterId, not the key, carries the clustered index — see TenantOwnedEntity.
                builder.HasKey(nameof(Manifest.Id)).IsClustered(false);
                builder.HasIndex(nameof(TenantOwnedEntity.ClusterId)).IsUnique().IsClustered();

                builder.Property(nameof(TenantOwnedEntity.ClusterId)).ValueGeneratedOnAdd();

                // The application never supplies a LabId; the database derives it from
                // the session context set for this connection.
                builder.Property(nameof(TenantOwnedEntity.LabId))
                    .ValueGeneratedOnAdd()
                    .HasDefaultValueSql(RowLevelSecurity.CurrentLabIdDefaultSql);
            });
        }
    }

    /// <summary>
    /// Rejects any attempt to change or remove an audit event.
    /// </summary>
    private void GuardAuditLogIsAppendOnly()
    {
        bool tampered = this.ChangeTracker
            .Entries<AuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (tampered)
        {
            throw new InvalidOperationException(
                "The audit log is append-only: audit events cannot be modified or deleted.");
        }
    }
}
