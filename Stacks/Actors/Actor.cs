﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stacks.Actors
{
    public abstract class Actor : IActor
    {
        private readonly ActorContext context;

        public IActor Parent { get; private set; }
        public IEnumerable<IActor> Childs => childs.Keys;
        public ActorSystem System { get; private set; }
         
        private readonly ConcurrentDictionary<IActor, IActor> childs;
        internal IActor Wrapper;

        /// <summary>
        /// Constructor should NOT be used to initialize an actor, as it is still in process of creation and all
        /// dependencied may not be registered to ActorSystem. 
        /// <para></para>
        /// Use OnStart() method to instead.
        /// </summary>
        protected Actor()
            : this(new ActionBlockExecutor(ActionBlockExecutorSettings.Default))
        {
        }

        /// <summary>
        /// Constructor should NOT be used to initialize an actor, as it is still in process of creation and all
        /// dependencied may not be registered to ActorSystem. 
        /// <para></para>
        /// Use OnStart() method to instead.
        /// </summary>
        protected Actor(ActorSettings settings)
            : this(
                new ActionBlockExecutor(
                    ActionBlockExecutorSettings.DefaultWith(settings.SupportSynchronizationContext)))
        {
        }

        private Actor(IExecutor executor)
        {
            if (!ActorCtorGuardian.IsGuarded())
            {
                throw new Exception(
                    $"Tried to created actor of {GetType().FullName} using constructor. Please, use " + 
                    $"{nameof(ActorSystem)}.{nameof(ActorSystem.CreateActor)} method instead.");
            }

            childs = new ConcurrentDictionary<IActor, IActor>();

            context = new ActorContext(this, executor);
        }

        protected virtual void OnStart()
        {
        }
        
        protected virtual void OnStopped()
        {
        }

        internal void Start()
        {
            try
            {
                OnStart();
            }
            catch (Exception exn)
            {
                throw new Exception($"Error occured when actor was starting. Actor: '{Name}' - {GetType().FullName}. See inner exception for details.", exn);
            }
        }

        protected void Stop(bool stopImmediately = false)
        {
            var stopTask = context.Stop(stopImmediately);

            stopTask.ContinueWith(t =>
            {
                System.KillActor(this);

                try
                {
                    OnStopped();
                }
                catch (Exception)
                {
                    // ignore
                }
            });
        } 


        internal void SetName(string newName)
        {
            Name = newName;
            context.SetName(newName);
        }

        internal void SetParent(IActor parentActor)
        {
            Parent = parentActor;
        }

        internal void AddChild(IActor childActor)
        {
            Ensure.IsNotNull(childActor, nameof(childActor));

            if (!childs.TryAdd(childActor, childActor))
            {
                throw new InvalidOperationException($"Tried to add child to actor '{Name}' - {GetType().FullName}. " + 
                    $"Child to be added '{childActor.Name}' - {childActor.GetType().FullName}. Actor already has this child registered.");   
            }
        }

        internal void SetWrapper(IActor actorWrapper)
        {
            Wrapper = actorWrapper;
        }

        internal void RemoveChild(IActor childActor)
        {
            Ensure.IsNotNull(childActor, nameof(childActor));
            IActor a;
            childs.TryRemove(childActor, out a);
        }

        internal void SetActorSystem(ActorSystem system)
        {
            System = system;
        }

        protected IActorContext Context => context;
        protected SynchronizationContext GetActorSynchronizationContext() => context.SynchronizationContext;
        protected Task Completion => context.Completion;
       
        public bool Named => Name != null;
        public string Name { get; private set; }

    }
}