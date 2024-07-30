// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

using System;
using System.Runtime.Serialization;
using Evergine.Common.Attributes;
using Evergine.Framework.Graphics;
using Evergine.Framework.Managers;
using Evergine.Mathematics;

namespace Evergine.Framework
{
    /// <summary>
    /// Represents a <see cref="Component"/> that can be painted.
    /// </summary>
    public abstract class Drawable : Component
    {
        /// <summary>
        /// Gets or sets the render manager.
        /// </summary>
        [BindSceneManager]
        [IgnoreEvergine]
        public RenderManager RenderManager { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the culling is enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if culling is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsCullingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the drawable bounding box.
        /// </summary>
        /// <remarks>It is used in the culling phase. If you not specify any BoundingBox, the drawable will not culled.</remarks>
        public BoundingBox? BoundingBox
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the bounding box will be drawn.
        /// </summary>
        [DontRenderProperty]
        public bool DebugBoundingbox { get; set; }

        /// <summary>
        /// Gets or sets the order bias of the material.
        /// This value is used to modify the rendering order of the meshes.
        /// </summary>
        [RenderPropertyAsInput(minLimit: -512, maxLimit: 511, AsSlider = true, DesiredChange = 1, DesiredLargeChange = 5)]
        public int OrderBias { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Drawable"/> class.
        /// </summary>
        public Drawable()
        {
            this.IsCullingEnabled = true;
            this.DebugBoundingbox = true;
        }

        /// <summary>
        /// Allows to perform custom drawing.
        /// </summary>
        /// <param name="drawContext">The draw context.</param>
        /// <remarks>
        /// This method will only be called if all the following points are true:
        /// <list type="bullet">
        ///    <item>
        ///         <description>The entity passes the culling test.</description>
        ///    </item>
        ///    <item>
        ///        <description>The parent of the owner <see cref="Entity"/> of the <see cref="Drawable"/> cascades its visibility to its children and it is visible.</description>
        ///    </item>
        ///    <item>
        ///        <description>The <see cref="Drawable"/> is active.</description>
        ///    </item>
        ///    <item>
        ///        <description>The owner <see cref="Entity"/> of the <see cref="Drawable"/> is active and visible.</description>
        ///    </item>
        /// </list>
        /// </remarks>
        public abstract void Draw(DrawContext drawContext);

        /// <summary>
        /// Allows to perform custom drawing.
        /// </summary>
        /// <param name="drawContext">The draw context.</param>
        public void BaseDraw(DrawContext drawContext)
        {
            if (this.RenderManager.DebugLines)
            {
                try
                {
                    this.DrawDebugLines();
                }
                catch (Exception ex) when (!this.CaptureComponentException(ex))
                {
                }
            }

            try
            {
                this.Draw(drawContext);
            }
            catch (Exception ex) when (!this.CaptureComponentException(ex))
            {
            }
        }

        /// <inheritdoc/>
        protected override bool OnAttached()
        {
            var isOk = base.OnAttached();

            this.RenderManager?.AddDrawable(this);
            return isOk;
        }

        /// <inheritdoc/>
        protected override void OnDetach()
        {
            this.RenderManager?.RemoveDrawable(this);
            base.OnDetach();
        }

        /// <summary>
        /// Helper method that draws debug lines.
        /// </summary>
        /// <remarks>
        /// This method will only work on debug mode and if RenderManager.DebugLines />
        /// is set to <c>true</c>.
        /// </remarks>
        protected virtual void DrawDebugLines()
        {
        }
    }
}
