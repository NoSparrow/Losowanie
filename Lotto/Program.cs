﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Threading;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Pobieranie danych Lotto...");
        var lottoResults = await Task.Run(() => FetchLottoResults()); // Uruchomienie wątku do pobierania danych
        
        if (lottoResults.Count > 0)
        {
            DisplayResults(lottoResults);
        }
        else
        {
            Console.WriteLine("Nie udało się pobrać wyników.");
        }
    }

    // Pobieranie danych Lotto z internetu
    static List<LottoResult> FetchLottoResults()
    {
        List<LottoResult> results = new List<LottoResult>();
        string url = "https://megalotto.pl/lotto/wyniki";
        HttpClient client = new HttpClient();
        var response = client.GetStringAsync(url).Result;

        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(response);

        var draws = doc.DocumentNode.SelectNodes("//div[@class='lista_ostatnich_losowan']/ul");
        
        if (draws != null)
        {
            foreach (var draw in draws.Reverse()) // Odwracamy, aby mieć wyniki od najstarszego
            {
                var numberNode = draw.SelectSingleNode(".//li[contains(@class, 'nr_in_list')]");
                var dateNode = draw.SelectSingleNode(".//li[contains(@class, 'date_in_list')]");
                var numberNodes = draw.SelectNodes(".//li[contains(@class, 'numbers_in_list')]");

                if (numberNode != null && dateNode != null && numberNodes != null)
                {
                    List<int> numbers = numberNodes.Select(n => int.Parse(n.InnerText.Trim())).ToList();
                    results.Add(new LottoResult(int.Parse(numberNode.InnerText.Trim().TrimEnd('.')), dateNode.InnerText.Trim(), numbers));
                }
            }
        }
        
        return results;
    }

    // Wyświetlanie wyników w tabeli z liczbami pod sobą
    static void DisplayResults(List<LottoResult> results)
    {
        int maxNumbers = results.Max(r => r.Numbers.Count);
        
        Console.WriteLine("Numer | Data         | Liczby");
        Console.WriteLine("----------------------------------");

        foreach (var result in results)
        {
            Console.Write(result.DrawNumber.ToString().PadRight(6) + "| ");
            Console.Write(result.Date.PadRight(12) + "| ");
            Console.WriteLine(string.Join(" ", result.Numbers));
        }
        
        Console.WriteLine("\nLiczby w kolumnach:");
        for (int i = 0; i < maxNumbers; i++)
        {
            foreach (var result in results)
            {
                if (i < result.Numbers.Count)
                    Console.Write(result.Numbers[i].ToString().PadRight(4));
                else
                    Console.Write("    ");
            }
            Console.WriteLine();
        }
    }
}

// Klasa do przechowywania wyników Lotto
class LottoResult
{
    public int DrawNumber { get; }
    public string Date { get; }
    public List<int> Numbers { get; }

    public LottoResult(int drawNumber, string date, List<int> numbers)
    {
        DrawNumber = drawNumber;
        Date = date;
        Numbers = numbers;
    }
}
