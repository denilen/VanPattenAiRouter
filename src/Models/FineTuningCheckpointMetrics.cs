namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents metrics for a fine-tuning checkpoint.
/// </summary>
public class FineTuningCheckpointMetrics
{
    /// <summary>
    ///     The training loss.
    /// </summary>
    public double? TrainingLoss { get; set; }

    /// <summary>
    ///     The validation loss.
    /// </summary>
    public double? ValidationLoss { get; set; }

    /// <summary>
    ///     The training accuracy.
    /// </summary>
    public double? TrainingAccuracy { get; set; }

    /// <summary>
    ///     The validation accuracy.
    /// </summary>
    public double? ValidationAccuracy { get; set; }

    /// <summary>
    ///     The full training loss.
    /// </summary>
    public double? FullTrainingLoss { get; set; }

    /// <summary>
    ///     The full validation loss.
    /// </summary>
    public double? FullValidationLoss { get; set; }

    /// <summary>
    ///     The full training accuracy.
    /// </summary>
    public double? FullTrainingAccuracy { get; set; }

    /// <summary>
    ///     The full validation accuracy.
    /// </summary>
    public double? FullValidationAccuracy { get; set; }
}