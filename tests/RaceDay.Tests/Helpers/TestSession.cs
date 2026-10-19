using Microsoft.AspNetCore.Http;

namespace RaceDay.Tests.Helpers;

/// <summary>Dictionary-backed ISession so controllers can be unit tested without the session middleware.</summary>
public class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public string Id { get; } = Guid.NewGuid().ToString();
    public bool IsAvailable => true;
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, out byte[] value)
    {
        if (_store.TryGetValue(key, out var stored))
        {
            value = stored;
            return true;
        }

        value = Array.Empty<byte>();
        return false;
    }
}
