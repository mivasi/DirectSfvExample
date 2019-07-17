/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

using Inventor;
using System.Threading;
using Path = System.IO.Path;

namespace DirectSVFPlugin
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        private class HeartBeat : IDisposable
        {
            private Thread t;

            // default is 50s
            public HeartBeat(int intervalMillisec = 50000)
            {
                long ticks = DateTime.UtcNow.Ticks;

                t = new Thread(() =>
                {

                    LogTrace("HeartBeating every {0}ms.", intervalMillisec);

                    for (; ; )
                    {
                        Thread.Sleep(intervalMillisec);
                        LogTrace("HeartBeat {0}.", (long)TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ticks).TotalSeconds);
                    }
                });

                t.Start();
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (t != null)
                    {
                        LogTrace("Ending HeartBeat");
                        t.Abort();
                        t = null;
                    }
                }
            }
        }

        private readonly InventorServer inventorApplication;

        public SampleAutomation(InventorServer inventorApp)
        {
            inventorApplication = inventorApp;
        }

        public void Run(Document doc)
        {
            LogTrace("Processing " + doc.FullFileName);

            try
            {
                if (doc.DocumentType == DocumentTypeEnum.kPartDocumentObject)
                {
                    using (new HeartBeat())
                    {
                        SaveAsSVF(doc);
                    }
                }
                else if (doc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject) // Assembly.
                {
                    using (new HeartBeat())
                    {
                        // TODO: handle the Inventor assembly here
                    }
                }
            }
            catch (Exception e)
            {
                LogError("Processing failed. " + e.ToString());
            }
        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {
            
        }

        private void SaveAsSVF(Document Doc)
        {
            LogTrace($"** Saving SVF");

            TranslatorAddIn oAddin = null;

            try
            {
                foreach (ApplicationAddIn item in inventorApplication.ApplicationAddIns)
                {

                    if (item.ClassIdString == "{C200B99B-B7DD-4114-A5E9-6557AB5ED8EC}")
                    {
                        Trace.TraceInformation("SVF Translator addin is available");
                        oAddin = (TranslatorAddIn)item;
                        break;
                    }
                    else { }
                }


                if (oAddin != null)
                {
                    TranslationContext oContext = inventorApplication.TransientObjects.CreateTranslationContext();
                    // Setting context type
                    oContext.Type = IOMechanismEnum.kFileBrowseIOMechanism;

                    NameValueMap oOptions = inventorApplication.TransientObjects.CreateNameValueMap();
                    // Create data medium;
                    DataMedium oData = inventorApplication.TransientObjects.CreateDataMedium();

                    Trace.TraceInformation("SVF save");
                    var sessionDir = Path.Combine(Directory.GetCurrentDirectory(), "SvfOutput");
                    oData.FileName = Path.Combine(sessionDir, "resultik.collaboration");
                    
                    // Setup SVF options
                    if (oAddin.get_HasSaveCopyAsOptions(Doc, oContext, oOptions))
                    {
                        oOptions.set_Value("GeometryType", 1);
                        oOptions.set_Value("EnableExpressTranslation", true);
                        oOptions.set_Value("SVFFileOutputDir", sessionDir);
                        oOptions.set_Value("ExportFileProperties", false);
                        oOptions.set_Value("ObfuscateLabels", true);
                    }
                    LogTrace($"SVF files are oputput to: {oOptions.get_Value("SVFFileOutputDir")}");

                    oAddin.SaveCopyAs(Doc, oContext, oOptions, oData);
                    Trace.TraceInformation("SVF can be exported.");
                    LogTrace($"** Saved SVF as {oData.FileName}");
                    //ZipFile.CreateFromDirectory(Directory.GetCurrentDirectory(), "result.zip", CompressionLevel.Fastest, false);
                }
            }
            catch (Exception e)
            {
                LogError($"********Export to format SVF failed: {e.Message}");
            }
        }

        #region Common utilities

        /// <summary>
        /// It downloads an onDemand argument. It is a synchronous operation.
        /// </summary>
        /// <param name="name">Argument name in activity you want to download</param>
        /// <param name="suffix">It is possible to add a suffix to URL given in a workitem</param>
        /// <param name="headers">It allows you to add HTTP headers. Use the following format key1:value1;key2:value2</param>
        /// <param name="requestContentOrFile">The Body of the HTTP request</param>
        /// <param name="responseFile">The name of the downloaded file. Use the following format file://filename.ext</param>
        /// <returns>True when download ends successfully</returns>
        private static bool OnDemandHttpOperation(string name, string suffix, string headers, string requestContentOrFile,
            string responseFile)
        {
            LogTrace("!ACESAPI:acesHttpOperation({0},{1},{2},{3},{4})",
                name ?? "", suffix ?? "", headers ?? "", requestContentOrFile ?? "", responseFile ?? "");

            int idx = 0;
            while (true)
            {
                char ch = Convert.ToChar(Console.Read());
                if (ch == '\x3')
                {
                    return true;
                }
                else if (ch == '\n')
                {
                    return false;
                }

                if (idx >= 16)
                {
                    return false;
                }

                idx++;
            }
        }
        #endregion

        #region Logging utilities

        /// <summary>
        /// Log message with 'trace' log level.
        /// </summary>
        private static void LogTrace(string format, params object[] args)
        {
            Trace.TraceInformation(format, args);
        }

        /// <summary>
        /// Log message with 'trace' log level.
        /// </summary>
        private static void LogTrace(string message)
        {
            Trace.TraceInformation(message);
        }

        /// <summary>
        /// Log message with 'error' log level.
        /// </summary>
        private static void LogError(string format, params object[] args)
        {
            Trace.TraceError(format, args);
        }

        /// <summary>
        /// Log message with 'error' log level.
        /// </summary>
        private static void LogError(string message)
        {
            Trace.TraceError(message);
        }

        #endregion
    }
}