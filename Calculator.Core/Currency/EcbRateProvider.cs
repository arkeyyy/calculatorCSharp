using System.Globalization;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using PeterO.Numbers;

namespace Calculator.Core;

public interface ICurrencyRateProvider
{
    Task<CurrencyRateSnapshot> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed class EcbRateProvider : ICurrencyRateProvider, IDisposable
{
    public const int MaximumBytes = 131072;
    public const string FeedUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
    private const string Namespace = "http://www.ecb.int/vocabulary/2002-08-01/eurofxref";
    private readonly HttpClient client;
    private readonly bool ownsClient;

    public EcbRateProvider(HttpClient? client = null)
    {
        ownsClient = client is null;
        this.client = client ?? new HttpClient(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
        { Timeout = TimeSpan.FromSeconds(12) };
    }

    public async Task<CurrencyRateSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
        request.Headers.Accept.ParseAdd("application/xml");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK) throw new HttpRequestException($"ECB returned HTTP {(int)response.StatusCode}.");
        if (response.Content.Headers.ContentLength > MaximumBytes) throw new ArgumentException("Rate response is too large.");
        using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (bytes.Length + read > MaximumBytes) throw new ArgumentException("Rate response is too large.");
            bytes.Write(buffer, 0, read);
        }
        return Parse(bytes.ToArray());
    }

    public static CurrencyRateSnapshot Parse(byte[] xml, DateOnly? today = null)
    {
        if (xml.Length > MaximumBytes) throw new ArgumentException("Rate response is too large.");
        try
        {
            using var stream = new MemoryStream(xml, writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes });
            var document = XDocument.Load(reader);
            XNamespace ns = Namespace;
            var cubes = document.Descendants(ns + "Cube").ToArray();
            var dated = cubes.Where(cube => cube.Attribute("time") is not null).ToArray();
            if (dated.Length != 1) throw new ArgumentException("Missing or multiple rate dates.");
            var date = DateOnly.ParseExact(dated[0].Attribute("time")!.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var rates = new Dictionary<string, EDecimal>(StringComparer.Ordinal) { ["EUR"] = EDecimal.One };
            foreach (var child in dated[0].Elements())
                if (child.Name != ns + "Cube" || child.Attribute("currency") is null || child.Attribute("rate") is null || child.Nodes().Any())
                    throw new ArgumentException("Malformed dated rate list.");
            foreach (var cube in cubes.Where(cube => cube.Parent == dated[0] || cube.Attribute("currency") is not null || cube.Attribute("rate") is not null))
            {
                if (cube.Parent != dated[0] || cube.Attribute("currency") is null || cube.Attribute("rate") is null ||
                    !rates.TryAdd(cube.Attribute("currency")!.Value, CurrencyRateSnapshot.ParseRate(cube.Attribute("rate")!.Value)))
                    throw new ArgumentException("Invalid or duplicate rate entry.");
            }
            return new(date, rates, today);
        }
        catch (Exception error) when (error is XmlException or FormatException or ArgumentException)
        { throw new ArgumentException("Invalid ECB rate response.", error); }
    }

    public void Dispose() { if (ownsClient) client.Dispose(); }
}
