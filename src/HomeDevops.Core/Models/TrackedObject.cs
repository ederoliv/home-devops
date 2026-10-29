using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeDevops.Models;

/// <summary>
/// Base dos objetos que fazem parte do arquivo .hdp. <see cref="Changed"/> só dispara para mudanças
/// que precisam ser gravadas em disco (estado puramente visual, como "expandido", não conta).
/// </summary>
public abstract class TrackedObject : ObservableObject
{
    public event EventHandler? Changed;

    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
