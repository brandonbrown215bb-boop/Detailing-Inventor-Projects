using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitConstructionVerifier.Operations;
using UploadForm = InventorAddIn_MOM_YCShell.Upload.CFrmUploadChanges;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture, NonParallelizable]
    public class YcShellBridgeTests
    {
        [SetUp]
        public void ResetUploadForm()
        {
            UploadForm.LastStart = null;
            UploadForm.InUploadDuringStart = false;
            UploadForm.ThrowOnStart = false;
            InventorAddIn_MOM_YCShell.CGlobals.ENGINEERING_RULES = new ISG_Common_Generator.CEngineeringRulesWrapper();
            ISG_Common_Generator.CGenerator.Last = null;
        }

        [Test]
        public void Restore_Rebuilds_Insulation_Then_Lets_YCShell_Write_The_Config()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0050\0050.IAM", isLoaded: true, isComposite: false);
            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;
            YcSurface surface = bridge.GetOpenSurfaces().Single();

            bridge.RestoreConfiguration(surface, "{\"configuration\":{}}");

            Assert.IsEmpty(bridge.RestoreProblem);
            Assert.That(bridge.RestoreChecks.Where(c => !c.Found), Is.Empty);
            ISG_Common_Generator.CGenerator generator = ISG_Common_Generator.CGenerator.Last!;
            Assert.AreSame(((InventorAddIn_MOM_YCShell.Types.CAssembly)surface.Handle).AssemblyDocument, generator.Document);
            Assert.AreEqual("{\"configuration\":{}}", generator.Configuration.Json, "parsed by YCShell's own model");
            Assert.AreSame(InventorAddIn_MOM_YCShell.CGlobals.ENGINEERING_RULES!.Data, generator.Rules);
            CollectionAssert.AreEqual(new[] { "GenerateInsulation", "EmbedJSONInAssembly" }, generator.Calls);
        }

        [Test]
        public void Restore_Refuses_When_YCShell_Has_No_Engineering_Rules()
        {
            InventorAddIn_MOM_YCShell.CGlobals.ENGINEERING_RULES = null;
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0050\0050.IAM", isLoaded: true, isComposite: false);
            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;

            var ex = Assert.Throws<InvalidOperationException>(() => bridge.RestoreConfiguration(bridge.GetOpenSurfaces().Single(), "{}"));
            StringAssert.Contains("Log in to MOM", ex!.Message);
            Assert.IsNull(ISG_Common_Generator.CGenerator.Last);
        }

        [Test]
        public void Restore_Refuses_A_Surface_YCShell_Has_Not_Loaded()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0050\0050.IAM", isLoaded: false, isComposite: false);
            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;

            var ex = Assert.Throws<InvalidOperationException>(() => bridge.RestoreConfiguration(bridge.GetOpenSurfaces().Single(), "{}"));
            StringAssert.Contains("has not loaded", ex!.Message);
        }

        [Test]
        public void Upload_Window_Gets_All_Assemblies_With_InUpload_Set_As_YCShell_Does()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0003\0003.IAM", isLoaded: true, isComposite: false);
            server.Add(@"C:\Unit\0074\0074.IAM", isLoaded: true, isComposite: false);
            server.Add(@"C:\Unit\FINAL.IAM", isLoaded: true, isComposite: true);
            UploadForm.ReadInUpload = () => server.InUpload;
            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;
            List<YcSurface> surfaces = bridge.GetOpenSurfaces().ToList();

            bridge.OpenUploadWindow(new[] { surfaces[1], surfaces[2] });

            object[] start = UploadForm.LastStart!;
            Assert.AreSame(server.InventorInstance, start[0]);
            var assemblies = (List<InventorAddIn_MOM_YCShell.Types.CAssembly>)start[1];   // YCShell's own list type
            CollectionAssert.AreEqual(new[] { surfaces[1].Handle, surfaces[2].Handle }, assemblies);
            Assert.AreSame(server.OpenParts, start[2]);
            ((UploadForm.DelegateSetSuppressDocumentEvents)start[3])(true);
            Assert.AreEqual(true, server.LastSuppressDocumentEvents, "bound to YCShell's SetSuppressDocumentEvents");
            Assert.IsTrue(UploadForm.InUploadDuringStart);
            Assert.IsFalse(server.InUpload);
        }

        [Test]
        public void InUpload_Is_Cleared_Even_When_The_Upload_Window_Fails()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0003\0003.IAM", isLoaded: true, isComposite: false);
            UploadForm.ThrowOnStart = true;
            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;

            var ex = Assert.Throws<InvalidOperationException>(() => bridge.OpenUploadWindow(bridge.GetOpenSurfaces()));
            Assert.AreEqual("upload window failed", ex!.Message);
            Assert.IsFalse(server.InUpload);
        }

        [Test]
        public void Uses_YCShells_Own_Hole_Test()
        {
            YcShellBridge bridge = YcShellBridge.FromServer(new InventorAddIn_MOM_YCShell.StandardAddInServer(), out _, out _)!;
            Assert.IsTrue(bridge.HasHoles("part with holes"));
            Assert.IsFalse(bridge.HasHoles("plain part"));
        }

        [Test]
        public void Resolves_Every_Member_On_A_Server_Shaped_Like_YCShell()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();

            YcShellBridge? bridge = YcShellBridge.FromServer(server, out string problem, out IReadOnlyList<MemberCheck> checks);

            Assert.IsNotNull(bridge, problem);
            Assert.That(checks.Where(c => !c.Found), Is.Empty);
            Assert.That(checks.Select(c => c.Name), Does.Contain("RegenerateFoamAndSnaplockHoles"));
            Assert.That(checks.Select(c => c.Name), Does.Contain("CFrmUploadChanges.Start"));
        }

        [Test]
        public void Lists_Open_Surfaces_With_Load_And_Composite_State()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0003\0003.IAM ", isLoaded: true, isComposite: false);
            server.Add(@"C:\Unit\FINAL.IAM", isLoaded: true, isComposite: true);
            server.Add(@"C:\Unit\0010\0010.IAM", isLoaded: false, isComposite: false);

            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;
            IReadOnlyList<YcSurface> surfaces = bridge.GetOpenSurfaces();

            Assert.AreEqual(3, surfaces.Count);
            Assert.AreEqual(@"C:\Unit\0003\0003.IAM", surfaces[0].FullFileName);
            Assert.AreEqual(false, surfaces[0].IsComposite);
            Assert.AreEqual(true, surfaces[1].IsComposite);
            Assert.IsFalse(surfaces[2].IsLoaded);
            Assert.IsNull(surfaces[2].IsComposite);
        }

        [Test]
        public void Regenerate_Runs_Through_RunOperation_Without_YCShells_Active_Assembly_Check()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0074\0074.IAM", isLoaded: true, isComposite: false);
            YcShellBridge bridge = YcShellBridge.FromServer(server, out _, out _)!;
            YcSurface surface = bridge.GetOpenSurfaces().Single();

            bridge.Regenerate(surface);

            Assert.AreSame(surface.Handle, server.RegeneratedItem);
            Assert.AreEqual(false, server.LastCheckForSuppressedOccurrences);
        }

        [Test]
        public void Names_The_Missing_Member_When_YCShell_Changes_Shape()
        {
            var server = new InventorAddIn_MOM_YCShell.ServerWithoutRegenerate();

            YcShellBridge? bridge = YcShellBridge.FromServer(server, out string problem, out IReadOnlyList<MemberCheck> checks);

            Assert.IsNull(bridge);
            StringAssert.Contains("RegenerateFoamAndSnaplockHoles", problem);
            Assert.IsFalse(checks.Single(c => c.Name == "RegenerateFoamAndSnaplockHoles").Found);
        }

        [Test]
        public void Surface_Matching_Follows_YCShells_Trimmed_Case_Insensitive_Rule()
        {
            var server = new InventorAddIn_MOM_YCShell.StandardAddInServer();
            server.Add(@"C:\Unit\0003\0003.IAM", isLoaded: true, isComposite: false);
            YcSurface surface = YcShellBridge.FromServer(server, out _, out _)!.GetOpenSurfaces().Single();

            Assert.IsTrue(surface.Matches(@" c:\unit\0003\0003.iam "));
            Assert.IsFalse(surface.Matches(@"C:\Unit\0006\0006.IAM"));
        }

        [TestCase("Changed_SQ", true)]
        [TestCase(" changed_sq ", true)]
        [TestCase("NoChange", false)]
        [TestCase("Changed_Parameter", false)]
        [TestCase(null, false)]
        public void Changed_SQ_Is_The_Change_Marker(string? status, bool expected)
        {
            Assert.AreEqual(expected, PartChangeStatus.IsChangedSq(status));
        }
    }
}

