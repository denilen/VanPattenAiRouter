using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents hyperparameters for fine-tuning.
/// </summary>
public class FineTuningHyperparameters
{
    /// <summary>
    ///     The number of epochs to train for.
    /// </summary>
    [JsonPropertyName("n_epochs")]
    public int? NEpochs { get; set; }

    /// <summary>
    ///     The batch size for training.
    /// </summary>
    [JsonPropertyName("batch_size")]
    public int? BatchSize { get; set; }

    /// <summary>
    ///     The learning rate multiplier.
    /// </summary>
    [JsonPropertyName("learning_rate_multiplier")]
    public double? LearningRateMultiplier { get; set; }
}