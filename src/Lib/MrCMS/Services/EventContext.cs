using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MrCMS.Events;
using MrCMS.Helpers;

namespace MrCMS.Services
{
    public class EventContext : IEventContext
    {
        private readonly IServiceProvider _serviceProvider;

        public EventContext(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public HashSet<Type> DisabledEvents { get; } = new HashSet<Type>();

        public Task Publish<TEvent, TArgs>(TArgs args) where TEvent : IEvent<TArgs>
        {
            return Publish(typeof(TEvent), args);
        }

        public async Task Publish(Type eventType, object args)
        {
            //using (MiniProfiler.Current.Step("Publishing " + eventType.FullName))
            using (var scope = _serviceProvider.CreateScope()) {
                foreach (var type in TypeHelper.GetAllConcreteTypesAssignableFrom(eventType)
                    .Where(type => !IsDisabled(type)))
                {
                    //using (MiniProfiler.Current.Step("Invoking " + @event.GetType().FullName))
                    {
                        MethodInfo methodInfo = type.GetMethod("Execute", new[] {args.GetType()});
                        var instance = scope.ServiceProvider.GetRequiredService(type);
                        await (Task) methodInfo.Invoke(instance, new[] {args});
                    }
                }
            }
        }

        public IDisposable Disable<T>()
        {
            return new EventPublishingDisabler(this, typeof(T));
        }

        public IDisposable Disable(params Type[] types)
        {
            return new EventPublishingDisabler(this, types);
        }

        private bool IsDisabled(Type type)
        {
            return DisabledEvents.Any(type.IsImplementationOf);
        }

        public class EventPublishingDisabler : IDisposable
        {
            private readonly EventContext _eventContext;
            private readonly HashSet<Type> _toEnableOnDispose = new HashSet<Type>();

            public EventPublishingDisabler(EventContext eventContext, params Type[] types)
            {
                _eventContext = eventContext;
                foreach (Type type in types.Where(type => _eventContext.DisabledEvents.Add(type)))
                {
                    _toEnableOnDispose.Add(type);
                }
            }

            public void Dispose()
            {
                foreach (Type type in _toEnableOnDispose)
                {
                    _eventContext.DisabledEvents.Remove(type);
                }
            }
        }

        //private class InstallationEventContext : IEventContext
        //{
        //    public void Publish<TEvent, TArgs>(TArgs args) where TEvent : IEvent<TArgs>
        //    {
        //    }

        //    public void Publish(Type eventType, object args)
        //    {
        //    }

        //    public IDisposable Disable<T>()
        //    {
        //        return new DummyDisabler();
        //    }

        //    public IDisposable Disable(params Type[] types)
        //    {
        //        return new DummyDisabler();
        //    }

        //    private class DummyDisabler : IDisposable
        //    {
        //        public void Dispose()
        //        {
        //        }
        //    }
        //}
    }
}