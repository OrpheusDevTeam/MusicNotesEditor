using System;

namespace MusicNotesEditor.LocalServer
{
    // JSON payload sent by the phone
    public sealed class AccessRequestPayload
    {
        public string DeviceName { get; set; } = "";
    }

    // Stored request
    public sealed class DeviceRequest
    {
        public string Id { get; init; } = default!;
        public DateTime Time { get; init; } = DateTime.UtcNow;
        public string DeviceName { get; init; } = "";
        public bool? Approved { get; set; } = null;
    }

}
