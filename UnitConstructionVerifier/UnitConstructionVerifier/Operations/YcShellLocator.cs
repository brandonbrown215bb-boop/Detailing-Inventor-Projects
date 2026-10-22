using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Finds the running YCShell add-in server object. Inventor's ApplicationAddIn has no AddInServer property, and
    /// YCShell's Automation returns null, so the object is found through the event handlers YCShell attached to
    /// Inventor objects. In one AppDomain, Inventor objects UCV fetches are the same COM wrappers YCShell holds. A
    /// handler YCShell added (button OnExecute, application events) is a delegate whose Target is its server.
    /// </summary>
    internal static class YcShellLocator
    {
        internal const string ServerTypeName = "InventorAddIn_MOM_YCShell.StandardAddInServer";

        private static readonly string[] YcShellButtonIds =
        {
            "ID_COMMAND_ISG_OPERATIONS_FOAM_AND_SNAPLOCK_HOLES",
            "ID_COMMAND_MOM_UPLOAD",
        };

        /// <summary>The YCShell server, or null. <paramref name="trace"/> says what was looked at.</summary>
        internal static object? Find(Inventor.Application app, List<string> trace)
        {
            AppDomain domain = AppDomain.CurrentDomain;
            trace.Add($"AppDomain: {domain.FriendlyName} (id {domain.Id})");

            Assembly? ycShell = domain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "InventorAddIn_MOM_YCShell");
            trace.Add(ycShell is null
                ? "YCShell assembly is NOT loaded in this AppDomain, so reflection cannot reach it."
                : $"YCShell assembly loaded here: {ycShell.GetName().Version} {SafeLocation(ycShell)}");

            List<Type> providerTypes = EventProviderTypes(domain, trace);

            var sources = new List<(string Name, object Rcw)>();
            foreach (string id in YcShellButtonIds)
            {
                try { sources.Add(($"button {id}", app.CommandManager.ControlDefinitions[id])); }
                catch (Exception ex) { trace.Add($"button {id}: not found ({ex.Message})"); }
            }

            try { sources.Add(("ApplicationEvents", app.ApplicationEvents)); }
            catch (Exception ex) { trace.Add($"ApplicationEvents: {ex.Message}"); }

            foreach ((string name, object rcw) in sources)
            {
                object? server = FindInEventSinks(rcw, providerTypes, out string detail);
                trace.Add($"{name}: {detail}");
                if (server is not null) return server;
            }

            return null;
        }

        private static List<Type> EventProviderTypes(AppDomain domain, List<string> trace)
        {
            var types = new List<Type>();
            foreach (Assembly interop in domain.GetAssemblies().Where(a => a.GetName().Name == "Autodesk.Inventor.Interop"))
            {
                trace.Add($"Inventor interop loaded: {interop.GetName().Version} {SafeLocation(interop)}");
                try
                {
                    types.AddRange(interop.GetTypes().Where(t => t.Name.EndsWith("_EventProvider", StringComparison.Ordinal)));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types.AddRange(ex.Types.Where(t => t is not null && t.Name.EndsWith("_EventProvider", StringComparison.Ordinal))!);
                }
            }

            return types;
        }

        private static object? FindInEventSinks(object rcw, List<Type> providerTypes, out string detail)
        {
            if (!Marshal.IsComObject(rcw))
            {
                detail = "not a COM object";
                return null;
            }

            var handlerOwners = new List<string>();
            foreach (Type providerType in providerTypes)
            {
                object? provider;
                try { provider = Marshal.GetComObjectData(rcw, providerType); }
                catch { continue; }
                if (provider is null) continue;

                foreach (Delegate handler in Handlers(provider))
                {
                    object? target = handler.Target;
                    string owner = target?.GetType().FullName ?? "(static)";
                    handlerOwners.Add($"{owner}.{handler.Method.Name}");
                    if (target is not null && target.GetType().FullName == ServerTypeName)
                    {
                        detail = $"found YCShell server via {providerType.Name} handler {handler.Method.Name}";
                        return target;
                    }
                }
            }

            detail = handlerOwners.Count == 0
                ? "no event handlers visible on this object"
                : "handlers seen: " + string.Join(", ", handlerOwners.Distinct().Take(12));
            return null;
        }

        /// <summary>Delegates held by an interop event provider's sink helpers (m_aEventSinkHelpers in tlbimp output).</summary>
        private static IEnumerable<Delegate> Handlers(object provider)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in provider.GetType().GetFields(all))
            {
                if (field.GetValue(provider) is not IEnumerable helpers || helpers is string) continue;
                foreach (object? helper in helpers)
                {
                    if (helper is null) continue;
                    foreach (FieldInfo helperField in helper.GetType().GetFields(all))
                    {
                        if (helperField.GetValue(helper) is not Delegate d) continue;
                        foreach (Delegate single in d.GetInvocationList()) yield return single;
                    }
                }
            }
        }

        private static string SafeLocation(Assembly asm)
        {
            try { return asm.Location; }
            catch { return string.Empty; }
        }
    }
}
