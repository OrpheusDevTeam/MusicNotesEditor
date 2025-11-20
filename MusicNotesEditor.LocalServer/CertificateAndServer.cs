using Microsoft.AspNetCore.Connections;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace MusicNotesEditor.LocalServer
{
    public class PendingRequest
    {
        public string Key { get; set; }
        public DateTime Time { get; set; }
        public string DeviceName { get; set; }
    }
    public sealed class CertAndServer : IDisposable
    {
        private const string SubjectCN = "CN=EurydiceLocal";
        private WebApplication? _app;
        private bool _disposed;

        public X509Certificate2 Certificate { get; private set; } = null!;
        public string FingerprintSha256 { get; private set; } = null!;
        public string ServerUrl { get; private set; } = null!;
        public string Token { get; private set; } = GenerateSecureToken();

        private readonly Dictionary<string, DeviceRequest> Connections = new();

        public event Action<string>? OnImageUploaded;

        private static string GenerateSecureToken()
        {
            Span<byte> bytes = stackalloc byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public void EnsureCertificate()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            var cert = store.Certificates
                .Find(X509FindType.FindBySubjectDistinguishedName, SubjectCN, false)
                .OfType<X509Certificate2>()
                .FirstOrDefault(c => c.NotAfter > DateTime.UtcNow.AddDays(1));

            Certificate = cert ?? CreateAndInstallSelfSignedCertificate(SubjectCN, 5);
            FingerprintSha256 = GetSha256Fingerprint(Certificate);
        }

        private static X509Certificate2 CreateAndInstallSelfSignedCertificate(string subject, int years)
        {
            using var rsa = RSA.Create(2048);

            var req = new CertificateRequest(
                subject,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
            var notAfter = notBefore.AddYears(years);

            var rawCert = req.CreateSelfSigned(notBefore, notAfter);

            var exportable = new X509Certificate2(
                rawCert.Export(X509ContentType.Pfx),
                (string?)null,
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable);

            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(exportable);

            return exportable;
        }

        private static string GetSha256Fingerprint(X509Certificate2 cert)
        {
            var hash = SHA256.HashData(cert.Export(X509ContentType.Cert));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public async Task<string> StartServerAsync(int port = 5003)
        {
            if(_app is not null)
            {

                return JsonSerializer.Serialize(new
                {
                    url = ServerUrl,
                    token = Token,
                    fp = FingerprintSha256
                });
           
            }

            EnsureCertificate();

            var hostIp = GetLocalIPv4();
            ServerUrl = $"https://{hostIp}:{port}";

            Console.WriteLine($"Selected IP: {hostIp}");


            var payload = new
            {
                url = ServerUrl,
                token = Token,
                fp = FingerprintSha256
            };

            var json = JsonSerializer.Serialize(payload);

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 30 * 1024 * 1024;
                options.Listen(IPAddress.Any, port, listen =>
                {
                    listen.UseHttps(Certificate);
                });
            });

            builder.Services.AddRouting();

            var app = builder.Build();

            app.MapGet("/cert", () =>
            {
                var cert = Certificate.Export(X509ContentType.Cert);
                var pem = PemEncoding.Write("CERTIFICATE", cert);
                return Results.Text(new string(pem), "application/x-pem-file");
            });


            // Health check, please be healthy
            app.MapGet("/ping", () => Results.Ok("pong"));

            // QR pairing packet
            app.MapGet("/pairinfo", () =>
            {
                var payload = new { url = ServerUrl, token = Token, fp = FingerprintSha256 };
                return Results.Json(payload);
            });

            app.MapPost("/request_access", async (HttpRequest req) =>
            {
                string? token = req.Headers["X-Token"];
                if (token != Token)
                    return Results.Forbid();

                var payload = await JsonSerializer.DeserializeAsync<AccessRequestPayload>(req.Body);
                if (payload == null || string.IsNullOrWhiteSpace(payload.DeviceName))
                    return Results.BadRequest();

                var id = Guid.NewGuid().ToString();

                Connections[id] = new DeviceRequest
                {
                    Id = id,
                    DeviceName = payload.DeviceName
                };

                Console.WriteLine($"[REQ] Device '{payload.DeviceName}' requested approval ({id})");

                return Results.Ok(new { id });
            });

            //string? token = req.Headers["X-Token"];
            //string? deviceId = req.Headers["DeviceID"];

            //if (deviceId is null || token is null)
            //    return Results.Unauthorized();

            //DeviceRequest? deviceStatus;
            //Connections.TryGetValue(deviceId, out deviceStatus);

            //if (deviceStatus is null)
            //    return Results.Forbid();

            //if (token != Token || deviceStatus.Approved != true)
            //    return Results.Forbid();
            app.MapPost("/upload", async (HttpRequest req) =>
            {
                string? token = req.Headers["X-Token"];
                if (token != Token)
                    return Results.StatusCode(403);

                // professional save location
                var saveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrpheusApp", "Uploads");
                Directory.CreateDirectory(saveDir);

                var name = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.jpg";
                var savePath = Path.Combine(saveDir, name);

                Console.WriteLine($"Saving file to: {savePath}");

                await using var fs = File.Create(savePath);
                await req.Body.CopyToAsync(fs);
                await fs.FlushAsync();

                OnImageUploaded?.Invoke(savePath);

                return Results.Ok(new { ok = true, file = name, path = savePath });
            });

            // Return all pending requests


            app.MapGet("/pending", () =>
            {
                var list = GetPending();

                return Results.Json(list);
            });


            // Approve device
            app.MapPost("/approve/{id}", (string id) =>
            {
                if (Connections.TryGetValue(id, out var req))
                {
                    req.Approved = true;
                    return Results.Ok();
                }

                return Results.NotFound();
            });

            // Deny device
            app.MapPost("/deny/{id}", (string id) =>
            {
                if (Connections.TryGetValue(id, out var req))
                {
                    req.Approved = false;
                    return Results.Ok();
                }

                return Results.NotFound();
            });

            // Download uploaded image by file name
            app.MapGet("/image/{file}", (string file) =>
            {
                var saveDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrpheusApp",
                    "Uploads"
                );

                var path = Path.Combine(saveDir, file);

                if (!File.Exists(path))
                    return Results.NotFound();

                var bytes = File.ReadAllBytes(path);
                return Results.File(bytes, "image/jpeg");
            });

            app.MapPost("/disconnect", async (HttpRequest req) =>
            {
                string? token = req.Headers["X-Token"];
                if (token != Token)
                    return Results.Forbid();

                var payload = await JsonSerializer.DeserializeAsync<AccessRequestPayload>(req.Body);
                if (payload == null || string.IsNullOrWhiteSpace(payload.DeviceName))
                    return Results.BadRequest();

                var connectionKeys = Connections
                    .Where(p => p.Value.DeviceName == payload.DeviceName)
                    .Select(p => p.Key)
                    .ToList();

                foreach (var key in connectionKeys)
                    Connections.Remove(key);

                Console.WriteLine($"[DISCONNECT] Device '{payload.DeviceName}' disconnected. " +
                                  $"Removed {connectionKeys.Count} entries.");

                return Results.Ok(new
                {
                    ok = true,
                    removed = connectionKeys.Count
                });
            });



            try
            {
                _app = app;
                await app.StartAsync();
            }catch(IOException _)
            {
                //TODO: if the port is taken, for example by FileMaker...
            }
            return json;
        }

        public async Task StopServerAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }
        }

        public List<PendingRequest> GetPending()
        {
            var list = Connections
                .Where(p => p.Value.Approved == null)
                .Select(p => new PendingRequest
                {
                    Key = p.Key,
                    Time = p.Value.Time,
                    DeviceName = p.Value.DeviceName
                })
                .ToList();

            return list;
        }

        public bool ApproveDevice(string id)
        {
            if (Connections.TryGetValue(id, out var req))
            {
                req.Approved = true;
                return true;
            }

            return false;
        }

        public bool DenyDevice(string id)
        {
            if (Connections.TryGetValue(id, out var req))
            {
                req.Approved = false;
                return true;
            }

            return false;
        }

        private static string GetLocalIPv4()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                var props = nic.GetIPProperties();

                // musi mieć default gateway (czyli normalną sieć)
                if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                    continue;

                // wybieramy IPv4
                var addr = props.UnicastAddresses
                    .FirstOrDefault(a =>
                        a.Address.AddressFamily == AddressFamily.InterNetwork &&
                        IsPrivateIPv4(a.Address));

                // błagam zadziałaj
                if (addr != null)
                    return addr.Address.ToString();
            }

            return "127.0.0.1";
        }

        private static bool IsPrivateIPv4(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();

            // 10.x.x.x
            if (bytes[0] == 10) return true;

            // 172.16.x.x – 172.31.x.x
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.x.x
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            return false;
        }



        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopServerAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }
    }
}
