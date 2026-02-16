namespace TravelPathways.Api.Services;

public interface IPackagePdfGenerator
{
    Task<byte[]> GenerateAsync(PackagePdfModel model, CancellationToken cancellationToken = default);
}
