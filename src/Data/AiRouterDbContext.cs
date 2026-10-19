using Microsoft.EntityFrameworkCore;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Data;

/// <summary>
///     Entity Framework database context for the AI Router application.
/// </summary>
public class AiRouterDbContext : DbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AiRouterDbContext" /> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public AiRouterDbContext(DbContextOptions<AiRouterDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    ///     Gets or sets the providers table.
    /// </summary>
    public DbSet<Provider> Providers { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the provider models table.
    /// </summary>
    public DbSet<ProviderModel> ProviderModels { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the files table.
    /// </summary>
    public DbSet<FileEntity> Files { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the assistants table.
    /// </summary>
    public DbSet<AssistantEntity> Assistants { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the threads table.
    /// </summary>
    public DbSet<ThreadEntity> Threads { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the thread messages table.
    /// </summary>
    public DbSet<ThreadMessageEntity> ThreadMessages { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the runs table.
    /// </summary>
    public DbSet<RunEntity> Runs { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the run steps table.
    /// </summary>
    public DbSet<RunStepEntity> RunSteps { get; set; } = null!;

    /// <summary>
    ///     Configures the entity model for the database.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore RateLimitConfig as it's a value object stored as JSON
        modelBuilder.Ignore<RateLimitConfig>();

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BaseUrl).HasMaxLength(500); // Virtual providers may not have BaseUrl
            entity.Property(e => e.ApiKey).HasMaxLength(500);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.Headers).HasMaxLength(2000);
            entity.Property(e => e.RateLimitJson).HasMaxLength(1000);
            entity.Property(e => e.RequestsToday).HasDefaultValue(0);
            entity.Property(e => e.TokensToday).HasDefaultValue(0);
            entity.Property(e => e.LastResetDate);
            entity.Property(e => e.RequestsThisHour).HasDefaultValue(0);
            entity.Property(e => e.TokensThisHour).HasDefaultValue(0);
            entity.Property(e => e.LastHourReset);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ProviderModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModelName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Mode).HasMaxLength(50); // "normal", "auto-free", "auto-all"
            entity.HasOne(e => e.Provider)
                  .WithMany(p => p.Models)
                  .HasForeignKey(e => e.ProviderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ProviderId, e.ModelName }).IsUnique();
        });

        modelBuilder.Entity<FileEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Filename).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StatusDetails).HasMaxLength(1000);
            entity.Property(e => e.StoragePath).HasMaxLength(1000);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Purpose);
        });

        modelBuilder.Entity<AssistantEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Instructions).HasMaxLength(10000);
            entity.Property(e => e.MetadataJson).HasMaxLength(2000);
            entity.Property(e => e.ResponseFormatJson).HasMaxLength(1000);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<ThreadEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(50);
            entity.Property(e => e.MetadataJson).HasMaxLength(2000);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<ThreadMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ThreadId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ContentJson).HasMaxLength(50000);
            entity.Property(e => e.FileIdsJson).HasMaxLength(2000);
            entity.Property(e => e.MetadataJson).HasMaxLength(2000);
            entity.Property(e => e.AssistantId).HasMaxLength(50);
            entity.Property(e => e.RunId).HasMaxLength(50);
            entity.HasIndex(e => e.ThreadId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<RunEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ThreadId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AssistantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Instructions).HasMaxLength(10000);
            entity.Property(e => e.ToolsJson).HasMaxLength(2000);
            entity.Property(e => e.FileIdsJson).HasMaxLength(2000);
            entity.Property(e => e.MetadataJson).HasMaxLength(2000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.RequiredActionJson).HasMaxLength(2000);
            entity.Property(e => e.LastErrorJson).HasMaxLength(1000);
            entity.Property(e => e.IncompleteDetailsJson).HasMaxLength(1000);
            entity.Property(e => e.ResponseFormatJson).HasMaxLength(1000);
            entity.Property(e => e.ToolChoiceJson).HasMaxLength(1000);
            entity.Property(e => e.TruncationStrategyJson).HasMaxLength(1000);
            entity.HasIndex(e => e.ThreadId);
            entity.HasIndex(e => e.AssistantId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<RunStepEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RunId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ThreadId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AssistantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.StepDetailsJson).HasMaxLength(2000);
            entity.Property(e => e.LastErrorJson).HasMaxLength(1000);
            entity.Property(e => e.UsageJson).HasMaxLength(1000);
            entity.HasIndex(e => e.RunId);
            entity.HasIndex(e => e.ThreadId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}