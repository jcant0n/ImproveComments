// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Evergine.Common;
using Evergine.Common.Dependencies;
using Evergine.Common.Helpers;
using Evergine.Framework.Assets.AssetSources.Entities;
using Evergine.Framework.Exceptions;
using Evergine.Framework.Helpers;
using Evergine.Framework.Managers;
using Evergine.Framework.Particles.Asset;
using Evergine.Framework.Prefabs;
using Evergine.Framework.Services;

namespace Evergine.Framework
{
    /// <summary>
    /// This class represents a game entity, which is basically a container of <see cref="Component"/> types,
    /// which are the ones that provide the actual game logic.
    /// <see cref="Entity"/> types are contained in <see cref="Scene"/> ones, which handle how
    /// to update and draw them.
    /// </summary>
    /// <remarks>
    /// <see cref="Entity"/> types can be organized in trees, with an entity containing others and so on.
    /// </remarks>
    /// <example>
    /// This is an example on how to create an <see cref="Entity"/>.
    /// Take into account that the *AddComponent()* and *RemoveComponent()* methods are designed to allow method chaining,
    /// so this type of code can be written when creating or configuring an <see cref="Entity"/>:
    /// <code>
    ///     var primitive = new Entity("Primitive")
    ///                         .AddComponent(new Transform3D())
    ///                         .AddComponent(new Spinner() { AxisTotalIncreases = new Vector3(0.01f, 0.02f, 0.01f) })
    ///                         .AddComponent(new CubeMesh())
    ///                         .AddComponent(new MeshRenderer())
    ///                         .AddComponent(new MaterialComponent() { Material = myMaterial });
    /// </code>
    ///  #### Recipes and samples.
    /// <list type="bullet">
    ///    <item>
    ///        <description>[Component based Architecture recipe](../recipes/Basic/Component-based-Architecture.md)</description>
    ///    </item>
    /// </list>
    /// </example>
    public class Entity : PrefabInstanceObject
    {
        /// <summary>
        /// The separator char used in EntityPath property.
        /// </summary>
        public static readonly char PathSeparatorChar = '.';

        /// <summary>
        /// The separator string.
        /// </summary>
        public static readonly string PathSelf = "[this]";

        /// <summary>
        /// The separator string.
        /// </summary>
        public static readonly string PathParent = "[parent]";

        /// <summary>
        /// The separator string.
        /// </summary>
        public static readonly string PathSeparatorString = PathSeparatorChar.ToString();

        /// <summary>
        /// Strings that are not valid in an entity name.
        /// </summary>
        public static readonly string[] InvalidEntityNameStrings = new[] { PathSeparatorString, PathSelf, PathParent };

        /// <summary>
        /// Number of instances created.
        /// </summary>
        private static int instances;

        /// <summary>
        /// Parent <see cref="Entity"/> of this instance.
        /// </summary>
        private Entity parent;

        /// <summary>
        /// Collection of children <see cref="Entity"/> instances.
        /// </summary>
        private Dictionary<Guid, Entity> childEntities;

        /// <summary>
        /// List of children <see cref="Entity"/> instances (used to maintain order).
        /// </summary>
        private List<Entity> childList;

        /// <summary>
        /// The <see cref="Component"/> instances list that provides the
        /// functionality to this instance.
        /// </summary>
        private List<Component> componentList;

        /// <summary>
        /// The entity name.
        /// </summary>
        private string name;

        /// <summary>
        /// The entity tag.
        /// </summary>
        private string tag;

        /// <summary>
        /// The associated entity manager.
        /// </summary>
        internal EntityManager entityManager;

        /// <summary>
        /// Whether this entity is static.
        /// </summary>
        private bool isStatic;

        /// <summary>
        /// This variable indicates if this entity is enabled, taking its hierarchy into account.
        /// </summary>
        private bool isHierarchyEnabled;

        /// <summary>
        /// Special entity behavior flags.
        /// </summary>
        public HideFlags Flags;

        /// <summary>
        /// Occurs when an <see cref="Entity"/> is added as child of this entity.
        /// </summary>
        public event EventHandler<Entity> ChildAdded;

        /// <summary>
        /// Occurs when an <see cref="Entity"/> child is detached from this entity.
        /// </summary>
        public event EventHandler<Entity> ChildDetached;

        /// <summary>
        /// Occurs when an <see cref="Entity"/> child order has been changed.
        /// </summary>
        public event EventHandler<Entity> ChildOrderChanged;

        /// <summary>
        /// Occurs when a <see cref="Component"/> is added to this entity.
        /// </summary>
        public event EventHandler<Component> ComponentAdded;

        /// <summary>
        /// Occurs when a <see cref="Component"/> is detached from this entity.
        /// </summary>
        public event EventHandler<Component> ComponentDetached;

        /// <summary>
        /// Occurs when a valid name is set to an entity.
        /// </summary>
        public event EventHandler<ValidNameEventArgs> CheckValidName;

        /// <summary>
        /// Occurs when the entity's name changes.
        /// </summary>
        public event EventHandler<NameEventArgs> NameChanged;

        /// <summary>
        /// Fired when the entity tag has been changed.
        /// </summary>
        public event EventHandler<ValueChanged<string>> TagChanged;

        /// <inheritdoc/>
        protected internal override bool ShouldBeActivated => this.isHierarchyEnabled;

        /// <summary>
        /// Gets the Entity path. The entity path is a string
        /// that conform the path in the EntityManager to obtain the instance.
        /// </summary>
        public string EntityPath
        {
            get
            {
                if (this.IsDestroyed)
                {
                    throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
                }

                return EntityPathHelper.PathFromEntity(this);
            }
        }

