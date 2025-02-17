using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Threading;

class Program
{
    // Blokada do synchronizacji postępu
    static object progressLock = new object();
    static int processedYears = 0;
    
    static async Task Main()
    {
        // Pobranie linków do lat
        Console.WriteLine("Pobieranie linków do lat...");
        var yearLinks = await FetchYearLinks();
        Console.WriteLine($"Znaleziono {yearLinks.Count} lat.");

        int totalYears = yearLinks.Count;
        List<Task<List<LottoResult>>> tasks = new List<Task<List<LottoResult>>>();

        // Uruchomienie równoległego pobierania wyników dla każdego roku
        foreach (var yearLink in yearLinks)
        {
            tasks.Add(Task.Run(() => {
                var results = FetchLottoResultsFromYear(yearLink);
                lock (progressLock)
                {
                    processedYears++;
                    Console.WriteLine($"Przetworzono {processedYears}/{totalYears} lat: {yearLink}");
                }
                return results;
            }));
        }

        // Czekamy na ukończenie wszystkich zadań
        var resultsByYear = await Task.WhenAll(tasks);
        var allResults = resultsByYear.SelectMany(x => x).ToList();

        // Sortowanie wyników według daty (zakładamy format dd-MM-yyyy)
        allResults.Sort((a, b) =>
        {
            DateTime da, db;
            if (DateTime.TryParseExact(a.Date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out da) &&
                DateTime.TryParseExact(b.Date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out db))
            {
                return da.CompareTo(db);
            }
            return a.Date.CompareTo(b.Date);
        });

        // Budowanie tabeli "pobrane" [Data | Zwycięska kombinacja]
        List<string[]> pobrane = new List<string[]>();
        foreach (var result in allResults)
        {
            string combo = string.Join(" ", result.Numbers.Select(n => n.ToString().PadLeft(2)));
            pobrane.Add(new string[] { result.Date, combo });
        }

        // Wyświetlenie tabeli "pobrane"
        Console.WriteLine("\nTabela 'pobrane':");
        Console.WriteLine("Data         | Zwycięska kombinacja");
        Console.WriteLine("-------------------------------------");
        foreach (var row in pobrane)
        {
            Console.WriteLine(row[0].PadRight(12) + " | " + row[1]);
        }
    }
    
    // Pobiera linki do podstron z wynikami dla poszczególnych lat
    static async Task<List<string>> FetchYearLinks()
    {
        List<string> yearLinks = new List<string>();
        string url = "https://megalotto.pl/lotto/wyniki";
        HttpClient client = new HttpClient();
        var response = await client.GetStringAsync(url);
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(response);

        // Szukamy węzłów <a> w obrębie elementu <p> z klasą zawierającą 'lista_lat'
        var yearLinkNodes = doc.DocumentNode.SelectNodes("//p[contains(@class, 'lista_lat')]//a");
        if (yearLinkNodes != null)
        {
            foreach (var node in yearLinkNodes)
            {
                string href = node.GetAttributeValue("href", "");
                if (!string.IsNullOrEmpty(href))
                {
                    yearLinks.Add(href);
                }
            }
        }
        return yearLinks;
    }
    
    // Pobiera wyniki Lotto z danej podstrony (dla konkretnego roku)
    static List<LottoResult> FetchLottoResultsFromYear(string yearUrl)
    {
        List<LottoResult> results = new List<LottoResult>();
        HttpClient client = new HttpClient();
        var response = client.GetStringAsync(yearUrl).Result;
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(response);

        // Wyniki znajdują się w <div class="lista_ostatnich_losowan">, a każdy wynik w osobnym <ul>
        var draws = doc.DocumentNode.SelectNodes("//div[@class='lista_ostatnich_losowan']/ul");
        if (draws != null)
        {
            foreach (var draw in draws)
            {
                var dateNode = draw.SelectSingleNode(".//li[contains(@class, 'date_in_list')]");
                var numberNodes = draw.SelectNodes(".//li[contains(@class, 'numbers_in_list')]");
                if (dateNode != null && numberNodes != null)
                {
                    List<int> numbers = numberNodes.Select(n => int.Parse(n.InnerText.Trim())).ToList();
                    // Numer losowania nie jest istotny – w naszym systemie generujemy własną numerację
                    results.Add(new LottoResult(0, dateNode.InnerText.Trim(), numbers));
                }
            }
        }
        return results;
    }
}

// Klasa do przechowywania wyniku losowania
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
