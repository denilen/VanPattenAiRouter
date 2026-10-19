using System.Text.Json.Serialization;

namespace VanPattenAIRouter.Models;

/// <summary>
///     Represents a fine-tuning job request compatible with OpenAI API format.
/// </summary>
public class FineTuningJobRequest
{
    /// <summary>
    ///     The ID of the file containing training data.
    /// </summary>
    [JsonPropertyName("training_file")]
    public string TrainingFile { get; set; } = "";

    /// <summary>
    ///     The ID of the file containing validation data.
    /// </summary>
    [JsonPropertyName("validation_file")]
    public string? ValidationFile { get; set; }

    /// <summary>
    ///     The name of the base model to fine-tune.
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    /// <summary>
    ///     The hyperparameters for the fine-tuning job.
    /// </summary>
    [JsonPropertyName("hyperparameters")]
    public FineTuningHyperparameters? Hyperparameters { get; set; }

    /// <summary>
    ///     A string to identify the fine-tuning job.
    /// </summary>
    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }

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

    /// <summary>
    ///     The random seed for training.
    /// </summary>
    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
}