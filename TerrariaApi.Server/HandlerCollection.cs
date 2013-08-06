﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace TerrariaApi.Server
{
  public class HandlerCollection<ArgsType>: IEnumerable<HandlerRegistration<ArgsType>> where ArgsType: EventArgs
  {
    private readonly List<HandlerRegistration<ArgsType>> registrations;
    public string hookName { get; private set; }

    internal HandlerCollection(string hookName)
    {
      if (string.IsNullOrWhiteSpace(hookName))
        throw new ArgumentException("Invalid hook name.", "hookName");

      this.registrations = new List<HandlerRegistration<ArgsType>>();
      this.hookName = hookName;
    }

    public void Register(TerrariaPlugin registrator, HookHandler<ArgsType> handler, int priority)
    {
      if (registrator == null)
        throw new ArgumentNullException("registrator");
      if (handler == null)
        throw new ArgumentNullException("handler");

      var newRegistration = new HandlerRegistration<ArgsType>
      {
        Registrator = registrator,
        Handler = handler,
        Priority = priority
      };

      // Highest priority goes up in the list, first registered wins if priority equals.
      for (int i = 0; i < this.registrations.Count + 1; i++)
      {
        if (i == this.registrations.Count)
        {
          this.registrations.Add(newRegistration);
          break;
        }

        int itemPriority = this.registrations[i].Priority;
        if (itemPriority < priority)
        {
          this.registrations.Insert(i, newRegistration);
          break;
        }
      }
    }

		public void Register(TerrariaPlugin registrator, HookHandler<ArgsType> handler)
		{
			this.Register(registrator, handler, registrator.Order);
		}

    public bool Deregister(TerrariaPlugin registrator, HookHandler<ArgsType> handler)
    {
      if (registrator == null)
        throw new ArgumentNullException("registrator");
      if (handler == null)
        throw new ArgumentNullException("handler");

      var registration = new HandlerRegistration<ArgsType>
      {
        Registrator = registrator,
        Handler = handler
      };
      int registrationIndex = this.registrations.IndexOf(registration);
      if (registrationIndex == -1)
        return false;

      this.registrations.RemoveAt(registrationIndex);
      return true;
    }

    public void Invoke(ArgsType args)
    {
      foreach (var registration in this.registrations) {
        try
        {
          if (ServerApi.Profiler == null)
          {
            registration.Handler(args);
          }
          else
          {
            Stopwatch watch = new Stopwatch();

            watch.Start();
            try
            {
              registration.Handler(args);
            }
            finally
            {
              watch.Stop();
              ServerApi.Profiler.InputPluginHandlerTime(registration.Registrator, hookName, watch.Elapsed);
            }
          }
        }
        catch (Exception ex)
        {
          ServerApi.LogWriter.ServerWriteLine(string.Format(
            "Plugin \"{0}\" has had an unhandled exception thrown by one of its {1} handlers: \n{2}",
            registration.Registrator.Name, hookName, ex), TraceLevel.Warning);

          if (ServerApi.Profiler != null)
            ServerApi.Profiler.InputPluginHandlerExceptionThrown(registration.Registrator, hookName, ex);
        }
      }
    }

    IEnumerator<HandlerRegistration<ArgsType>> IEnumerable<HandlerRegistration<ArgsType>>.GetEnumerator()
    {
      return this.registrations.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return this.registrations.GetEnumerator();
    }
  }
}