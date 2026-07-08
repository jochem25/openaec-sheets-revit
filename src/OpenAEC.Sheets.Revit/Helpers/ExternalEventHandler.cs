using Autodesk.Revit.UI;

namespace OpenAEC.Sheets.Revit.Helpers;

/// <summary>
/// Generieke ExternalEventHandler die async/await koppelt aan de Revit API-thread.
/// Alle Revit API-calls lopen via deze handler.
/// </summary>
public sealed class ExternalEventHandler : IExternalEventHandler
{
    private readonly string _name;
    private readonly Queue<(Action<UIApplication> Action, TaskCompletionSource<object?> Tcs)> _queue = new();
    private ExternalEvent? _externalEvent;

    public ExternalEventHandler(string name)
    {
        _name = name;
    }

    /// <summary>Aanroepen binnen een geldige Revit API-context (command of startup).</summary>
    public void Initialize()
    {
        _externalEvent ??= ExternalEvent.Create(this);
    }

    public Task ExecuteAsync(Action<UIApplication> action)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_queue)
        {
            _queue.Enqueue((action, tcs));
        }
        _externalEvent?.Raise();
        return tcs.Task;
    }

    public Task<TResult> ExecuteAsync<TResult>(Func<UIApplication, TResult> func)
    {
        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var wrapped = new Action<UIApplication>(app => tcs.TrySetResult(func(app)));

        var inner = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_queue)
        {
            _queue.Enqueue((app =>
            {
                try
                {
                    wrapped(app);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    throw;
                }
            }, inner));
        }
        _externalEvent?.Raise();
        return tcs.Task;
    }

    public void Execute(UIApplication app)
    {
        while (true)
        {
            (Action<UIApplication> Action, TaskCompletionSource<object?> Tcs) item;
            lock (_queue)
            {
                if (_queue.Count == 0) break;
                item = _queue.Dequeue();
            }

            try
            {
                item.Action(app);
                item.Tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                item.Tcs.TrySetException(ex);
            }
        }
    }

    public string GetName() => _name;
}
