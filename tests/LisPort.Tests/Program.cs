using System;
using LisPort.Lis;

namespace LisPort.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.WriteLine("LisPort.Tests (smoke) запущен.");
            Console.WriteLine(".NET Framework 4.8 тестовый каркас подготовлен.");

            if (args.Length >= 1)
            {
                var lisPath = args[0];
                try
                {
                    var summary = LisApi.LoadSummary(lisPath);
                    Console.WriteLine("Summary JSON длина: " + summary.SummaryJson.Length);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return 2;
                }
            }

            return 0;
        }
    }

}
