using ITManagement.Domain.Exceptions;

namespace ITManagement.Infrastructure.Services;

/// <summary>Relay RAM có giới hạn; không persist nội dung cá nhân vào DB hoặc disk.</summary>
public sealed class OperationPayloads
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, (string Body, DateTime Expires)> values = [];
    public void Put(Guid id, string body)
    {
        lock (gate)
        {
            Prune();
            values.Remove(id);
            if (values.Values.Sum(x => (long)x.Body.Length) + body.Length > 24 * 1024 * 1024)
                throw new SimulationException(503, "RELAY_BUSY", "Hệ thống đang xử lý nhiều file, vui lòng thử lại.");
            values[id] = (body, DateTime.UtcNow.AddMinutes(10));
        }
    }
    public string? Get(Guid id)
    {
        lock (gate) { Prune(); return values.TryGetValue(id, out var value) ? value.Body : null; }
    }
    public void Remove(Guid id) { lock (gate) values.Remove(id); }
    private void Prune()
    {
        foreach (var key in values.Where(x => x.Value.Expires < DateTime.UtcNow).Select(x => x.Key).ToArray()) values.Remove(key);
    }
}
