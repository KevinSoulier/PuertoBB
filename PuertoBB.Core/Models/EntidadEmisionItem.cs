namespace PuertoBB.Core.Models;

/// <summary>DTO liviano para pasar Empresa o Agencia al diálogo de emisión individual sin acoplar IDialogService a tipos por-app.</summary>
public record EntidadEmisionItem(int Id, string Nombre);
