using NAudio.Wave.Asio;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VRA.AudioManager;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║        VRA Audio Manager — ASIO Discovery       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.WriteLine();

        var driverNames = AsioDriver.GetAsioDriverNames();

        if (driverNames.Length == 0)
        {
            Console.WriteLine("  Aucun driver ASIO détecté sur cette machine.");
            Console.WriteLine("  Vérifiez que vos interfaces audio sont installées.");
            return;
        }

        Console.WriteLine($"  {driverNames.Length} driver(s) ASIO détecté(s) :");
        Console.WriteLine(new string('─', 50));

        for (int i = 0; i < driverNames.Length; i++)
        {
            var name = driverNames[i];
            Console.WriteLine();
            Console.WriteLine($"  [{i + 1}] {name}");

            try
            {
                PrintDriverDetails(name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠ Impossible d'ouvrir ce driver : {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(new string('─', 50));
        Console.WriteLine("  Scan terminé. Appuyez sur une touche pour quitter.");
        Console.ReadKey(true);
    }

    private static void PrintDriverDetails(string driverName)
    {
        // NAudio's AsioOut opens the driver, queries capabilities, then we dispose
        using var asio = new NAudio.Wave.AsioOut(driverName);

        int inputChannels = asio.DriverInputChannelCount;
        int outputChannels = asio.DriverOutputChannelCount;

        Console.WriteLine($"      Entrées : {inputChannels} canal/canaux");
        Console.WriteLine($"      Sorties : {outputChannels} canal/canaux");

        if (inputChannels > 0)
        {
            Console.WriteLine("      ┌─ Canaux d'entrée :");
            for (int ch = 0; ch < inputChannels; ch++)
            {
                var channelName = asio.AsioInputChannelName(ch);
                Console.WriteLine($"      │  IN {ch,2} : {channelName}");
            }
            Console.WriteLine("      └");
        }

        if (outputChannels > 0)
        {
            Console.WriteLine("      ┌─ Canaux de sortie :");
            for (int ch = 0; ch < outputChannels; ch++)
            {
                var channelName = asio.AsioOutputChannelName(ch);
                Console.WriteLine($"      │  OUT {ch,2} : {channelName}");
            }
            Console.WriteLine("      └");
        }
    }
}