        /// <summary>
        /// Gets or sets the name of the instance.
        /// </summary>
        public string Name
        {
            get
            {
                return this.name;
            }

            set
            {
                if (this.IsDestroyed)
                {
                    throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
                }

                if (this.name == value)
                {
                    return;
                }

                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("The new name for this entity is empty or null.");
                }

                string oldName = this.name;

                if (!IsValidName(value))
                {
                    throw new InvalidOperationException("The new name for this entity is not valid. Cannot contains the '.' characters.");
                }

                if (this.CheckValidName != null)
                {
                    var args = new ValidNameEventArgs(value);
                    this.CheckValidName(this, args);

                    if (args.Valid == false)
                    {
                        throw new InvalidOperationException("The new name for this entity is not valid.");
                    }
                    else
                    {
                        this.name = value;
                        this.NameChanged?.Invoke(this, new NameEventArgs(oldName, this.name));
                    }
                }
                else
                {
                    this.name = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the parent <see cref="Entity"/>.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">Entity have been disposed.</exception>
        public Entity Parent
        {
            get
            {
                return this.parent;
            }

            set
            {
                if (this.IsDestroyed)
                {
                    throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
                }

                if (this.parent != value)
                {
                    this.parent?.DetachChild(this);
                    value.AddChild(this);
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="Scene" /> that contains this instance.
        /// </summary>
        public Scene Scene => this.entityManager?.Scene;

        /// <summary>
        /// Gets the <see cref="EntityManager" /> that contains this instance.
        /// </summary>
        public EntityManager EntityManager => this.entityManager;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is static. Once an entity is initialized, this cannot be displaced, rotated or scaled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is static; otherwise, <c>false</c>.
        /// </value>
        public bool IsStatic
        {
            get => this.isStatic;
            set => this.isStatic = value;
        }

        // TODO: Is Final Static
        /////// <summary>
        /////// Gets a value indicating whether this instance should be treated as a static entity.
        /////// </summary>
        /////// <value>
        ///////   <c>true</c> if this instance is static; otherwise, <c>false</c>.
        /////// </value>
        ////public bool IsFinalStatic
        ////{
        ////    get
        ////    {
        ////        if (this.IsLoaded)
        ////        {
        ////            return this.isStatic && this.entityManager.IsStaticEntitiesAllowed;
        ////        }
        ////        else
        ////        {
        ////            return this.isStatic;
        ////        }
        ////    }
        ////}

        /// <summary>
        /// Gets the <see cref="Component"/> collection of this instance.
        /// </summary>
        public IEnumerable<Component> Components => this.componentList.AsEnumerable();

        /// <summary>
        /// Gets the children <see cref="Entity"/> of this instance.
        /// </summary>
        public IEnumerable<Entity> ChildEntities => this.childList.AsEnumerable();

        /// <summary>
        /// Gets the number of children <see cref="Entity"/> actually contained in this instance.
        /// </summary>
        public int NumChildren => this.childList.Count;

        /// <summary>
        /// Gets or sets the Tag of the instance.
        /// </summary>
        public string Tag
        {
            get
            {
                return this.tag;
            }

            set
            {
                if (this.IsDestroyed)
                {
                    throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
                }

                if (this.tag == value)
                {
                    return;
                }

                string oldTag = this.tag;
                this.tag = value;

                this.entityManager?.NotifyTagChanged(this, oldTag, value);
                this.TagChanged?.Invoke(this, new ValueChanged<string>(oldTag, value));
            }
        }

        /// <summary>
        /// Gets a value indicating whether this entity is the root of a prefab instance.
        /// </summary>
        public bool IsPrefabInstanceRoot
        {
            get
            {
                if (this.IsPrefabInstance)
                {
                    if ((this.Parent == null) || (this.Parent.PrefabSource != this.PrefabSource))
                    {
                        return true;
                    }

                    return false;
                }

                return false;
            }
        }

        /// <inheritdoc/>
        protected override void RefreshPrefab(Prefab newValue)
        {
            if (!this.IsUpdatingPrefab && this.IsAttached && this.IsPrefabInstanceRoot)
            {
                var oldValue = this.PrefabSource;

                var prefabInstance = new PrefabInstanceModel(this);
                prefabInstance.Source = newValue;

                if (this.Parent != null)
                {
                    var parent = this.Parent;
                    parent.DetachChild(this);
                    var newEntity = prefabInstance.Entity;
                    parent.AddChild(newEntity);
                }
                else
                {
                    var entityManager = this.EntityManager;
                    entityManager.Detach(this);
                    var newEntity = prefabInstance.Entity;
                    entityManager.Add(newEntity);
                }
            }
            else
            {
                base.RefreshPrefab(newValue);
            }
        }

        internal List<Entity> ChildList => this.childList;

        /// <summary>
        /// If the prefab is being updated.
        /// </summary>
        public bool IsUpdatingPrefab;

        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// By default, the <see cref="Entity"/> is visible and active.
        /// </summary>
        public Entity()
            : this(NextDefaultName())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Entity"/> class.
        /// By default, the <see cref="Entity"/> is visible and active.
        /// </summary>
        /// <param name="name">The name of the entity.</param>
        /// <exception cref="System.ArgumentNullException">If name is null or empty.</exception>
        public Entity(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            this.Name = name;

            instances++;
            this.childEntities = new Dictionary<Guid, Entity>();
            this.childList = new List<Entity>();
            this.componentList = new List<Component>();
        }

        /// <summary>
        /// Return a default entity name.
        /// </summary>
        /// <returns>The entity name.</returns>
        public static string NextDefaultName()
        {
            return $"Entity_{instances++}";
        }

        /// <inheritdoc/>
        protected override void OnLoaded()
        {
        }

        /// <inheritdoc/>
        protected override bool OnAttached()
        {
            this.RefreshIsHierarchyEnabled();

            // Update entity manager on children
            this.RefreshEntityManager(this.entityManager);

            // Attach components
            var isOk = true;
            for (int i = 0; i < this.componentList.Count; i++)
            {
                var component = this.componentList[i];

                try
                {
                    isOk &= component.Attach(this);
                }
                catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                {
                }
            }

            if (!isOk)
            {
                var componentsID = new List<string>();
                foreach (var component in this.componentList)
                {
                    if (!component.IsAttached)
                    {
                        componentsID.Add(component.Id.ToString());
                    }
                }

                Trace.TraceError($"Entity [{this.EntityPath}] failed to attach components: [{string.Join(", ", componentsID)}]");
            }

            this.entityManager?.NotifyTagChanged(this, null, this.tag);

            // Attach children
            foreach (var child in this.childList)
            {
                child.Attach();
            }

            return true;
        }

        /// <inheritdoc/>
        internal override void Activate()
        {
            base.Activate();

            if (this.IsStarted)
            {
                // Start component
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.IsActivated && !component.IsStarted)
                    {
                        try
                        {
                            component.BaseStart();
                        }
                        catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                        {
                        }
                    }
                }

                // Start children
                foreach (var child in this.childList)
                {
                    if (child.IsActivated && !child.IsStarted)
                    {
                        child.BaseStart();
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnActivated()
        {
            if (!this.ShouldBeActivated)
            {
                return;
            }

            for (int i = 0; i < this.componentList.Count; i++)
            {
                var component = this.componentList[i];
                if (component.State == AttachableObjectState.Deactivated)
                {
                    try
                    {
                        component.Activate();
                    }
                    catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                    {
                    }
                }
            }

            // Activate children
            foreach (var child in this.childList)
            {
                if (child.ShouldBeActivated && child.State == AttachableObjectState.Deactivated)
                {
                    child.Activate();
                }
            }
        }

        /// <inheritdoc/>
        protected override void Start()
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                var component = this.componentList[i];
                if (component.IsActivated && !component.IsStarted && !this.IsStarted)
                {
                    try
                    {
                        component.BaseStart();
                    }
                    catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                    {
                    }
                }
            }

            // Start children
            foreach (var child in this.childList)
            {
                if (child.IsActivated && !child.IsStarted && !this.IsStarted)
                {
                    child.BaseStart();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnDeactivated()
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                var component = this.componentList[i];
                if (component.IsActivated)
                {
                    try
                    {
                        component.Deactivate();
                    }
                    catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                    {
                    }
                }
            }

            // Deactivate children
            foreach (var child in this.childList)
            {
                if (child.IsActivated)
                {
                    child.Deactivate();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnDetach()
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                var component = this.componentList[i];
                if (component.State == AttachableObjectState.Deactivated)
                {
                    try
                    {
                        component.Detach();
                    }
                    catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                    {
                    }
                }
            }

            // Detach children
            foreach (var child in this.childList)
            {
                if (child.State == AttachableObjectState.Deactivated)
                {
                    child.Detach();
                }
            }

            this.RefreshEntityManager(null);
        }

        /// <inheritdoc/>
        protected override void OnDestroy()
        {
            for (int i = 0; i < this.componentList.Count; i++)
            {
                var component = this.componentList[i];
                try
                {
                    component.ForceState(AttachableObjectState.Destroyed);
                }
                catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                {
                }
            }

            this.componentList.Clear();

            // Destroy children
            foreach (var child in this.childList)
            {
                child.ForceState(AttachableObjectState.Destroyed);
            }

            this.childEntities.Clear();
            this.childList.Clear();
        }

        /// <summary>
        /// Reattach all components.
        /// </summary>
        public void ReattachComponents()
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (this.IsAttached)
            {
                // Deactivate all components
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.IsActivated)
                    {
                        try
                        {
                            component.Deactivate();
                        }
                        catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                        {
                        }
                    }
                }

                // Detach all components
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.State == AttachableObjectState.Deactivated)
                    {
                        try
                        {
                            component.Detach();
                        }
                        catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                        {
                        }
                    }
                }

                // Attach all components
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.State == AttachableObjectState.Detached)
                    {
                        try
                        {
                            component.Attach(this);
                        }
                        catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                        {
                        }
                    }
                }

                // Activate all components
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.State == AttachableObjectState.Deactivated)
                    {
                        try
                        {
                            component.Activate();
                        }
                        catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                        {
                        }
                    }
                }

