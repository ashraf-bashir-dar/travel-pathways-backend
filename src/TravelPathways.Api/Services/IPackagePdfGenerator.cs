namespace TravelPathways.Api.Services;

public interface IPackagePdfGenerator
{
    Task<PackagePdfGenerateResult> GenerateAsync(PackagePdfModel model, CancellationToken cancellationToken = default);
}
