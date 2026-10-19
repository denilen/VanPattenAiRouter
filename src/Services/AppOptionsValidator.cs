using Microsoft.Extensions.Options;
using VanPattenAIRouter.Models;

namespace VanPattenAIRouter.Services;

/// <summary>
///     Validates the application options.
/// </summary>
public class AppOptionsValidator : IValidateOptions<AppOptions>
{
    public ValidateOptionsResult Validate(string? name, AppOptions options)
    {
        var failures = new List<string>();

        if (options.Router.ModelRefreshIntervalMinutes <= 0)
            failures.Add("Router:ModelRefreshIntervalMinutes must be greater than 0");

        if (options.Router.TimeoutSeconds <= 0) failures.Add("Router:TimeoutSeconds must be greater than 0");

        if (options.Database.Path == null || string.IsNullOrWhiteSpace(options.Database.Path))
            failures.Add("Database:Path is required");

        if (options.Router.Retry == null) failures.Add("Router:Retry configuration is required");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}