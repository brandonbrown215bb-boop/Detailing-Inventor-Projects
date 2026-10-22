using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// The only place that knows the JCI MOM YCShell add-in's internals. YCShell is not ours to change, so UCV reaches
    /// its private members by reflection on the running add-in server (both add-ins live in Inventor's process).
    /// Every member is resolved up front: if YCShell changes shape, <see cref="FromServer"/> says which member is
    /// missing instead of failing halfway through a unit. See Docs/batch_regenerate_upload_plan.md section 3.
    /// </summary>
    public sealed class YcShellBridge
    {
        public const string AddInId = "{8056411E-F31D-4EC5-A172-4A874BBEA3AA}";

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string Ns = "InventorAddIn_MOM_YCShell";

        private readonly object _server;
        private readonly FieldInfo _openAssemblies;
        private readonly FieldInfo _activeAssembly;
        private readonly MethodInfo _runOperation;
        private readonly Delegate _regenerate;

        private YcShellBridge(object server, FieldInfo openAssemblies, FieldInfo activeAssembly,
            MethodInfo runOperation, Delegate regenerate, IReadOnlyList<MemberCheck> checks)
        {
            _server = server;
            _openAssemblies = openAssemblies;
            _activeAssembly = activeAssembly;
            _runOperation = runOperation;
            _regenerate = regenerate;
            Checks = checks;
        }

        /// <summary>One line per YCShell member UCV relies on, for the spike report.</summary>
        public IReadOnlyList<MemberCheck> Checks { get; }

        public string Version => _server.GetType().Assembly.GetName().Version?.ToString() ?? "unknown";

        public string AssemblyLocation
        {
            get
            {
                try { return _server.GetType().Assembly.Location; }
                catch { return "unknown"; }
            }
        }

        /// <summary>Finds the running YCShell add-in in Inventor and checks it. <paramref name="trace"/> records how.</summary>
        public static YcShellBridge? Connect(Inventor.Application app, List<string> trace, out string problem, out IReadOnlyList<MemberCheck> checks)
        {
            checks = Array.Empty<MemberCheck>();
            Inventor.ApplicationAddIn? addIn = null;
            foreach (Inventor.ApplicationAddIn candidate in app.ApplicationAddIns)
            {
                if (string.Equals(candidate.ClientId, AddInId, StringComparison.OrdinalIgnoreCase))
                {
                    addIn = candidate;
                    break;
                }
            }

            if (addIn is null)
            {
                problem = $"YCShell add-in {AddInId} is not installed.";
                return null;
            }

            if (!addIn.Activated)
            {
                problem = "YCShell add-in is installed but not loaded.";
                return null;
            }

            trace.Add($"YCShell add-in: {addIn.DisplayName}, loaded, {addIn.Location}");
            object? server = YcShellLocator.Find(app, trace);
            if (server is null)
            {
                problem = "YCShell is loaded, but UCV could not reach its add-in server object (see trace).";
                return null;
            }

            return FromServer(server, out problem, out checks);
        }

        /// <summary>Resolves every member UCV needs on a YCShell add-in server object.</summary>
        public static YcShellBridge? FromServer(object server, out string problem, out IReadOnlyList<MemberCheck> checks)
        {
            var list = new List<MemberCheck>();
            checks = list;
            Type type = server.GetType();
            Assembly asm = type.Assembly;

            if (type.FullName == "System.__ComObject")
            {
                problem = "YCShell add-in server is a COM proxy, not the .NET object, so its members cannot be reached.";
                list.Add(new MemberCheck("add-in server type", false, problem));
                return null;
            }

            list.Add(new MemberCheck("add-in server type", true, $"{type.FullName} ({asm.GetName().Name} {asm.GetName().Version})"));

            Type? setStatusType = CheckType(list, asm, $"{Ns}.CUtility+DelegateSetStatus");
            Type? operationType = CheckType(list, asm, $"{Ns}.CommonUI.CFrmRunOperation+DelegateOperation");
            Type? uploadFormType = CheckType(list, asm, $"{Ns}.Upload.CFrmUploadChanges");
            CheckType(list, asm, $"{Ns}.Upload.CFrmUploadChanges+DelegateSetSuppressDocumentEvents");

            FieldInfo? openAssemblies = CheckField(list, type, "m_OpenAssemblyList", typeof(IEnumerable));
            CheckField(list, type, "m_OpenPartList", typeof(IEnumerable));
            CheckField(list, type, "m_InventorInstance", null);
            FieldInfo? activeAssembly = CheckField(list, type, "m_ActiveAssembly", null);
            CheckField(list, type, "m_InUpload", typeof(bool));

            MethodInfo? regenerate = setStatusType is null ? null
                : CheckMethod(list, type, "RegenerateFoamAndSnaplockHoles", typeof(object), setStatusType);
            MethodInfo? runOperation = operationType is null ? null
                : CheckMethod(list, type, "RunOperation", typeof(object), operationType, typeof(bool));
            CheckMethod(list, type, "SetSuppressDocumentEvents", typeof(bool));

            if (uploadFormType is not null)
            {
                MethodInfo? start = uploadFormType.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance);
                list.Add(new MemberCheck("CFrmUploadChanges.Start", start?.GetParameters().Length == 4,
                    start is null ? "not found" : Describe(start)));
            }

            Delegate? regenerateDelegate = null;
            if (regenerate is not null && operationType is not null)
            {
                try
                {
                    regenerateDelegate = Delegate.CreateDelegate(operationType, server, regenerate);
                }
                catch (Exception ex)
                {
                    list.Add(new MemberCheck("Regenerate as DelegateOperation", false, ex.Message));
                }
            }

            MemberCheck? firstMiss = list.FirstOrDefault(c => !c.Found);
            if (firstMiss is not null || openAssemblies is null || activeAssembly is null || runOperation is null || regenerateDelegate is null)
            {
                problem = $"YCShell version not supported: {firstMiss?.Name ?? "member missing"}";
                return null;
            }

            problem = string.Empty;
            return new YcShellBridge(server, openAssemblies, activeAssembly, runOperation, regenerateDelegate, list);
        }

        /// <summary>The surfaces and composites YCShell has loaded, in its own order.</summary>
        public IReadOnlyList<YcSurface> GetOpenSurfaces()
        {
            var result = new List<YcSurface>();
            if (_openAssemblies.GetValue(_server) is not IEnumerable items) return result;

            foreach (object? item in items)
            {
                if (item is not null) result.Add(YcSurface.From(item));
            }

            return result;
        }

        /// <summary>The surface YCShell's own buttons would act on (follows the active / in-place-edited document).</summary>
        public string? ActiveAssemblyFileName =>
            _activeAssembly.GetValue(_server) is object active ? YcSurface.From(active).FullFileName : null;

        /// <summary>
        /// Runs Regenerate Channel Features on one surface through YCShell's own progress window. Blocks until YCShell
        /// finishes, including any message boxes it shows. YCShell's suppressed-occurrence check looks at its active
        /// assembly rather than <paramref name="surface"/>, so it is skipped here.
        /// </summary>
        public void Regenerate(YcSurface surface)
        {
            try
            {
                _runOperation.Invoke(_server, new object[] { surface.Handle, _regenerate, false });
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        private static Type? CheckType(List<MemberCheck> list, Assembly asm, string fullName)
        {
            Type? found = asm.GetType(fullName, throwOnError: false);
            list.Add(new MemberCheck(fullName.Substring(Ns.Length + 1), found is not null, found is null ? "not found" : "type"));
            return found;
        }

        private static FieldInfo? CheckField(List<MemberCheck> list, Type type, string name, Type? assignableTo)
        {
            FieldInfo? field = type.GetField(name, Private);
            bool ok = field is not null && (assignableTo is null || assignableTo.IsAssignableFrom(field.FieldType));
            list.Add(new MemberCheck(name, ok, field is null ? "not found" : field.FieldType.Name));
            return ok ? field : null;
        }

        private static MethodInfo? CheckMethod(List<MemberCheck> list, Type type, string name, params Type[] parameters)
        {
            MethodInfo? method = type.GetMethod(name, Private, null, parameters, null);
            string detail = method is not null ? Describe(method)
                : type.GetMethods(Private).FirstOrDefault(m => m.Name == name) is MethodInfo other
                    ? $"different parameters: {Describe(other)}"
                    : "not found";
            list.Add(new MemberCheck(name, method is not null, detail));
            return method;
        }

        private static string Describe(MethodInfo method) =>
            $"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";
    }

    public sealed class MemberCheck
    {
        public MemberCheck(string name, bool found, string detail)
        {
            Name = name;
            Found = found;
            Detail = detail;
        }

        public string Name { get; }
        public bool Found { get; }
        public string Detail { get; }

        public override string ToString() => $"{(Found ? "ok  " : "MISS")}  {Name}  [{Detail}]";
    }

    /// <summary>One entry of YCShell's open assembly list, with the opaque CAssembly handle YCShell expects back.</summary>
    public sealed class YcSurface
    {
        private YcSurface(object handle, string fullFileName, bool isLoaded, bool? isComposite)
        {
            Handle = handle;
            FullFileName = fullFileName;
            IsLoaded = isLoaded;
            IsComposite = isComposite;
        }

        public object Handle { get; }
        public string FullFileName { get; }

        /// <summary>YCShell has built its MOM wrapper for the document (LoadAssemblyDocument ran).</summary>
        public bool IsLoaded { get; }

        /// <summary>null when YCShell has not loaded the document or the flag could not be read.</summary>
        public bool? IsComposite { get; }

        internal static YcSurface From(object handle)
        {
            Type type = handle.GetType();
            string fullFileName = type.GetProperty("FullFileName")?.GetValue(handle) as string ?? string.Empty;
            object? document = type.GetProperty("AssemblyDocument")?.GetValue(handle);

            bool? isComposite = null;
            if (document is not null)
            {
                try { isComposite = document.GetType().GetProperty("IsCompositeAssembly")?.GetValue(document) as bool?; }
                catch { /* reported as unknown */ }
            }

            return new YcSurface(handle, fullFileName.Trim(), document is not null, isComposite);
        }

        /// <summary>The rule YCShell uses to match documents: trimmed, case-insensitive full path.</summary>
        public bool Matches(string fullFileName) =>
            string.Equals(FullFileName, fullFileName.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