                // Start all components
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.IsActivated)
                    {
                        try
                        {
                            component.BaseStart();
                        }
                        catch (Exception ex) when (!this.CaptureComponentException(component, ex))
                        {
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a child <see cref="Entity"/> to this instance.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/> to add.</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">If entity is null.</exception>
        /// <exception cref="InvalidOperationException">If entity was added to itself.</exception>
        /// <exception cref="InvalidOperationException">If entity was already added to another <see cref="Entity"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// If there already was an <see cref="Entity"/> with the same name as entity added to this instance.
        /// </exception>
        public Entity AddChild(Entity entity)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(Entity));
            }

            if (entity == this)
            {
                throw new InvalidOperationException(string.Format("Can not add an entity as a child of itself (\"{0}\").", entity.Name));
            }

            if (entity.entityManager != null)
            {
                throw new InvalidOperationException(string.Format("Entity \"{0}\" has already added to EntityManager.", entity.Name));
            }

            if (entity.parent != null)
            {
                throw new InvalidOperationException(string.Format("Entity \"{0}\" has already been assigned to another entity.", entity.Name));
            }

            if (this.childEntities.ContainsKey(entity.Id))
            {
                throw new InvalidOperationException(string.Format("An entity can not contain two entities with the same Id (\"{0}\").", entity.Name));
            }

            // Load the entity
            entity.parent = this;
            entity.Load();

            this.childEntities.Add(entity.Id, entity);
            this.childList.Add(entity);

            // If name if changed we need to update the childEntities dictionary
            entity.RefreshEntityManager(this.entityManager);
            entity.CheckValidName += this.OnChildValidName;
            entity.NameChanged += this.OnChildNameChanged;

            if (this.IsAttached && entity.Attach())
            {
                if (this.IsActivated && entity.IsEnabled)
                {
                    entity.Activate();
                    if (this.IsStarted)
                    {
                        entity.BaseStart();
                    }
                }
            }

            this.ChildAdded?.Invoke(this, entity);

            this.entityManager?.RegisterEntity(entity);
            this.entityManager?.FireEntityAdded(entity);

