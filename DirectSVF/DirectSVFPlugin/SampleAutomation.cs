﻿/////////////////////////////////////////////////////////////////////
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
using System.Linq;
using System.Runtime.InteropServices;

using Inventor;
using System.Threading;
using Path = System.IO.Path;
using File = System.IO.File;

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

                    for (;;)
                    {
                        Thread.Sleep(intervalMillisec);
                        LogTrace("HeartBeat {0}.",
                            (long) TimeSpan.FromTicks(DateTime.UtcNow.Ticks - ticks).TotalSeconds);
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
                SaveAsSVF(doc);
            } catch (Exception e)
            {
                LogError("Processing failed. " + e.ToString());
            }
        }

        public void RunWithArguments(Document doc, NameValueMap map)
        {

        }

        private void SaveAsSVF(Document Doc)
        {
            using (new HeartBeat())
            {
                LogTrace($"** Saving SVF");

                TranslatorAddIn oAddin = null;

                try
                {
                    ApplicationAddIn svfAddin = inventorApplication
                        .ApplicationAddIns
                        .Cast<ApplicationAddIn>()
                        .FirstOrDefault(item => item.ClassIdString == "{C200B99B-B7DD-4114-A5E9-6557AB5ED8EC}");
                        
                    oAddin = (TranslatorAddIn)svfAddin;

                    if (oAddin != null)
                    {
                        Trace.TraceInformation("SVF Translator addin is available");
                        TranslationContext oContext = inventorApplication.TransientObjects.CreateTranslationContext();
                        // Setting context type
                        oContext.Type = IOMechanismEnum.kFileBrowseIOMechanism;

                        NameValueMap oOptions = inventorApplication.TransientObjects.CreateNameValueMap();
                        // Create data medium;
                        DataMedium oData = inventorApplication.TransientObjects.CreateDataMedium();

                        Trace.TraceInformation("SVF save");
                        var sessionDir = Path.Combine(Directory.GetCurrentDirectory(), "SvfOutput");
                        oData.FileName = Path.Combine(sessionDir, "result.collaboration");
                        var outputDir = Path.Combine(sessionDir, "output");
                        var bubbleFileOriginal = Path.Combine(outputDir, "bubble.json");
                        var bubbleFileNew = Path.Combine(sessionDir, "bubble.json");

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
                        File.Move(bubbleFileOriginal, bubbleFileNew);
                    }
                }
                catch (Exception e)
                {
                    LogError($"********Export to format SVF failed: {e.Message}");
                }
            }
        }

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