// Stand-ins shaped like the real YCShell types (MOM_ISG/InventorAddIn/InventorAddIn_MOM_YCShell). The bridge looks
// them up by full name in the server's assembly, so these namespaces and member names must match YCShell's.
#pragma warning disable CS0169, CS0414, CS0649, IDE0051, IDE0044
namespace InventorAddIn_MOM_YCShell
{
    public static class CUtility
    {
        public delegate void DelegateSetStatus(string msg);

        internal static ISG_Common_Generator.CGenerator CreateISGGenerator(Types.FakeAssemblyDocument document,
            JCI.DataModel.ISG.Models.Configuration.CConfiguration configuration, JCI.DataModel.ISG.Models.EngineeringRules.CEngineeringRules rules) =>
            ISG_Common_Generator.CGenerator.Last = new ISG_Common_Generator.CGenerator(document, configuration, rules);
    }

    public static class CGlobals
    {
        internal static ISG_Common_Generator.CEngineeringRulesWrapper? ENGINEERING_RULES;
    }

    public class StandardAddInServer
    {
        private List<Types.CAssembly> m_OpenAssemblyList = new List<Types.CAssembly>();
        private List<object> m_OpenPartList = new List<object>();
        private object m_InventorInstance = new object();
        private Types.CAssembly? m_ActiveAssembly;
        private bool m_InUpload;