            return this;
        }

        /// <summary>
        /// Removes a child <see cref="Entity"/> from this instance.
        /// </summary>
        /// <param name="id">Id of the child <see cref="Entity"/>.</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">If entityName is null or empty.</exception>
        public Entity RemoveChild(Guid id)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (id == Guid.Empty)
            {
                throw new ArgumentException("id can't be empty");
            }

            if (this.childEntities.TryGetValue(id, out var child))
            {
                this.InternalRemoveChild(child);
            }

            return this;
        }

        /// <summary>
        /// Removes a child <see cref="Entity"/> from this instance.
        /// </summary>
        /// <param name="childName">Name of the child <see cref="Entity"/>.</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">If entityName is null or empty.</exception>
        public Entity RemoveChild(string childName)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (string.IsNullOrEmpty(childName))
            {
                throw new ArgumentException("id can't be empty");
            }

            var child = this.childEntities.Values.Where(c => c.name == childName).FirstOrDefault();
            if (child != null)
            {
                this.InternalRemoveChild(child);
            }

            return this;
        }

        /// <summary>
        /// Removes a child <see cref="Entity"/> from this instance.
        /// </summary>
        /// <param name="entity">The <see cref="Entity"/> to remove.</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">If entity is null.</exception>
        public Entity RemoveChild(Entity entity)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(Entity));
            }

            if (this.childEntities.ContainsKey(entity.Id))
            {
                this.InternalRemoveChild(entity);
            }

            return this;
        }

        /// <summary>
        /// Detaches the child <see cref="Entity"/>.
        /// </summary>
        /// <param name="id">Id of the entity.</param>
        /// <returns>The entity detached or null otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">If entityName is null or empty.</exception>
        public Entity DetachChild(Guid id)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (id == Guid.Empty)
            {
                throw new ArgumentException("Invalid id");
            }

            if (this.childEntities.TryGetValue(id, out var child))
            {
                this.InternalDetachChild(child);
                return child;
            }

            return null;
        }

        /// <summary>
        /// Detaches the child <see cref="Entity"/>.
        /// </summary>
        /// <param name="childName">Name of the child <see cref="Entity"/>.</param>
        /// <returns>The entity detached or null otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">If entityName is null or empty.</exception>
        public Entity DetachChild(string childName)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (string.IsNullOrEmpty(childName))
            {
                throw new ArgumentException("id can't be empty");
            }

            var child = this.childEntities.Values.Where(c => c.name == childName).FirstOrDefault();
            if (child != null)
            {
                this.InternalDetachChild(child);
                return child;
            }

            return null;
        }

        /// <summary>
        /// Detaches the child <see cref="Entity"/>.
        /// </summary>
        /// <param name="entity">Name of the entity.</param>
        /// <returns>The entity detached or null otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">If entity is null or empty.</exception>
        public Entity DetachChild(Entity entity)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(Entity));
            }

            if (this.childEntities.ContainsKey(entity.Id))
            {
                this.InternalDetachChild(entity);
                return entity;
            }

            return null;
        }

        /// <summary>
        /// Finds the first child <see cref="Entity"/> in this instance with a given entity path.
        /// </summary>
        /// <param name="entityPath">The path to the entity.</param>
        /// <returns>The entity of the path.</returns>
        public Entity Find(string entityPath)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (string.IsNullOrEmpty(entityPath))
            {
                throw new ArgumentNullException("entityName");
            }

            return EntityPathHelper.EntityFromPath(entityPath, this, this.entityManager);
        }

        /// <summary>
        /// Finds a child <see cref="Entity"/> in this instance with a id.
        /// </summary>
        /// <param name="id">Id of the child <see cref="Entity"/>.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>The searched child <see cref="Entity"/>, or null if no matching child was found.</returns>
        /// <exception cref="System.ArgumentNullException">If entityName is null or empty.</exception>
        public Entity FindChild(Guid id, bool isRecursive = false)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (id == Guid.Empty)
            {
                throw new ArgumentException("id");
            }

            var searchQueue = new Queue<Entity>();
            searchQueue.Enqueue(this);

            Entity child = null;

            if (isRecursive)
            {
                while (searchQueue.Count > 0)
                {
                    Entity entityFromQueue = searchQueue.Dequeue();

                    if (entityFromQueue.childEntities.TryGetValue(id, out child))
                    {
                        break;
                    }

                    foreach (var descendant in entityFromQueue.ChildEntities)
                    {
                        searchQueue.Enqueue(descendant);
                    }
                }
            }
            else
            {
                this.childEntities.TryGetValue(id, out child);
            }

            return child;
        }

        /// <summary>
        /// Finds the first child <see cref="Entity"/> in this instance with a given name.
        /// </summary>
        /// <param name="entityName">Name of the child <see cref="Entity"/>.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>The searched child <see cref="Entity"/>, or null if no matching child was found.</returns>
        /// <exception cref="System.ArgumentNullException">If entityName is null or empty.</exception>
        public Entity FindChild(string entityName, bool isRecursive = false)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (string.IsNullOrEmpty(entityName))
            {
                throw new ArgumentNullException("entityName");
            }

            var searchQueue = new Queue<Entity>();
            searchQueue.Enqueue(this);

            Entity child = null;

            if (isRecursive)
            {
                while (searchQueue.Count > 0)
                {
                    Entity entityFromQueue = searchQueue.Dequeue();

                    if (entityFromQueue.TryGetChildByName(entityName, out child))
                    {
                        break;
                    }

                    foreach (var descendant in entityFromQueue.ChildEntities)
                    {
                        searchQueue.Enqueue(descendant);
                    }
                }
            }
            else
            {
                this.TryGetChildByName(entityName, out child);
            }

            return child;
        }

        /// <summary>
        /// Find children <see cref="Entity"/> in this instance by Tag.
        /// </summary>
        /// <param name="tag">The tag to filter.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <param name="skipOwner">Ignore the owner entity.</param>
        /// <returns>A collection of <see cref="Entity"/>, with all children that match the specified Tag.</returns>
        /// <exception cref="System.ArgumentNullException">If tag is null or empty.</exception>
        public IEnumerable<Entity> FindParentsByTag(string tag, bool isRecursive = false, bool skipOwner = true)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException("tag");
            }

            if (!skipOwner && this.tag == tag)
            {
                yield return this;
            }

            var currentParent = this.Parent;

            if (currentParent != null)
            {
                if (isRecursive)
                {
                    while (currentParent != null)
                    {
                        if (currentParent.Tag == tag)
                        {
                            yield return currentParent;
                        }

                        currentParent = currentParent.Parent;
                    }
                }
                else
                {
                    if (this.parent.Tag == tag)
                    {
                        yield return this.parent;
                    }
                }
            }
        }

        /// <summary>
        /// Find children <see cref="Entity"/> in this instance by Tag.
        /// </summary>
        /// <param name="tag">The tag to filter.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <param name="skipOwner">Ignore the owner entity.</param>
        /// <returns>A collection of <see cref="Entity"/>, with all children that match the specified Tag.</returns>
        /// <exception cref="System.ArgumentNullException">If tag is null or empty.</exception>
        public IEnumerable<Entity> FindChildrenByTag(string tag, bool isRecursive = false, bool skipOwner = true)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (string.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException("tag");
            }

            if (!skipOwner && this.tag == tag)
            {
                yield return this;
            }

            if (isRecursive)
            {
                // We don't want visit again the root elements, so that we only enqueue the children
                var searchQueue = new Queue<Entity>(this.ChildEntities);

                while (searchQueue.Count > 0)
                {
                    Entity entityFromQueue = searchQueue.Dequeue();

                    // Add all children
                    foreach (Entity child in entityFromQueue.ChildEntities)
                    {
                        searchQueue.Enqueue(child);
                    }

                    if (entityFromQueue.Tag == tag)
                    {
                        yield return entityFromQueue;
                    }
                }
            }
            else
            {
                foreach (var child in this.ChildEntities)
                {
                    if (child.Tag == tag)
                    {
                        yield return child;
                    }
                }
            }
        }

        /// <summary>
        /// Adds a <see cref="Component"/> to this instance.
        /// </summary>
        /// <param name="component">The <see cref="Component"/> to add.</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        /// <exception cref="System.ArgumentException">If component is null.</exception>
        /// <exception cref="InvalidOperationException">If component was already added to another <see cref="Entity"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// If there already was a <see cref="Component"/> of the same type as component added to this instance.
        /// </exception>
        public Entity AddComponent(Component component)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (component == null)
            {
                throw new ArgumentNullException("component");
            }

            if (component.Owner != null && component.Owner != this)
            {
                throw new InvalidOperationException(string.Format("Component \"{0}\" has already been assigned to another entity.", component.GetTypeName()));
            }

            if (this.componentList.Contains(component))
            {
                throw new InvalidOperationException(string.Format("Component \"{0}\" has already assigned to this entity.", component.GetTypeName()));
            }

            Type t = component.GetType();
            bool allowMultipleInstances = t.GetTypeInfo().GetCustomAttribute<AllowMultipleInstances>(true) != null;
            if (!allowMultipleInstances)
            {
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var c = this.componentList[i];
                    if (c.GetType() == t)
                    {
                        throw new InvalidOperationException(string.Format("An entity can not contains two components of the same type (\"{0}\")", t));
                    }
                }
            }

            try
            {
                component.Load();
            }
            catch (Exception ex) when (!this.CaptureComponentException(component, ex))
            {
            }

            this.componentList.Add(component);

            try
            {
                if (this.IsAttached)
                {
                    if (component.Attach(this))
                    {
                        if (this.IsActivated)
                        {
                            component.Activate();

                            if (component.IsActivated && this.IsStarted)
                            {
                                component.BaseStart();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (!this.CaptureComponentException(component, ex))
            {
            }

            this.ComponentAdded?.Invoke(this, component);

            return this;
        }

        /// <summary>
        /// Removes a <see cref="Component"/> from this instance.
        /// </summary>
        /// <typeparam name="T">Type of the <see cref="Component"/> to remove.</typeparam>
        /// <param name = "isExactType" >if set to<c>true</c> [is exact type].</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity RemoveComponent<T>(bool isExactType = true)
            where T : Component
        {
            return this.RemoveComponent(typeof(T), isExactType);
        }

        /// <summary>
        /// Removes a <see cref="Component" /> from this instance.
        /// </summary>
        /// <param name="componentType">Type of the component.</param>
        /// <param name="isExactType">if set to <c>true</c> [is exact type].</param>
        /// <returns>
        /// This instance.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">The entity is disposed.</exception>
        /// <exception cref="System.NullReferenceException">componentType
        /// or
        /// componentType.</exception>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity RemoveComponent(Type componentType, bool isExactType = true)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (componentType == null)
            {
                throw new NullReferenceException("componentType");
            }

            if (!ReflectionHelper.IsAssignableFrom(typeof(Component), componentType))
            {
                throw new NullReferenceException("componentType");
            }

            var component = this.InternalSearchComponent(componentType, isExactType);
            if (component != null)
            {
                // Remove the component
                this.InternalRemoveComponent(component, true);
            }

            return this;
        }

        /// <summary>
        /// Removes a <see cref="Component" /> from this instance.
        /// </summary>
        /// <param name="component">The component to remove.</param>
        /// <returns>
        /// This instance.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">The entity is disposed.</exception>
        /// <exception cref="System.NullReferenceException">componentType
        /// or
        /// componentType.</exception>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity RemoveComponent(Component component)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (component == null)
            {
                throw new NullReferenceException("component");
            }

            this.InternalRemoveComponent(component, true);

            return this;
        }

        /// <summary>
        /// Removes a <see cref="Component"/> from this instance.
        /// </summary>
        /// <typeparam name="T">Type of the <see cref="Component"/> to remove.</typeparam>
        /// <param name="detachedComponent">The detached component instance.</param>
        /// <param name = "isExactType" >if set to<c>true</c> [is exact type].</param>
        /// <returns>This instance.</returns>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity DetachComponent<T>(out T detachedComponent, bool isExactType = true)
            where T : Component
        {
            this.DetachComponent(typeof(T), out var component, isExactType);
            detachedComponent = component as T;

            return this;
        }

        /// <summary>
        /// Removes a <see cref="Component" /> from this instance.
        /// </summary>
        /// <param name="componentType">Type of the component.</param>
        /// <param name="detachedComponent">The detached component instance.</param>
        /// <param name="isExactType">if set to <c>true</c> [is exact type].</param>
        /// <returns>
        /// This instance.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">The entity is disposed.</exception>
        /// <exception cref="System.NullReferenceException">componentType
        /// or
        /// componentType.</exception>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity DetachComponent(Type componentType, out Component detachedComponent, bool isExactType = true)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (componentType == null)
            {
                throw new NullReferenceException("componentType");
            }

            if (!ReflectionHelper.IsAssignableFrom(typeof(Component), componentType))
            {
                throw new NullReferenceException("componentType");
            }

            detachedComponent = this.InternalSearchComponent(componentType, isExactType);
            if (detachedComponent != null)
            {
                // Remove the component
                this.InternalRemoveComponent(detachedComponent, false);
            }

            return this;
        }

        /// <summary>
        /// Removes a <see cref="Component" /> from this instance.
        /// </summary>
        /// <param name="component">The component to remove.</param>
        /// <returns>
        /// This instance.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">The entity is disposed.</exception>
        /// <exception cref="System.NullReferenceException">componentType
        /// or
        /// componentType.</exception>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity DetachComponent(Component component)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (component == null)
            {
                throw new NullReferenceException("component");
            }

            this.InternalRemoveComponent(component, false);

            return this;
        }

        /// <summary>
        /// Search a <see cref="Component" /> by its type.
        /// </summary>
        /// <param name="componentType">Type of the component.</param>
        /// <param name="isExactType">if set to <c>true</c> [is exact type].</param>
        /// <returns>The component.</returns>
        private Component InternalSearchComponent(Type componentType, bool isExactType)
        {
            if (isExactType)
            {
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.GetType() == componentType)
                    {
                        return component;
                    }
                }
            }
            else
            {
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (ReflectionHelper.IsAssignableFrom(componentType, component.GetType()))
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Removes a <see cref="Component" /> from this instance.
        /// </summary>
        /// <param name="component">The component to remove.</param>
        /// <param name="destroyComponent">Destroy this component or only detach it.</param>
        private void InternalRemoveComponent(Component component, bool destroyComponent)
        {
            if (this.componentList.Remove(component))
            {
                this.ProcessRemoveComponentLifecycle(component, destroyComponent);
            }
        }

        private void ProcessRemoveComponentLifecycle(Component component, bool destroyComponent)
        {
            try
            {
                component.ForceState(destroyComponent ? AttachableObjectState.Destroyed : AttachableObjectState.Detached);
            }
            catch (Exception ex) when (!this.CaptureComponentException(component, ex))
            {
            }

            this.ComponentDetached?.Invoke(this, component);
        }

        /// <summary>
        /// Removes all components of specified type from this instance.
        /// </summary>
        /// <typeparam name="T">The exact type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">if set to <c>true</c> [is exact type].</param>
        /// <returns>
        /// This instance.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">The entity is disposed.</exception>
        /// <exception cref="System.NullReferenceException">componentType
        /// or
        /// componentType.</exception>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity RemoveAllComponentsOfType<T>(bool isExactType = true)
            where T : Component
        {
            return this.RemoveAllComponentsOfType(typeof(T), isExactType);
        }

        /// <summary>
        /// Removes all components of specified type from this instance.
        /// </summary>
        /// <param name="componentType">Type of the component.</param>
        /// <param name="isExactType">if set to <c>true</c> [is exact type].</param>
        /// <returns>
        /// This instance.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">The entity is disposed.</exception>
        /// <exception cref="System.NullReferenceException">componentType
        /// or
        /// componentType.</exception>
        /// <remarks>
        /// The method returns this instance. It can be used
        /// with method chaining, so performing consecutive operations
        /// over the same instance is simpler.
        /// </remarks>
        public Entity RemoveAllComponentsOfType(Type componentType, bool isExactType = true)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (componentType == null)
            {
                throw new NullReferenceException("componentType");
            }

            if (!ReflectionHelper.IsAssignableFrom(typeof(Component), componentType))
            {
                throw new ArgumentException("componentType");
            }

            Predicate<Component> checkComponentTypePredicate;
            Func<Type, bool> checkTypePredicate;

            if (isExactType)
            {
                checkComponentTypePredicate = (c) =>
                {
                    if (c.GetType() == componentType)
                    {
                        this.ProcessRemoveComponentLifecycle(c, true);
                        return true;
                    }

                    return false;
                };

                checkTypePredicate = (t) =>
                {
                    return t == componentType;
                };
            }
            else
            {
                checkComponentTypePredicate = (c) =>
                {
                    if (ReflectionHelper.IsAssignableFrom(componentType, c.GetType()))
                    {
                        this.ProcessRemoveComponentLifecycle(c, true);
                        return true;
                    }

                    return false;
                };

                checkTypePredicate = (t) =>
                {
                    return ReflectionHelper.IsAssignableFrom(componentType, t.GetType());
                };
            }

            this.componentList.RemoveAll(checkComponentTypePredicate);

            List<Component> componentsToRemove = new List<Component>();
            for (int i = this.componentList.Count - 1; i >= 0; i--)
            {
                Component component = this.componentList[i];
                if (checkComponentTypePredicate(component))
                {
                    this.componentList.RemoveAt(i);

                    this.ProcessRemoveComponentLifecycle(component, true);
                }
            }

            return this;
        }

        // TODO: Entity Clone
        /////// <summary>
        /////// Creates a new object that is a copy of the current instance.
        /////// </summary>
        /////// <param name="entityName">Name of the entity.</param>
        /////// <returns>
        /////// A new object that is a copy of this instance.
        /////// </returns>
        /////// <exception cref="System.ObjectDisposedException">Instance is already disposed.</exception>
        /////// <remarks>
        /////// Performs a deep copy of the instance.
        /////// </remarks>
        ////public Entity Clone(string entityName)
        ////{
        ////    if (this.IsDestroyed)
        ////    {
        ////        throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
        ////    }

        ////    return this.InternalClone(entityName, this.parent);
        ////}

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance (its name).
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance (its name).
        /// </returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(this.Tag))
            {
                return this.Name;
            }
            else
            {
                return $"{this.Name} [{this.Tag}]";
            }
        }

        /// <summary>
        /// Fire entity initialized event.
        /// </summary>
        internal void FireChildOrderChangedEvent(Entity child)
        {
            this.ChildOrderChanged?.Invoke(this, child);
        }

        /// <summary>
        /// Finds a <see cref="Component"/> in this instance with the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public T FindComponent<T>(bool isExactType = true, string tag = null)
            where T : Component
        {
            return this.FindComponent(typeof(T), isExactType, tag) as T;
        }

        /// <summary>
        /// Finds a <see cref="Component"/> in this instance with the specified type.
        /// </summary>
        /// <param name="type">The type of the <see cref="Component"/> to find.</param>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public Component FindComponent(Type type, bool isExactType = true, string tag = null)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            // Filter by entity tag
            if (!string.IsNullOrEmpty(tag) && this.Tag != tag)
            {
                return null;
            }

            if (isExactType)
            {
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (component.GetType() == type)
                    {
                        return component;
                    }
                }
            }
            else
            {
                for (int i = 0; i < this.componentList.Count; i++)
                {
                    var component = this.componentList[i];
                    if (ReflectionHelper.IsAssignableFrom(type, component.GetType()))
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a <see cref="Component"/> in this instance or any of its children with the specified type using depth first search.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public T FindComponentInChildren<T>(bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
            where T : Component
        {
            return this.FindComponentsInChildren<T>(isExactType, tag, skipOwner, isRecursive)?.FirstOrDefault();
        }

        /// <summary>
        /// Finds a <see cref="Component"/> in this instance or any of its children with the specified type using depth first search.
        /// </summary>
        /// <param name="type">The type of the <see cref="Component"/> to find.</param>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public Component FindComponentInChildren(Type type, bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
        {
            return this.FindComponentsInChildren(type, isExactType, tag, skipOwner, isRecursive)?.FirstOrDefault();
        }

        /// <summary>
        /// Finds a <see cref="Component"/> in any of the parents of this instance with the specified type using depth first search.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public T FindComponentInParents<T>(bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
            where T : Component
        {
            return this.FindComponentsInParents<T>(isExactType, tag, skipOwner, isRecursive)?.FirstOrDefault();
        }

        /// <summary>
        /// Finds a <see cref="Component"/> in any of the parents of this instance with the specified type using depth first search.
        /// </summary>
        /// <param name="type">The type of the <see cref="Component"/> to find.</param>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public Component FindComponentInParents(Type type, bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
        {
            return this.FindComponentsInParents(type, isExactType, tag, skipOwner, isRecursive)?.FirstOrDefault();
        }

        /// <summary>
        /// Finds a <see cref="Component"/> collection in this instance with the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public IEnumerable<T> FindComponents<T>(bool isExactType = true, string tag = null)
            where T : Component
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            // Filter by entity tag
            if (!string.IsNullOrEmpty(tag) && this.Tag != tag)
            {
                return Enumerable.Empty<T>();
            }

            if (isExactType)
            {
                Type type = typeof(T);
                return this.componentList
                           .Where(c => type == c.GetType())
                           .Cast<T>();
            }
            else
            {
                return this.componentList.OfType<T>();
            }
        }

        /// <summary>
        /// Finds a <see cref="Component"/> collection in this instance with the specified type.
        /// </summary>
        /// <param name="type">The type of the <see cref="Component"/> to find.</param>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public IEnumerable<Component> FindComponents(Type type, bool isExactType = true, string tag = null)
        {
            if (this.IsDestroyed)
            {
                throw new InvalidOperationException($"The entity \"{this.name}\" is destroyed");
            }

            if (!string.IsNullOrEmpty(tag) && this.Tag != tag)
            {
                return Enumerable.Empty<Component>();
            }

            if (isExactType)
            {
                return this.componentList
                           .Where(c => c.GetType() == type);
            }
            else
            {
                return this.componentList
                           .Where(c => ReflectionHelper.IsAssignableFrom(type, c.GetType()));
            }
        }

        /// <summary>
        /// Finds a <see cref="Component"/> collection in this instance or any of its children with the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public IEnumerable<T> FindComponentsInChildren<T>(bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
            where T : Component
        {
            IEnumerable<T> componentsInEntity = Enumerable.Empty<T>();

            if (!skipOwner)
            {
                componentsInEntity = this.FindComponents<T>(isExactType, tag);
            }

            if (isRecursive)
            {
                return componentsInEntity.Union(this.childList.SelectMany(c => c.FindComponentsInChildren<T>(isExactType, tag)));
            }
            else
            {
                return componentsInEntity.Union(this.childList.SelectMany(c => c.FindComponents<T>(isExactType, tag)));
            }
        }

        /// <summary>
        /// Finds a <see cref="Component"/> collection in this instance or any of its children with the specified type.
        /// </summary>
        /// <param name="type">The type of the <see cref="Component"/> to find.</param>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all descendants of the hierarchy; otherwise, the search will only include the direct descendants.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public IEnumerable<Component> FindComponentsInChildren(Type type, bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
        {
            IEnumerable<Component> componentsInEntity = Enumerable.Empty<Component>();

            if (!skipOwner)
            {
                componentsInEntity = this.FindComponents(type, isExactType, tag);
            }

            if (isRecursive)
            {
                return componentsInEntity.Union(this.childList.SelectMany(c => c.FindComponentsInChildren(type, isExactType, tag)));
            }
            else
            {
                return componentsInEntity.Union(this.childList.SelectMany(c => c.FindComponents(type, isExactType, tag)));
            }
        }

        /// <summary>
        /// Finds a <see cref="Component"/> collection in this instance or any of its parents with the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Component"/> to find.</typeparam>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all ascendants of the hierarchy; otherwise, the search will only include the direct ascendant.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public IEnumerable<T> FindComponentsInParents<T>(bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
            where T : Component
        {
            IEnumerable<T> componentsInEntity = Enumerable.Empty<T>();

            if (!skipOwner)
            {
                componentsInEntity = this.FindComponents<T>(isExactType, tag);
            }

            if (this.parent != null)
            {
                IEnumerable<T> componentsInParent = this.parent.FindComponents<T>(isExactType, tag);
                if (isRecursive)
                {
                    return componentsInEntity.Union(componentsInParent.Union(this.parent.FindComponentsInParents<T>(isExactType, tag)));
                }
                else
                {
                    return componentsInEntity.Union(componentsInParent.Union(this.parent.FindComponents<T>(isExactType, tag)));
                }
            }
            else
            {
                return componentsInEntity;
            }
        }

        /// <summary>
        /// Finds a <see cref="Component"/> collection in this instance or any of its parents with the specified type.
        /// </summary>
        /// <param name="type">The type of the <see cref="Component"/> to find.</param>
        /// <param name="isExactType">Whether to match the exact type.</param>
        /// <param name="tag">Filter entities by this tag.</param>
        /// <param name="skipOwner">Indicates whether the owner is included in the search.</param>
        /// <param name="isRecursive">If set to <c>true</c> the search will include all ascendants of the hierarchy; otherwise, the search will only include the direct ascendant.</param>
        /// <returns>
        /// The <see cref="Component"/> or null if no <see cref="Component"/> with the specified type was found.
        /// </returns>
        /// <exception cref="System.ObjectDisposedException">Entity has been disposed.</exception>
        public IEnumerable<Component> FindComponentsInParents(Type type, bool isExactType = true, string tag = null, bool skipOwner = false, bool isRecursive = true)
        {
            IEnumerable<Component> componentsInEntity = Enumerable.Empty<Component>();

            if (!skipOwner)
            {
                componentsInEntity = this.FindComponents(type, isExactType, tag);
            }

            if (this.parent != null)
            {
                IEnumerable<Component> componentsInParent = this.parent.FindComponents(type, isExactType, tag);

                if (isRecursive)
                {
                    return componentsInEntity.Union(componentsInParent.Union(this.parent.FindComponentsInParents(type, isExactType, tag)));
                }
                else
                {
                    return componentsInEntity.Union(componentsInParent.Union(this.parent.FindComponents(type, isExactType, tag)));
                }
            }
            else
            {
                return componentsInEntity;
            }
        }

        /// <summary>
        /// Check an entity name.
        /// </summary>
        /// <param name="name">The entity name.</param>
        /// <returns>If it's valid or not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidName(string name)
        {
            return !InvalidEntityNameStrings.Any(x => name.Contains(x));
        }

        internal override void RefreshIsEnabled()
        {
            this.RefreshIsHierarchyEnabledRecursive();
            base.RefreshIsEnabled();

            if (this.IsActivated && this.entityManager != null && this.entityManager.IsStarted)
            {
                this.BaseStart();
            }
        }

        internal void RefreshEntityManager(EntityManager entityManager)
        {
            this.entityManager = entityManager;

            foreach (var child in this.childList)
            {
                child.RefreshEntityManager(this.entityManager);
            }
        }

        private void RefreshIsHierarchyEnabled()
        {
            this.isHierarchyEnabled = this.IsEnabled && ((this.parent != null) ? this.parent.isHierarchyEnabled : true);
        }

        private void RefreshIsHierarchyEnabledRecursive()
        {
            var inheritedIsEnabled = (this.parent != null) ? this.parent.isHierarchyEnabled : true;
            this.RefreshIsHierarchyEnabledRecursive(inheritedIsEnabled);
        }

        private void RefreshIsHierarchyEnabledRecursive(bool inheritedIsEnabled = true)
        {
            inheritedIsEnabled &= this.IsEnabled;
            this.isHierarchyEnabled = inheritedIsEnabled;

            for (int i = 0; i < this.childEntities.Count; i++)
            {
                this.childList[i].RefreshIsHierarchyEnabledRecursive(inheritedIsEnabled);
            }
        }

        /// <summary>
        /// Internal remove child without checks.
        /// </summary>
        /// <param name="entity">The child entity to remove.</param>
        private void InternalRemoveChild(Entity entity)
        {
            if (this.InternalDetachChild(entity))
            {
                entity.Destroy();
            }
        }

        /// <summary>
        /// The internal method without checks to detach an entity.
        /// </summary>
        /// <param name="entity">The entity to detach.</param>
        /// <returns>True if the child was removed or false otherwise.</returns>
        private bool InternalDetachChild(Entity entity)
        {
            entity.parent = null;
            entity.CheckValidName -= this.OnChildValidName;
            entity.NameChanged -= this.OnChildNameChanged;
            if (this.childEntities.Remove(entity.Id))
            {
                this.childList.Remove(entity);
                entity.ForceState(AttachableObjectState.Detached);

                this.ChildDetached?.Invoke(this, entity);

                this.entityManager?.NotifyEntityDetached(entity);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Raises when the entity name is changed.
        /// </summary>
        /// <param name="sender">Entity as object.</param>
        /// <param name="e">For more information about this parameter <see cref="NameEventArgs"/> class.</param>
        private void OnChildNameChanged(object sender, NameEventArgs e)
        {
        }

        /// <summary>
        /// To Check if a Entity name is a valid name.
        /// </summary>
        /// <param name="sender">Entity as object.</param>
        /// <param name="args">For more information about this parameter <see cref="ValidNameEventArgs"/> class.</param>
        private void OnChildValidName(object sender, ValidNameEventArgs args)
        {
        }

        /// <summary>
        /// Gets the first child entity with the specified name.
        /// </summary>
        /// <param name="entityName">The name of the entity to get.</param>
        /// <param name="childEntity">When this method returns, contains the value associated with the specified key,
        /// if the key is found; otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if this <see cref="Entity"/> contains the first child with the specified name; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">If the entity name is null.</exception>
        public bool TryGetChildByName(string entityName, out Entity childEntity)
        {
            if (string.IsNullOrEmpty(entityName))
            {
                throw new ArgumentNullException("entityName");
            }

            childEntity = this.childList.Where(e => e.name == entityName).FirstOrDefault();

            return childEntity != null;
        }

        /// <summary>
        /// Gets the child entity with the specified id.
        /// </summary>
        /// <param name="entityId">The id of the entity to get.</param>
        /// <param name="childEntity">When this method returns, contains the value associated with the specified key,
        /// if the key is found; otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>true if this <see cref="Entity"/> contains a child with the specified id; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">If the entity name is null.</exception>
        public bool TryGetChildById(Guid entityId, out Entity childEntity)
        {
            return this.childEntities.TryGetValue(entityId, out childEntity);
        }

        /// <inheritdoc/>
        protected override void IdHasChanged(Guid oldId)
        {
            base.IdHasChanged(oldId);

            if (this.parent != null)
            {
                if (this.parent.childEntities.Remove(oldId))
                {
                    this.parent.childEntities.Add(this.Id, this);
                }
            }
        }

        /// <summary>
        /// Capture exception.
        /// </summary>
        /// <param name="component">The component that threw the exception.</param>
        /// <param name="ex">The exception to capture.</param>
        /// <returns>True if we want to re-throw the exception.</returns>
        internal bool CaptureComponentException(Component component, Exception ex)
        {
            var errorHandler = Application.Current.Container.Resolve<ErrorHandler>();
            return errorHandler?.CaptureException(new ComponentException(this, component, ex)) ?? true;
        }
    }
}
