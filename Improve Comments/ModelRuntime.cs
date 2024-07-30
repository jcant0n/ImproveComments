// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

using Evergine.Framework.Graphics;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Evergine.Framework.Runtimes
{
    /// <summary>
    /// Base class to load 3d format in runtime.
    /// </summary>
    public abstract class ModelRuntime
    {
        /// <summary>
        /// Gets the 3d format extension.
        /// </summary>
        public abstract string Extentsion { get; }

        /// <summary>
        /// Read a 3D format file from stream and return a model asset.
        /// </summary>
        /// <param name="stream">Seeked stream.</param>
        /// <param name="materialAssigner">Material assigner.</param>
        /// <returns>Model asset.</returns>
        public abstract Task<Model> Read(Stream stream, Func<MaterialData, Task<Material>> materialAssigner = null);
    }
}
