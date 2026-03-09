namespace Api.Services;

using System.Threading;
using System.Threading.Tasks;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
