﻿using EventStore.Client;
using System.Reflection;
using System.Security.Cryptography;

namespace Aggregates.EventStoreDB; 

static class Serialization {
    /// <summary>
    /// Returns a <see cref="Func{TResult}"/> that creates a <see cref="EventData"/> for each change.
    /// </summary>
    /// <param name="serializer">The <see cref="EventSerializerDelegate"/> to use when serializing the event payload.</param>
    /// <returns>A <see cref="Func{TResult}"/></returns>
    public static Func<Aggregate, object, EventData> CreateSerializer(EventSerializerDelegate serializer) {
        var serializedEventsPerAggregate = new Dictionary<AggregateIdentifier, int>();

        return (aggregate, @event) => {
            // attempt to get the version offset for this aggregate
            // if we don't have one yet, initialize it to zero
            if (!serializedEventsPerAggregate.TryGetValue(aggregate.Identifier, out var versionOffset))
                serializedEventsPerAggregate[aggregate.Identifier] = versionOffset = 0;

            var eventData = new EventData(
                eventId:
                CreateEventId(aggregate, @event, versionOffset),

                type:
                GetEventType(@event),

                data:
                SerializePayload(serializer, @event),

                // todo: null metadata for now, since we have no idea what to put in here.
                metadata:
                null);

            // next time round, the version offset for this aggregate will be incremented by one
            serializedEventsPerAggregate[aggregate.Identifier]++;

            return eventData;
        };
    }

    /// <summary>
    /// Creates a predictable <see cref="Guid"/> for the given <paramref name="aggregate"/>/<paramref name="event"/> combination, to be used as the unique identifier of the event.
    /// </summary>
    /// <param name="aggregate">The aggregate for which the event will be serialized.</param>
    /// <param name="event">The event that will be serialized.</param>
    /// <param name="versionOffset">The offset within the aggregate's event stream from the last persisted version.</param>
    /// <returns>A <see cref="Guid"/>.</returns>
    static Uuid CreateEventId(Aggregate aggregate, object @event, int versionOffset) {
        // event id will be a hash of aggregate id, version, event type and event hashcode, in order to ensure idempotence
        // use MD5 to compute the hash since it provides us with a 16-byte hash, which is convenient since that's the 
        // exact length of a GUID ¯\_(ツ)_/¯
        var bufferWriter = new BinaryWriter(new MemoryStream());
        bufferWriter.Write(aggregate.Identifier.ToString());
        bufferWriter.Write(aggregate.AggregateRoot.Version + versionOffset);
        bufferWriter.Write(@event.GetType().Name);
        bufferWriter.Write(@event.GetHashCode());
        bufferWriter.Flush();
        bufferWriter.BaseStream.Seek(0, SeekOrigin.Begin);

        var bufferReader = new BinaryReader(bufferWriter.BaseStream);
        return Uuid.FromGuid(new Guid(MD5.Create().ComputeHash(bufferReader.ReadBytes((int)bufferReader.BaseStream.Length))));
    }

    /// <summary>
    /// Gets the event type for the given <paramref name="event"/>. If the event is decorated with the <see cref="EventContractAttribute"/> attribute, that will be used to create the event type, otherwise the CLR type name is used.
    /// </summary>
    /// <param name="event">The event to get the event type of.</param>
    /// <returns>A <see cref="string"/>.</returns>
    static string GetEventType(object @event) {
        var eventContract = @event.GetType().GetCustomAttribute<EventContractAttribute>();
        return eventContract?.ToString() ?? @event.GetType().Name;
    }

    /// <summary>
    /// Serializes the given <paramref name="event"/> using Google Protocol Buffers.
    /// </summary>
    /// <param name="event">The event to serialize.</param>
    /// <returns>A byte array.</returns>
    static byte[] SerializePayload(EventSerializerDelegate serialize, object @event) {
        using var stream = new MemoryStream();
        serialize(stream, @event);
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new BinaryReader(stream);
        return reader.ReadBytes((int)stream.Length);
    }
}