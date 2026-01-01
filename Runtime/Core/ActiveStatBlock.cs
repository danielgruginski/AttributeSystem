using System;
using System.Collections.Generic;

namespace ReactiveSolutions.AttributeSystem.Core
{
    /// <summary>
    /// Represents a "Live" instance of a StatBlock applied to a processor.
    /// Keeps track of the disposable handles for all applied modifiers so they can be removed cleanly.
    /// </summary>
    public class ActiveStatBlock : IDisposable
    {
        private readonly List<IDisposable> _handles = new List<IDisposable>();

        /// <summary>
        /// Registers a cleanup handle (usually from AttributeProcessor.AddModifier).
        /// </summary>
        public void AddHandle(IDisposable handle)
        {
            if (handle != null) _handles.Add(handle);
        }

        /// <summary>
        /// Disposes all registered handles, effectively removing the modifiers from their targets.
        /// </summary>
        public void Dispose()
        {
            foreach (var handle in _handles)
            {
                handle.Dispose();
            }
            _handles.Clear();
        }
    }
}