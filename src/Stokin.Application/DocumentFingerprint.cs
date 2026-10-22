using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Stokin.Application;

public static class DocumentFingerprint
{
    public static string Compute(DocumentView d) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { d.Id, d.Type, d.Reason, d.ExternalReference, d.ReferenceDocumentId, d.Version, Lines = d.Lines.OrderBy(x => x.ProductId) }))));
}
