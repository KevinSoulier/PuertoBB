namespace Afip.Documentos.Catalogo;

public static class TipoComprobanteAfip
{
    public static string Letra(int codigo) => codigo switch
    {
        1 or 2 or 3 or 4 or 5  => "A",
        6 or 7 or 8             => "B",
        11 or 12 or 13 or 15    => "C",
        19 or 20 or 21          => "E",
        _                       => " "
    };

    public static string Nombre(int codigo) => codigo switch
    {
        1 or 6 or 11            => "FACTURA",
        2 or 7 or 12            => "NOTA DE DÉBITO",
        3 or 8 or 13            => "NOTA DE CRÉDITO",
        4 or 9 or 15            => "RECIBO",
        _                       => "COMPROBANTE"
    };
}
