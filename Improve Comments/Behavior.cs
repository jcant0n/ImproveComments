// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

using System;
using System.Runtime.Serialization;
using Evergine.Common.Attributes;

namespace Evergine.Framework
{
    /// <summary>
    /// Represents a <see cref="Component"/> that has just logic, updatable through the <c>Update()</c> method.
    /// </summary>
    public abstract class Behavior : Component
    {
        /// <summary>
        /// The update order 0.5f by default (range 0f - 1f).
        /// </summary>
        internal float Order;

        /// <summary>
        /// Gets or sets the <see cref="FamilyType"/>.
        /// Every behavior is initialized with <c>Family</c> set to <c>Default</c>.
        /// Default behaviors are updated first, and any other behaviors with different <see cref="FamilyType"/> values
        /// are updated in a second row.
        /// Families that are not <c>Default</c> are updated in parallel
        /// (the list of <see cref="Behavior"/> of each <see cref="FamilyType"/> is updated in a non-deterministic sequential order).
        /// For example: if a game had three behaviors marked as "AI" and two marked as "Physics",
        /// it would launch two threads: one to update the AI behaviors and another to update the Physics ones.
        /// See <see cref="FamilyType"/> for more information.
        /// </summary>
        [DontRenderProperty]
        public FamilyType Family { get; protected set; }

        /// <summary>
        /// Gets or sets the update order.
        /// Behaviors are sorted according to this value in ascending order, so behaviors with lower values will be updated before behaviors with higher values.
        /// </summary>
        /// <value>
        /// The update order.
        /// </value>
        [RenderProperty(Tooltip = "Behaviors are sorted according to this value in ascending order, so behaviors with lower values will be updated before behaviors with higher values")]
        public float UpdateOrder
        {
            get
            {
                return this.Order;
            }

            set
            {
                if (value < 0f || value > 1f)
                {
                    throw new IndexOutOfRangeException("Argument out of range, Update order range is (0f - 1f)");
                }

                this.Order = value;

                // TODO: Sort behaviors when Update Order is changed
                ////if (this.BehaviorManager != null)
                ////{
                ////    this.BehaviorManager.SortBehaviors();
                ////}
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Behavior"/> class.
        /// </summary>
        protected Behavior()
            : this(FamilyType.DefaultBehavior)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Behavior" /> class.
        /// </summary>
        /// <param name="family">The family.</param>
        protected Behavior(FamilyType family)
        {
            this.Family = family;
            this.Order = 0.5f;
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        /// <param name="gameTime">The game time.</param>
        /// <remarks>
        /// This is only executed if the instance is active.
        /// </remarks>
        internal void BaseUpdate(TimeSpan gameTime)
        {
            try
            {
                if (this.IsStarted)
                {
                    this.Update(gameTime);
                }
            }
            catch (Exception ex) when (!this.CaptureComponentException(ex))
            {
            }
        }

        /// <inheritdoc/>
        protected override bool OnAttached()
        {
            this.Managers.BehaviorManager.AddBehavior(this);
            return base.OnAttached();
        }

        /// <inheritdoc/>
        protected override void OnDetach()
        {
            base.OnDetach();
            this.Managers.BehaviorManager.RemoveBehavior(this);
        }

        /// <summary>
        /// Allows this instance to execute custom logic during its <c>Update</c>.
        /// </summary>
        /// <param name="gameTime">The game time.</param>
        /// <remarks>
        /// This method will not be executed if the <see cref="Component"/>, or the <see cref="Entity"/>
        /// owning it are not <c>Active</c>.
        /// </remarks>
        protected abstract void Update(TimeSpan gameTime);
    }
}
