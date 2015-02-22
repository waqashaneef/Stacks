﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stacks.Actors;
using Xunit;

namespace Stacks.Tests.ActorSystemTests
{
    public class ActorSystemCreate
    {
        public ActorSystemCreate()
        {
            ActorSystem.Default.ResetSystem();
        }

        [Fact]
        public void Creating_actor_using_constructor_should_throw_exception()
        {
            Assert.Throws<Exception>(() =>
            {
                var actor = new TestActor();
            });
        }

        [Fact]
        public void Creating_actors_using_different_names_should_succeed()
        {
            var actor = ActorSystem.Default.CreateActor<TestActor, ITestActor>("Name");
            var actor2 = ActorSystem.Default.CreateActor<TestActor, ITestActor>("Name2");
        }

        [Fact]
        public void Creating_actors_using_name_twice_should_throw()
        {
            var actor = ActorSystem.Default.CreateActor<TestActor, ITestActor>("Name");

            Assert.Throws<Exception>(() =>
            {
                var actor2 = ActorSystem.Default.CreateActor<TestActor, ITestActor>("Name");
            });
        }

        [Fact]
        public void Creating_actor_that_does_not_inherit_from_Actor_class_should_throw()
        {
            Assert.Throws<Exception>(() =>
            {
                var actor = ActorSystem.Default.CreateActor<NotAnActor, ITestActor>("Name");
            });
        }

        [Fact]
        public void When_creating_actor_without_specyfing_interface_it_should_be_guessed()
        {
            var actor = ActorSystem.Default.CreateActor<TestActor>("Name");

            Assert.IsAssignableFrom(typeof(ITestActor), actor);
        }
    }

    public interface ITestActor
    {
        Task Foo();
    }

    public interface ISomeInterface
    {
        
    }

    public interface ITestActorInterface
    {
    }

    public class TestActor : Actor, ITestActor, ISomeInterface, ITestActorInterface
    {
        public async Task Foo()
        {
        }
    }

    public class NotAnActor : ITestActor
    {
        public async Task Foo()
        {
            
        }
    }
}
