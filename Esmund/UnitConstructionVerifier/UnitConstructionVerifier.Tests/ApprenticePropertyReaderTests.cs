using NUnit.Framework;
using System;
using System.IO;
using Inventor;

namespace UnitConstructionVerifier.Tests
{
    [TestFixture]
    public class ApprenticePropertyReaderTests
    {
        [Test]
        public void TestApprenticeServer_ReadStandardTemplateProperties()
        {
            string templatePath = @"C:\Users\Public\Documents\Autodesk\Inventor 2020\Templates\en-US\Standard.ipt";

            if (!System.IO.File.Exists(templatePath))
            {
                Assert.Ignore($"Standard Inventor template was not found at {templatePath}. Skipping Apprentice integration test.");
                return;
            }

            ApprenticeServerComponent? apprentice = null;
            ApprenticeServerDocument? doc = null;

            try
            {
                apprentice = new ApprenticeServerComponent();
                doc = apprentice.Open(templatePath);

                Assert.IsNotNull(doc);
                Assert.AreEqual(templatePath, doc.FullFileName);
                
                Console.WriteLine($"Successfully opened {doc.FullFileName} via Apprentice Server.");

                // Validate that we can access the Design Tracking property set
                PropertySet designTrackingSet = doc.PropertySets["Design Tracking Properties"];
                Assert.IsNotNull(designTrackingSet);
                
                string partNum = designTrackingSet["Part Number"]?.Value?.ToString();
                Console.WriteLine($"Template Part Number: {partNum}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(partNum));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to load Apprentice Server or read properties: {ex.Message}");
            }
            finally
            {
                if (doc != null)
                {
                    try { doc.Close(); } catch { }
                }
                if (apprentice != null)
                {
                    try { apprentice.Close(); } catch { }
                }
            }
        }
    }
}
