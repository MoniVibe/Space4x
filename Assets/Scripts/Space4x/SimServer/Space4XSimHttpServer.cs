using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace Space4X.SimServer
{
    internal static class Space4XSimHttpServer
    {
        private static readonly ConcurrentQueue<string> s_directiveQueue = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<string> s_saveQueue = new ConcurrentQueue<string>();
        private static readonly ConcurrentQueue<string> s_loadQueue = new ConcurrentQueue<string>();
        private static readonly object s_statusLock = new object();
        private static string s_statusJson = "{\"ok\":true}";
        private static HttpListener s_listener;
        private static Thread s_thread;
        private static volatile bool s_running;

        internal static bool IsRunning => s_running;

        internal static void UpdateStatus(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            lock (s_statusLock)
            {
                s_statusJson = json;
            }
        }

        internal static bool TryDequeueDirective(out string json)
        {
            return s_directiveQueue.TryDequeue(out json);
        }

        internal static bool TryDequeueSave(out string json)
        {
            return s_saveQueue.TryDequeue(out json);
        }

        internal static bool TryDequeueLoad(out string json)
        {
            return s_loadQueue.TryDequeue(out json);
        }

        internal static void Start(string host, int port)
        {
            if (s_running)
            {
                return;
            }

            var prefix = $"http://{host}:{port}/";
            s_listener = new HttpListener();
            s_listener.Prefixes.Add(prefix);
            s_listener.Start();
            s_running = true;
            s_thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "Space4XSimHttpServer"
            };
            s_thread.Start();
        }

        internal static void Stop()
        {
            s_running = false;
            try
            {
                s_listener?.Stop();
                s_listener?.Close();
            }
            catch
            {
                // ignore shutdown errors
            }
            s_listener = null;
            s_thread = null;
        }

        private static void ListenLoop()
        {
            while (s_running && s_listener != null)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = s_listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    continue;
                }

                if (ctx == null)
                {
                    continue;
                }

                HandleRequest(ctx);
            }
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            var request = ctx.Request;
            var response = ctx.Response;
            var path = request.Url?.AbsolutePath?.TrimEnd('/')?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            try
            {
                if (request.HttpMethod == "GET" && (path == "/" || path == "/health"))
                {
                    WriteJson(response, "{\"ok\":true}");
                    return;
                }

                if (request.HttpMethod == "GET" && path == "/dashboard")
                {
                    WriteHtml(response, BuildDashboardHtml());
                    return;
                }

                if (request.HttpMethod == "GET" && path == "/status")
                {
                    string status;
                    lock (s_statusLock)
                    {
                        status = s_statusJson;
                    }
                    WriteJson(response, status);
                    return;
                }

                if (request.HttpMethod == "GET" && path == "/saves")
                {
                    WriteJson(response, Space4XSimServerPaths.BuildSaveListJson());
                    return;
                }

                if (request.HttpMethod == "POST" && path == "/directive")
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
                    var body = reader.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        s_directiveQueue.Enqueue(body);
                    }
                    WriteJson(response, "{\"accepted\":true}");
                    return;
                }

                if (request.HttpMethod == "POST" && path == "/save")
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
                    var body = reader.ReadToEnd();
                    s_saveQueue.Enqueue(body ?? string.Empty);
                    WriteJson(response, "{\"queued\":true}");
                    return;
                }

                if (request.HttpMethod == "POST" && path == "/load")
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
                    var body = reader.ReadToEnd();
                    s_loadQueue.Enqueue(body ?? string.Empty);
                    WriteJson(response, "{\"queued\":true}");
                    return;
                }

                WriteJson(response, "{\"error\":\"not_found\"}", 404);
            }
            catch
            {
                try
                {
                    WriteJson(response, "{\"error\":\"server_error\"}", 500);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static void WriteJson(HttpListenerResponse response, string json, int statusCode = 200)
        {
            var buffer = Encoding.UTF8.GetBytes(json ?? "{}");
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            using var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
        }

        private static void WriteHtml(HttpListenerResponse response, string html, int statusCode = 200)
        {
            var buffer = Encoding.UTF8.GetBytes(html ?? string.Empty);
            response.StatusCode = statusCode;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            using var output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
        }

        private static string BuildDashboardHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <title>Space4X Sim Dashboard</title>
  <style>
    body { font-family: Arial, sans-serif; margin: 16px; }
    pre { background:#111; color:#0f0; padding:12px; overflow:auto; max-height:300px; }
    label { display:inline-block; width:120px; }
    input { margin:4px 0; }
    button { margin-top:8px; }
  </style>
</head>
<body>
  <h2>Space4X Sim Dashboard</h2>
  <div>
    <button onclick='refreshStatus()'>Refresh</button>
    <button onclick='saveSim()'>Save</button>
    <input id='loadSlot' placeholder='load slot'/>
    <button onclick='loadSim()'>Load</button>
  </div>
  <pre id='status'>loading...</pre>
  <h3>Directive</h3>
  <div>
    <div><label>Faction Id</label><input id='factionId' value='1'/></div>
    <div><label>Directive</label><input id='directiveId' value='secure_resources'/></div>
    <div><label>Priority</label><input id='priority' value='0.5'/></div>
    <div><label>Mode</label><input id='mode' value='blend'/></div>
    <div><label>Replace</label><input id='replaceOrders' value='false'/></div>
    <div><label>Duration (s)</label><input id='durationSeconds' value='0'/></div>
    <div><label>Duration (ticks)</label><input id='durationTicks' value='0'/></div>
    <div><label>Expires At</label><input id='expiresAtTick' value='0'/></div>
    <div><label>Security</label><input id='wSecurity' value='-1'/></div>
    <div><label>Economy</label><input id='wEconomy' value='-1'/></div>
    <div><label>Research</label><input id='wResearch' value='-1'/></div>
    <div><label>Expansion</label><input id='wExpansion' value='-1'/></div>
    <div><label>Diplomacy</label><input id='wDiplomacy' value='-1'/></div>
    <button onclick='sendDirective()'>Send</button>
  </div>
  <script>
    async function refreshStatus(){
      const res = await fetch('/status');
      const data = await res.json();
      document.getElementById('status').textContent = JSON.stringify(data, null, 2);
    }
    async function sendDirective(){
      const payload = {
        faction_id: parseInt(document.getElementById('factionId').value || '0'),
        directive_id: document.getElementById('directiveId').value,
        priority: parseFloat(document.getElementById('priority').value || '0.5'),
        mode: document.getElementById('mode').value,
        replace_orders: document.getElementById('replaceOrders').value === 'true',
        duration_seconds: parseFloat(document.getElementById('durationSeconds').value || '0'),
        duration_ticks: parseInt(document.getElementById('durationTicks').value || '0'),
        expires_at_tick: parseInt(document.getElementById('expiresAtTick').value || '0'),
        weights: {
          security: parseFloat(document.getElementById('wSecurity').value || '-1'),
          economy: parseFloat(document.getElementById('wEconomy').value || '-1'),
          research: parseFloat(document.getElementById('wResearch').value || '-1'),
          expansion: parseFloat(document.getElementById('wExpansion').value || '-1'),
          diplomacy: parseFloat(document.getElementById('wDiplomacy').value || '-1')
        }
      };
      await fetch('/directive', { method: 'POST', body: JSON.stringify(payload) });
      await refreshStatus();
    }
    async function saveSim(){
      await fetch('/save', { method: 'POST', body: '{}' });
    }
    async function loadSim(){
      const slot = document.getElementById('loadSlot').value;
      const payload = slot ? { slot: slot } : { latest: true };
      await fetch('/load', { method: 'POST', body: JSON.stringify(payload) });
      await refreshStatus();
    }
    refreshStatus();
    setInterval(refreshStatus, 2000);
  </script>
</body>
</html>";
        }
    }
}
