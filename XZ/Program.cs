using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Globalization;
using System.Threading.Tasks.Dataflow;
using NLog;
using NLog.Config;
using NLog.Targets;
using Newtonsoft.Json;
using BCrypt.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Data.Common;
using System.ComponentModel.DataAnnotations.Schema;


namespace XZ;

internal static class OnMain
{
    private static async Task Main()
    {
        var db = new UserDatabase();
        db.Load();
        OnAwake.Write();
        await OnStart.Start(db);
    }
}
internal static class OnAwake
{
    internal static void Write()
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("XZ System 1.0 Beta");
        Console.Write("[-- Author : ");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("AARR / Advanced Army of Red Raider");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(" --]\n");
    }
}