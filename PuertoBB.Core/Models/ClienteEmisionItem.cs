namespace PuertoBB.Core.Models;

/// <summary>DTO liviano para pasar Cliente o Cliente al diálogo de emisión individual sin acoplar IDialogService a tipos por-app.</summary>
public record ClienteEmisionItem(int Id, string Nombre);
