using System.Net;
using System.Net.Http.Headers;

namespace Vograph.Core.Services;

public class AutoRefreshService : IDisposable
{
    private readonly Database _db;
    private readonly ParserService _parser;
    private readonly System.Threading.Timer _timer;
    private readonly HttpClient _http;
    private bool _disposed;

    public AutoRefreshService(Database db, ParserService parser)
    {
        _db = db;
        _parser = parser;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Vograph/1.0");
        // check every 24h
        _timer = new System.Threading.Timer(async _ => await CheckAsync(false), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        // immediate check on start (background)
        Task.Run(async () => await CheckAsync(true));
        // then every 24h
        _timer.Change(TimeSpan.FromHours(24), TimeSpan.FromHours(24));
    }

    public async Task<bool> CheckAsync(bool isStartup)
    {
        var settings = _db.GetSettings();
        DateTime lastCheck = DateTime.MinValue;
        if (!string.IsNullOrEmpty(settings.LastAutoCheckAt) && DateTime.TryParse(settings.LastAutoCheckAt, out var lc)) lastCheck = lc;
        // respect 1-3 day interval: if last check <24h ago and not startup, skip
        if (!isStartup && (DateTime.UtcNow - lastCheck).TotalHours < 24) return false;

        string url = ParserService.DefaultUrl;
        string? lastFetched = settings.LastFetchedAt;
        DateTime? lastModified = null;
        if (!string.IsNullOrEmpty(lastFetched) && DateTime.TryParse(lastFetched, out var lf)) lastModified = lf;

        bool changed = false;
        string logLine;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            if (lastModified != null)
            {
                req.Headers.IfModifiedSince = new DateTimeOffset(lastModified.Value.ToUniversalTime());
            }
            var resp = await _http.SendAsync(req);
            // log headers
            var lm = resp.Content.Headers.LastModified ?? resp.Headers.Date;
            string lmStr = lm?.ToString("R") ?? "no Last-Modified";
            if (resp.StatusCode == HttpStatusCode.NotModified)
            {
                logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} HEAD {url} 304 Not Modified (If-Modified-Since {lastModified:O}) lm={lmStr} -> no update";
                Log(logLine);
            }
            else if (resp.IsSuccessStatusCode)
            {
                // check if remote Last-Modified newer than local
                bool newer = true;
                if (lm != null && lastModified != null)
                {
                    newer = lm.Value.UtcDateTime > lastModified.Value.ToUniversalTime().AddSeconds(-5);
                }
                if (newer)
                {
                    // fetch full
                    var (xml, _) = await _parser.FetchXmlAsync(url, _http);
                    await _parser.RefreshAsync(xmlOverride: xml);
                    changed = true;
                    logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} HEAD {url} 200 OK lm={lmStr} -> re-parsed, updated lastFetchedAt={DateTime.UtcNow:O}";
                    Log(logLine);
                }
                else
                {
                    logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} HEAD {url} 200 but not newer (lm {lmStr}) -> skip";
                    Log(logLine);
                }
            }
            else
            {
                logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} HEAD {url} {(int)resp.StatusCode} {resp.StatusCode} -> no update";
                Log(logLine);
            }
        }
        catch (Exception ex)
        {
            logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} HEAD {url} error {ex.GetType().Name}: {ex.Message}";
            Log(logLine);
            // on error, keep last cache + stale badge will be shown by caller
        }
        finally
        {
            var s = _db.GetSettings();
            s.LastAutoCheckAt = DateTime.UtcNow.ToString("o");
            if (changed) s.LastFetchedAt = DateTime.UtcNow.ToString("o");
            _db.SaveSettings(s);
        }
        return changed;
    }

    private void Log(string line)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "data", "runs");
            Directory.CreateDirectory(dir);
            var altDir = @"C:\Users\NiLle\Desktop\projects\vograph\data\runs";
            Directory.CreateDirectory(altDir);
            string file = Path.Combine(dir, $"autorefresh-{DateTime.Now:yyyyMMdd}.log");
            string altFile = Path.Combine(altDir, $"autorefresh-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllLines(file, new[] { line });
            File.AppendAllLines(altFile, new[] { line });
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
        _http.Dispose();
    }
}
