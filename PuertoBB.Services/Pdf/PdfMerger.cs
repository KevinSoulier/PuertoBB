using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PuertoBB.Core.Interfaces.Services;

namespace PuertoBB.Services.Pdf;

/// <summary>Concatena PDFs con PdfSharp (open/import páginas + save).</summary>
public class PdfMerger : IPdfMerger
{
    public byte[] Merge(IEnumerable<byte[]> pdfs)
    {
        var lista = pdfs?.Where(b => b is { Length: > 0 }).ToList() ?? [];
        if (lista.Count == 0) return [];
        if (lista.Count == 1) return lista[0];

        using var salida = new PdfDocument();
        foreach (var bytes in lista)
        {
            using var ms = new MemoryStream(bytes);
            using var entrada = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
            for (int i = 0; i < entrada.PageCount; i++)
                salida.AddPage(entrada.Pages[i]);
        }
        using var outMs = new MemoryStream();
        salida.Save(outMs, false);
        return outMs.ToArray();
    }
}