        public object? RegeneratedItem { get; private set; }
        public bool? LastCheckForSuppressedOccurrences { get; private set; }

        public void Add(string fullFileName, bool isLoaded, bool isComposite) =>
            m_OpenAssemblyList.Add(new Types.CAssembly
            {
                FullFileName = fullFileName,
                AssemblyDocument = isLoaded ? new Types.FakeAssemblyDocument { IsCompositeAssembly = isComposite } : null,
            });

        private void RegenerateFoamAndSnaplockHoles(object item, CUtility.DelegateSetStatus setStatus) => RegeneratedItem = item;

        private void RunOperation(object item, CommonUI.CFrmRunOperation.DelegateOperation operation, bool checkForSuppressedOccurrences = true)
        {
            LastCheckForSuppressedOccurrences = checkForSuppressedOccurrences;
            operation(item, null!);
        }

        public bool? LastSuppressDocumentEvents { get; private set; }

        public bool InUpload => m_InUpload;
        public object InventorInstance => m_InventorInstance;
        public object OpenParts => m_OpenPartList;

        private void SetSuppressDocumentEvents(bool val) => LastSuppressDocumentEvents = val;
    }

    public class ServerWithoutRegenerate
    {
        private List<Types.CAssembly> m_OpenAssemblyList = new List<Types.CAssembly>();
        private List<object> m_OpenPartList = new List<object>();
        private object m_InventorInstance = new object();
        private Types.CAssembly? m_ActiveAssembly;
        private bool m_InUpload;

        private void RunOperation(object item, CommonUI.CFrmRunOperation.DelegateOperation operation, bool checkForSuppressedOccurrences = true) { }

        private void SetSuppressDocumentEvents(bool val) { }
    }
}

namespace InventorAddIn_MOM_YCShell.CommonUI
{
    public class CFrmRunOperation
    {
        public delegate void DelegateOperation(object item, CUtility.DelegateSetStatus setStatus);
    }
}

namespace InventorAddIn_MOM_YCShell.Upload
{
    public class CFrmUploadChanges
    {
        public delegate void DelegateSetSuppressDocumentEvents(bool val);

        /// <summary>What the last Start received, and whether YCShell's m_InUpload was set at the time.</summary>
        public static object[]? LastStart;
        public static bool InUploadDuringStart;
        public static Func<bool>? ReadInUpload;
        public static bool ThrowOnStart;

        public void Start(object inventor, object assyList, object partList, DelegateSetSuppressDocumentEvents handler)
        {
            LastStart = new[] { inventor, assyList, partList, handler };
            InUploadDuringStart = ReadInUpload?.Invoke() == true;
            if (ThrowOnStart) throw new InvalidOperationException("upload window failed");
        }
    }
}

namespace InventorAddIn_MOM_YCShell.ExtensionMethods
{
    public static class Inventor_PartDocument
    {
        internal static bool HasHoles(this object me) => me is string s && s.Contains("holes");
    }
}

namespace InventorAddIn_MOM_YCShell.Types
{
    public class CAssembly
    {
        public string FullFileName { get; set; } = string.Empty;
        public FakeAssemblyDocument? AssemblyDocument { get; set; }
    }

    public class FakeAssemblyDocument
    {
        public bool IsCompositeAssembly { get; set; }
    }
}

namespace ISG_Common_Generator
{
    public class CEngineeringRulesWrapper
    {
        public JCI.DataModel.ISG.Models.EngineeringRules.CEngineeringRules Data { get; } = new JCI.DataModel.ISG.Models.EngineeringRules.CEngineeringRules();
    }

    public class CGenerator
    {
        public static CGenerator? Last;

        public CGenerator(object document, JCI.DataModel.ISG.Models.Configuration.CConfiguration configuration, object rules)
        {
            Document = document;
            Configuration = configuration;
            Rules = rules;
        }

        public object Document { get; }
        public JCI.DataModel.ISG.Models.Configuration.CConfiguration Configuration { get; }
        public object Rules { get; }
        public List<string> Calls { get; } = new List<string>();

        public void GenerateInsulation() => Calls.Add(nameof(GenerateInsulation));
        public void EmbedJSONInAssembly() => Calls.Add(nameof(EmbedJSONInAssembly));
    }
}

namespace JCI.DataModel.ISG.Models.Configuration
{
    public class CConfiguration
    {
        public string Json { get; set; } = string.Empty;
    }
}

namespace JCI.DataModel.ISG.Models.EngineeringRules
{
    public class CEngineeringRules { }
}

namespace JCI.DataModel.ISG.Models.Extensions
{
    public static class SerializationExtensions
    {
        public static Configuration.CConfiguration ToISGConfiguration(this string json) => new Configuration.CConfiguration { Json = json };
    }
}
#pragma warning restore CS0169, CS0414, CS0649, IDE0051, IDE0044
