using Microsoft.Extensions.Logging;
using PuertoBB.Core.Entities.CamaraPortuaria;
using PuertoBB.Core.Interfaces.Repositories.CamaraPortuaria;
using PuertoBB.Infrastructure.Data;

namespace PuertoBB.Infrastructure.Repositories.CamaraPortuaria;

public class NotaDeCreditoRepository : RepositoryBase<NotaDeCredito>, INotaDeCreditoRepository
{
    public NotaDeCreditoRepository(CamaraPortuariaDbContext db, ILogger<NotaDeCreditoRepository> logger) : base(db, logger) { }
}
