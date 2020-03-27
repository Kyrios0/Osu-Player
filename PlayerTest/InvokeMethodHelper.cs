﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlayerTest
{
    public static class InvokeMethodHelper
    {
        private static SynchronizationContext _uiContext;

        public static void SetMainThreadContext()
        {
            if (_uiContext != null) throw new NotSupportedException();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                var fileName = Path.GetFileName(assembly.Location);
                if (fileName == "System.Windows.Forms.dll")
                {
                    var type = assembly.DefinedTypes.First(k => k.Name.StartsWith("WindowsFormsSynchronizationContext"));
                    _uiContext = (SynchronizationContext)Activator.CreateInstance(type);
                    break;
                }
                else if (fileName == "WindowsBase.dll")
                {
                    var type = assembly.DefinedTypes.First(k => k.Name.StartsWith("DispatcherSynchronizationContext"));
                    _uiContext = (SynchronizationContext)Activator.CreateInstance(type);
                    break;
                }
            }

            if (_uiContext == null) _uiContext = SynchronizationContext.Current;
        }

        public static void OnMainThread(Action action, bool raiseEventInUiThread = true)
        {
            if (_uiContext == null) throw new ArgumentNullException();

            if (raiseEventInUiThread)
                _uiContext.Send(obj => { action?.Invoke(); }, null);
            else
                action?.Invoke();
        }
    }
}
