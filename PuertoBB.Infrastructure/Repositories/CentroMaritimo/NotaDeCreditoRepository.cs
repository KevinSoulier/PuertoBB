using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CentroMaritimo;
using PuertoBB.Core.Interfaces.Repositories.CentroMaritimo;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CentroMaritimo;

public class NotaDeCreditoRepository : RepositoryBase<NotaDeCredito>, INotaDeCreditoRepository
{
    public NotaDeCreditoRepository(CentroMaritimoDbContext db, ILogger<NotaDeCreditoRepository> logger) : base(db, logger) { }
}
