using FluentValidation;

namespace ReplicaGuard.Application;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> HttpUrl<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(url =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("'{PropertyName}' must be a valid HTTP or HTTPS URL.");
    }

    public static IRuleBuilderOptions<T, string> ValidFilePath<T>(
        this IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder.Must(path =>
        {
            try
            {
                Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        })
        .WithMessage("'{PropertyName}' must be a valid file path.");
    }
}
