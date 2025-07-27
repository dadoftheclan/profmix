using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App_Mix.Interfaces
{
    /// <summary>
    /// Represents a contract for objects that can have their position within a stream or sequence set and retrieved.
    /// Although named with the 'I' prefix typically reserved for interfaces in C#, this is implemented as a concrete class.
    /// It provides a single property to manage the current position.
    /// </summary>
    /// <remarks>
    /// In C#, types prefixed with 'I' (e.g., IDisposable, IEnumerable) are conventionally interfaces.
    /// If this class were intended to define a contract for seekable behavior without providing an implementation,
    /// it would typically be declared as `public interface ISeekable { long Position { get; set; } }`.
    /// As a class, it serves as a simple data holder for a position.
    /// </remarks>
    public class ISeekable
    {
        /// <summary>
        /// Gets or sets the current position within the seekable stream or sequence.
        /// This value typically represents an offset in bytes or samples from the beginning.
        /// </summary>
        public long Position { get; set; }
    }
}
