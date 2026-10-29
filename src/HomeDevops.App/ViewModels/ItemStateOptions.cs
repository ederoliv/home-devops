using HomeDevops.Models;

namespace HomeDevops.ViewModels;

public sealed record StateOption(ItemState Value, string Label);

/// <summary>Estados disponíveis no seletor de um item, com o texto em português.</summary>
public static class ItemStateOptions
{
    public static IReadOnlyList<StateOption> All { get; } =
    [
        new(ItemState.Todo, "A fazer"),
        new(ItemState.Doing, "Em andamento"),
        new(ItemState.Done, "Concluído"),
    ];

    public static string LabelOf(ItemState state) => All.First(o => o.Value == state).Label;
}

public enum ItemFilter
{
    All,
    Feature,
    Bug,
    Done,
}
