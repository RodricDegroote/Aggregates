﻿// ReSharper disable CheckNamespace

using Aggregates.Extensions;
using Aggregates.Types;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Aggregates;

public class ReactionsOptions {
    /// <summary>
    /// Gets or sets the set of <see cref="Assembly"/> to scan for reaction types.
    /// </summary>
    public Assembly[]? Assemblies { get; set; }

    internal Action<IServiceCollection>? ConfigureServices { get; private set; }

    internal void AddConfiguration(Action<IServiceCollection> configuration) =>
        ConfigureServices = ConfigureServices.AndThen(configuration);
}

public static class ExtensionsForReactionRegistration {

    /// <summary>
    /// Registers the necessary dependencies to work with the reaction infrastructure provided by the Aggregates package.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
    /// <returns>A <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UseReactions(this IServiceCollection services, Action<ReactionsOptions> configure) {
        var options = new ReactionsOptions();
        configure(options);

        options.ConfigureServices?.Invoke(services);

        // find all implementations of IReaction and register them
        foreach (var (implType, reactionEventType, commandType, stateType, eventType) in
                 from assembly in options.Assemblies ?? AppDomain.CurrentDomain.GetAssemblies()
                 from type in assembly.GetTypes()

                 from @interface in type.GetInterfaces()
                 where @interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IReaction<,,,>)

                 let genericArgs = @interface.GetGenericArguments()

                 select (type, genericArgs[0], genericArgs[1], genericArgs[2], genericArgs[3])) {
            services.AddScoped(typeof(IReaction<,,,>).MakeGenericType(reactionEventType, commandType, stateType, eventType), implType);
        }

        return services;
    }
}