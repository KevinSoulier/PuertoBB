namespace PuertoBB.Core.Models;

/// <summary>
/// Resultado del asistente de generación de certificado AFIP. Contiene la clave privada PEM (.key)
/// recién generada, para cargarla en el punto de venta en modo CRT+KEY. El usuario luego sube el CSR
/// al portal de AFIP e importa el .crt que le devuelvan. Null si el usuario canceló.
/// </summary>
public record CertificadoWizardResult(byte[] ClavePrivadaPem, string Alias);
