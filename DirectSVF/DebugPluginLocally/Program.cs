using System;
using Inventor;
using System.IO;

namespace DebugPluginLocally
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var inv = new InventorConnector())
            {
                InventorServer server = inv.GetInventorServer();

                try
                {
                    Console.WriteLine("Running locally...");
                    // run the plugin
                    DebugSamplePlugin(server);
                }
                catch (Exception e)
                {
                    string message = $"Exception: {e.Message}";
                    if (e.InnerException != null)
                        message += $"{System.Environment.NewLine}    Inner exception: {e.InnerException.Message}";

                    Console.WriteLine(message);
                }
                finally
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        Console.WriteLine("Press any key to exit. All documents will be closed.");
                        Console.ReadKey();
                    }
                }
            }
        }

        /// <summary>
        /// Opens box.ipt and runs SamplePlugin
        /// </summary>
        /// <param name="app"></param>
        private static void DebugSamplePlugin(InventorServer app)
        {
            // get box.ipt absolute path
            string inputPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), @"inputFiles\", "VerticalPlate.ipt");

            // open box.ipt by Inventor
            Document doc = app.Documents.Open(inputPath);

            // create an instance of DirectSVFPlugin
            DirectSVFPlugin.SampleAutomation plugin = new DirectSVFPlugin.SampleAutomation(app);

            // run the plugin
            plugin.Run(doc);

        }
    }
}
