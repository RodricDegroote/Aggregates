﻿using Aggregates.Extensions;
using Aggregates.Metadata;
using System.Reflection;

namespace Aggregates.Entities;

/// <summary>
/// The root of an entity aggregate.
/// </summary>
/// <typeparam name="TState">The type of the maintained state object.</typeparam>
/// <typeparam name="TEvent">The type of the event(s) that are applicable.</typeparam>
/// <param name="State">The state of the aggregate.</param>
/// <param name="Version">The version of the aggregate when it was loaded.</param>
public sealed record EntityRoot<TState, TEvent>(TState? State, AggregateVersion Version) : IAggregateRoot where TState : IState<TState, TEvent> {
    readonly List<object> _changes = new();

    /// <summary>
    /// Gets the current state of the aggregate.
    /// </summary>
    public TState State { get; set; } = State ?? TState.Initial;

    /// <summary>
    /// Gets the sequence of changes that were applied, if any.
    /// </summary>
    /// <returns>A <see cref="IEnumerable{T}"/>.</returns>
    public IEnumerable<object> GetChanges() => _changes;

    /// <summary>
    /// Accepts the given <paramref name="command"/> in order to asynchronously progress the state of the aggregate.
    /// </summary>
    /// <param name="command">The command to accept.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    public async ValueTask AcceptAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TState, TEvent> =>
        State = await command.ProgressAsync(State, cancellationToken)
            .TapAsync(@event => _changes.Add(@event))
            .AggregateAsync(State, static (state, @event) => SetMetadata(state.Apply(@event)), cancellationToken: cancellationToken);

    static TState SetMetadata(TState state) {
        foreach (var metadata in state.GetType().GetCustomAttributes<MetadataAttribute>())
            MetadataScope.Current.Add(metadata.Create(state));

        return state;
    }

    public static implicit operator TState(EntityRoot<TState, TEvent> instance) => instance.State;
}