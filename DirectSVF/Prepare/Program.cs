﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Prepare
{
    class Program
    {
        struct Command
        {
            public string Description;
            public Func<Publisher, Task> Action;
        }

        private static readonly Command[] commands = new Command[]
        {
            new Command { Description = "Prepare the SVF sample app bundle and activity",          Action = (publisher) => publisher.PreparingDirectSvf() }
        };

        static async Task Main(string[] args)
        {
            // TODO: ensure you've set credentials in `appsettings.json`

            bool useCommandLine = (args.Length == 1);
            try
            {
                Publisher publisher = CreatePublisher();

                if (useCommandLine)
                {
                    await RunCommand(publisher, args[0]);
                }
                else
                {
                    await RunLoop(publisher);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            if (useCommandLine || Debugger.IsAttached)
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to close console...");
                Console.ReadKey();
            }
        }

        private static async Task RunLoop(Publisher publisher)
        {
            PrintHelp();
            while (true)
            {
                Console.Write("> ");
                // wait for pressed key
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key == ConsoleKey.Q) break;

                Console.WriteLine();

                if (key.Key == ConsoleKey.H) { PrintHelp(); continue; }
                if (key.Key == ConsoleKey.C) { Console.Clear(); continue; }

                await RunCommand(publisher, key.KeyChar.ToString());
            }
        }

        private static async Task RunCommand(Publisher publisher, string commandIndex)
        {
            // try to convert the key to index of available actions
            if (int.TryParse(commandIndex, out var index) &&
                (index >= 0 && index < commands.Length))
            {
                Command command = commands[index];
                Console.WriteLine($"Running '{command.Description}'");
                await command.Action(publisher);
            }
            else
            {
                Console.WriteLine("Unknown command");
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Design Automation SVF sample console");
            Console.WriteLine("Available actions:");
            for (int i = 0; i < commands.Length; i++)
            {
                Console.WriteLine($"  {i} - {commands[i].Description}");
            }
            Console.WriteLine("  H - Help");
            Console.WriteLine("  C - Clear console");
            Console.WriteLine("  Q - Quit");
        }

        private static Publisher CreatePublisher()
        {
            var configuration = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", false)
                                .AddJsonFile("appsettings.user.json", false)
                                .AddEnvironmentVariables()
                                .Build();

            return new Publisher(configuration);
        }
    }
}
