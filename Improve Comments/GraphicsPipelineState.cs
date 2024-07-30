// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

namespace Evergine.Common.Graphics
{
    /// <summary>
    /// This class represent the GPU graphics pipeline.
    /// </summary>
    public abstract class GraphicsPipelineState : PipelineState
    {
        /// <summary>
        /// Gets the graphics pipelinestate description.
        /// </summary>
        public readonly GraphicsPipelineDescription Description;

        /// <summary>
        /// Invalidates the current viewport.
        /// </summary>
        public bool InvalidatedViewport;

        /// <summary>
        /// Gets or sets a string identifying this instance. Can be used in graphics debuggers tools.
        /// </summary>
        public abstract string Name { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsPipelineState"/> class.
        /// </summary>
        /// <param name="description">The pipelineState description.</param>
        protected GraphicsPipelineState(ref GraphicsPipelineDescription description)
        {
            this.Description = description;
        }
    }
}
