﻿namespace Aggregates.Types;

/// <summary>
/// Decides which events are required to progress a state object as a result of executing a command.
/// </summary>
/// <typeparam name="TCommand">The type of the command itself.</typeparam>
/// <typeparam name="TState">The type of the maintained state object.</typeparam>
/// <typeparam name="TEvent">The type of the event(s) that are applicable.</typeparam>
public interface ICommand<in TCommand, in TState, out TEvent>
    where TState : IState<TState, TEvent>
    where TCommand : ICommand<TCommand, TState, TEvent> {
    /// <summary>
    /// Accepts the <paramref name="state"/> to produce a sequence of events that will progress it to a new state.
    /// </summary>
    /// <param name="state">The current state to accept.</param>
    /// <returns>A sequence of events.</returns>
    IEnumerable<TEvent> Progress(TState state) =>
        ProgressAsync(state).ToEnumerable();

    /// <summary>
    /// Accepts the <paramref name="state"/> to produce a sequence of events that will progress it to a new state.
    /// </summary>
    /// <param name="state">The current state to accept.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous sequence of events.</returns>
    IAsyncEnumerable<TEvent> ProgressAsync(TState state, CancellationToken cancellationToken = default) =>
        Progress(state).ToAsyncEnumerable();

    /// <summary>
    /// Implicitly casts the given <typeparamref name="TCommand"/> to an <see cref="AggregateIdentifier"/>.
    /// </summary>
    /// <param name="instance">The command to cast.</param>
    static abstract implicit operator AggregateIdentifier(TCommand instance);
}