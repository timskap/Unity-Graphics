using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.GraphDelta;
using System.Linq;
using UnityEditor.ShaderGraph.Registry.Defs;

namespace UnityEditor.ShaderGraph.Registry
{
    /*
    TODOs:
        Namespaces and Context local overrides.

    Search:
        Categories, search hierachy, tooltips, etc-- how should these be handled?
    Registry:
        Unregister Builder
        Clean/Reinit Registry
        On Registry Updated
        Registration descriptors- etc.
    Errors:
        No messaging or error state handling or checking on Registry actions.
        Need an error handler for definition interface that can be used for concretization as well.
    */
    public struct Box<T> : ISerializable
    {
        public T data;

        Box(SerializationInfo info, StreamingContext context)
        {
            data = (T)info.GetValue("value", typeof(T));
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("value", data);
        }
    }


    [Serializable]
    public struct RegistryKey : ISerializable
    {
        public string Name;
        public int Version;

        public override string ToString() => $"{Name}.{Version}";
        public override int GetHashCode() => ToString().GetHashCode();
        public override bool Equals(object obj) => obj is RegistryKey rk && rk.ToString().Equals(this.ToString());

        public RegistryKey(SerializationInfo info, StreamingContext context)
        {
            Name = info.GetString("Name");
            Version = info.GetInt32("Version");
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", Name);
            info.AddValue("Version", Version);
        }
    }

    [Flags] public enum RegistryFlags
    {
        Type = 1, // The corresponding node definition is allowed to be a port.
        Func = 2, // Cannot be a port.
        Cast = 3,
        Base = 4,
    }


    public class Registry
    {
        Dictionary<RegistryKey, Defs.IRegistryEntry> builders = new Dictionary<RegistryKey, Defs.IRegistryEntry>();
        public GraphDelta.IGraphHandler defaultTopologies = GraphUtil.CreateGraph();

        public Registry()
        {
            Register<Defs.ContextBuilder>();
            Register<Defs.ReferenceNodeBuilder>();
        }

        public IEnumerable<RegistryKey> BrowseRegistryKeys() => builders.Keys;
        public INodeReader GetDefaultTopology(RegistryKey key) => defaultTopologies.GetNodeReader(key.ToString());

        public bool CastExists(RegistryKey from, RegistryKey to) => builders.Values.OfType<Defs.ICastDefinitionBuilder>().Any(e => e.GetTypeConversionMapping().Equals((from,to)));

        public bool Register<T>() where T : Defs.IRegistryEntry
        {
            var builder = Activator.CreateInstance<T>();
            var key = builder.GetRegistryKey();
            if (builders.ContainsKey(key))
                return false;
            builders.Add(key, builder);
            if(builder is INodeDefinitionBuilder && builder.GetRegistryFlags() == RegistryFlags.Func)
                defaultTopologies.AddNode(key, key.ToString(),this);
            return true;
        }

        public Defs.IContextDescriptor GetContextDescriptor(RegistryKey key)
        {
            var contextNodeBuilder = GetBuilder(key);
            var registryFlags = contextNodeBuilder.GetRegistryFlags();
            if(registryFlags == RegistryFlags.Base)
                return (Defs.IContextDescriptor)contextNodeBuilder;

            return null;
        }

        public Defs.INodeDefinitionBuilder GetNodeBuilder(RegistryKey key) => (Defs.INodeDefinitionBuilder)GetBuilder(key);
        public Defs.ITypeDefinitionBuilder GetTypeBuilder(RegistryKey key) => (Defs.ITypeDefinitionBuilder)GetBuilder(key);
        public Defs.ICastDefinitionBuilder GetCastBuilder(RegistryKey key) => (Defs.ICastDefinitionBuilder)GetBuilder(key);

        private Defs.IRegistryEntry GetBuilder(RegistryKey key) => builders.TryGetValue(key, out var builder) ? builder : null;
        public static RegistryKey ResolveKey<T>() where T : Defs.IRegistryEntry => Activator.CreateInstance<T>().GetRegistryKey();
        public static RegistryFlags ResolveFlags<T>() where T : Defs.IRegistryEntry => Activator.CreateInstance<T>().GetRegistryFlags();
    }
}
