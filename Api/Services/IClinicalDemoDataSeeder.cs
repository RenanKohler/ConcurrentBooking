namespace Api.Services;

using System.Threading;
using System.Threading.Tasks;
using ConcurrentBooking.Infrastructure.Data;

public interface IClinicalDemoDataSeeder
{
    Task SeedAsync(BookingDbContext db, CancellationToken cancellationToken = default);
}
