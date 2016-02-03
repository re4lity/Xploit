﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XPloit.Core.Collections;
using XPloit.Core.Helpers;
using XPloit.Core.Interfaces;
using XPloit.Core.Listeners;
using XPloit.Core.Listeners.IO;
using XPloit.Core.Listeners.Layer;
using XPloit.Modules;
using XPloit.Res;

namespace XPloit
{
    class Program
    {
        static int Main(string[] args)
        {
            // hacer load, reload, probar el global con payload, hacer el listen general con un handler, no cargar exploits sin el load
            //args = new string[] { @"Replay=d:\temp\console.txt" };

            // Linq to library assembly
            BuildLink.Dummy();

            // Configure
            //Console.InputEncoding = Encoding.UTF8;
            //Console.OutputEncoding = Encoding.UTF8;
            using (CommandLayer command = new CommandLayer(new ConsoleIO()))
            {
                command.SetBackgroundColor(ConsoleColor.White);
                command.SetBackgroundColor(ConsoleColor.Black);

                command.AddInput("banner");

                // TODO: Fix \"CryptKey=#Crypt0 M3#\" -> broken line whith white space
                // \"CryptKey=#Crypt0M3#\" 
                Config cfg = ArgumentHelper.Parse<Config>(args);// ("\"Replay=d:\\temp\\console.txt\" \"Listen={Port=23 CryptKey=#Test# IPFilter={OnlyAllowed=127.0.0.1,172.22.32.51}}\" \"User={UserName=root Password=toor}\"");

                // Run file
                if (!string.IsNullOrEmpty(cfg.Replay))
                {
                    try
                    {
                        command.SetForeColor(ConsoleColor.Gray);
                        command.Write(Lang.Get("Reading_File", cfg.Replay));

                        foreach (string line in File.ReadAllLines(cfg.Replay, Encoding.UTF8))
                        {
                            string ap = line.Trim();
                            if (string.IsNullOrEmpty(ap) || ap.StartsWith("#") || ap.StartsWith("//")) continue;
                            command.AddInput(ap);
                        }

                        command.SetForeColor(ConsoleColor.Green);
                        command.WriteLine(Lang.Get("Ok").ToUpperInvariant());
                    }
                    catch
                    {
                        command.SetForeColor(ConsoleColor.Red);
                        command.WriteLine(Lang.Get("Error").ToUpperInvariant());
                    }
                }


                if (cfg.Connect != null)
                {
                    // Connect to server
                    SocketListener client = new SocketListener(cfg.Connect);

                    command.SetForeColor(ConsoleColor.Gray);
                    command.Write(Lang.Get("Connecting_To", client.ToString()));
                    if (client.Start())
                    {
                        command.SetForeColor(ConsoleColor.Green);
                        command.WriteLine(Lang.Get("Ok").ToUpperInvariant());
                    }
                    else
                    {
                        command.SetForeColor(ConsoleColor.Red);
                        command.WriteLine(Lang.Get("Error").ToUpperInvariant());
                    }

                    command.SetForeColor(ConsoleColor.DarkGray);
                    command.WriteLine(Lang.Get("Press_Any_Key"));

                    Console.ReadKey();
                }
                else
                {
                    List<IListener> listeners = new List<IListener>();

                    // Launch socket listener
                    if (cfg.Listen != null) listeners.Add(new SocketListener(cfg.Listen));

                    // Run listeners
                    foreach (IListener listener in listeners)
                    {
                        command.SetForeColor(ConsoleColor.Gray);
                        command.Write(Lang.Get("Starting_Listener", listener.ToString()));

                        if (listener.Start())
                        {
                            command.SetForeColor(ConsoleColor.Green);
                            command.WriteLine(Lang.Get("Ok").ToUpperInvariant());
                        }
                        else
                        {
                            command.SetForeColor(ConsoleColor.Red);
                            command.WriteLine(Lang.Get("Error").ToUpperInvariant());
                        }
                    }

                    // Console listener
                    CommandListener cmd = new CommandListener(command);
                    cmd.Start();
                }
            }

            // Wait exit signal
            JobCollection.Current.KillAll();
            return 0;
        }
    }
}