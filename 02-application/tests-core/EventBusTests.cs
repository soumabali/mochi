using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MochiV2.Core.Events;
using Xunit;

namespace MochiV2.Tests.Core
{
    /// <summary>
    /// T-007: EventBus tests — subscribe, publish, unsubscribe, multiple
    /// handlers, thread safety under parallel publish.
    /// </summary>
    public class EventBusTests
    {
        // ------------------------------------------------------------------
        // Basic subscribe / publish
        // ------------------------------------------------------------------

        [Fact]
        public void Subscribe_And_Publish_Receives_Event()
        {
            var bus = new EventBus();
            TestEvent? received = null;
            bus.Subscribe<TestEvent>(e => received = e);

            var evt = new TestEvent(42, "hello");
            bus.Publish(evt);

            Assert.NotNull(received);
            Assert.Equal(42, received!.Value);
            Assert.Equal("hello", received.Message);
        }

        [Fact]
        public void Publish_With_No_Subscribers_Is_NoOp()
        {
            var bus = new EventBus();
            // Should not throw.
            bus.Publish(new TestEvent(1, "x"));
        }

        // ------------------------------------------------------------------
        // Unsubscribe
        // ------------------------------------------------------------------

        [Fact]
        public void Unsubscribe_Stops_Receiving()
        {
            var bus = new EventBus();
            var calls = new List<int>();
            Action<TestEvent> handler = e => calls.Add(e.Value);

            bus.Subscribe(handler);
            bus.Publish(new TestEvent(1, "a"));
            Assert.Single(calls);

            Assert.True(bus.Unsubscribe(handler));
            bus.Publish(new TestEvent(2, "b"));
            Assert.Single(calls); // no new call
        }

        [Fact]
        public void Unsubscribe_NotRegistered_Returns_False()
        {
            var bus = new EventBus();
            Action<TestEvent> handler = e => { };
            Assert.False(bus.Unsubscribe(handler));
        }

        // ------------------------------------------------------------------
        // Multiple handlers
        // ------------------------------------------------------------------

        [Fact]
        public void Multiple_Handlers_All_Receive()
        {
            var bus = new EventBus();
            var calls = new List<int>();
            bus.Subscribe<TestEvent>(e => calls.Add(e.Value * 1));
            bus.Subscribe<TestEvent>(e => calls.Add(e.Value * 10));
            bus.Subscribe<TestEvent>(e => calls.Add(e.Value * 100));

            bus.Publish(new TestEvent(2, "x"));

            Assert.Equal(new[] { 2, 20, 200 }, calls);
        }

        [Fact]
        public void Handler_Count_Tracks_Subscriptions()
        {
            var bus = new EventBus();
            Assert.Equal(0, bus.HandlerCount<TestEvent>());

            Action<TestEvent> h1 = _ => { };
            Action<TestEvent> h2 = _ => { };
            bus.Subscribe(h1);
            Assert.Equal(1, bus.HandlerCount<TestEvent>());
            bus.Subscribe(h2);
            Assert.Equal(2, bus.HandlerCount<TestEvent>());

            bus.Unsubscribe(h1);
            Assert.Equal(1, bus.HandlerCount<TestEvent>());
        }

        // ------------------------------------------------------------------
        // ClearAll (test teardown helper)
        // ------------------------------------------------------------------

        [Fact]
        public void ClearAll_Removes_All_Subscribers()
        {
            var bus = new EventBus();
            var calls = 0;
            bus.Subscribe<TestEvent>(_ => calls++);
            bus.Subscribe<TestEvent>(_ => calls++);
            bus.Subscribe<OtherEvent>(_ => { });

            bus.ClearAll();

            bus.Publish(new TestEvent(1, "x"));
            Assert.Equal(0, calls);
            Assert.Equal(0, bus.HandlerCount<TestEvent>());
            Assert.Equal(0, bus.HandlerCount<OtherEvent>());
        }

        // ------------------------------------------------------------------
        // Thread safety
        // ------------------------------------------------------------------

        [Fact]
        public async Task Parallel_Publish_All_Handlers_Receive_Every_Event()
        {
            var bus = new EventBus();
            var counter = new ConcurrentBag<int>();

            bus.Subscribe<TestEvent>(e => counter.Add(e.Value));

            const int threads = 16;
            const int perThread = 500;
            var tasks = new Task[threads];
            for (int t = 0; t < threads; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < perThread; i++)
                    {
                        bus.Publish(new TestEvent(i, "p"));
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Every publish must have been delivered exactly once.
            Assert.Equal(threads * perThread, counter.Count);
        }

        [Fact]
        public async Task Parallel_Subscribe_And_Publish_Is_Safe()
        {
            var bus = new EventBus();
            var counter = new ConcurrentBag<int>();

            // One writer subscribes a handler while many publishers are active.
            bus.Subscribe<TestEvent>(e => counter.Add(e.Value));

            var pub = Task.Run(() =>
            {
                for (int i = 0; i < 2000; i++)
                    bus.Publish(new TestEvent(1, "x"));
            });

            var sub = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    Action<TestEvent> h = _ => { };
                    bus.Subscribe(h);
                    bus.Unsubscribe(h);
                }
            });

            await Task.WhenAll(pub, sub);
            // No crash, no deadlock; at least the publisher's events were delivered.
            Assert.True(counter.Count >= 2000);
        }

        // ------------------------------------------------------------------
        // Exception isolation
        // ------------------------------------------------------------------

        [Fact]
        public void Handler_Exception_Does_Not_Prevent_Others()
        {
            var bus = new EventBus();
            var secondCalled = false;
            bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<TestEvent>(_ => secondCalled = true);

            // Publish re-throws the first exception after running all handlers.
            Assert.Throws<InvalidOperationException>(() => bus.Publish(new TestEvent(1, "x")));
            Assert.True(secondCalled, "Second handler should still run after first throws.");
        }

        // ───────────────────── helper event types ─────────────────────

        private sealed record TestEvent(int Value, string Message);
        private sealed record OtherEvent;
    }
}