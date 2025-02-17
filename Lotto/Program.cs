using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Threading;
using System.IO;

class Program
{
    // Blokada do synchronizacji postępu
    static object progressLock = new object();
    static int processedYears = 0;
    
    static async Task Main()
    {
        // Krok 1: Ustalenie lokalizacji pliku
        string defaultPath = "/home/marcin/Pulpit/LottoCS/PobraneDane.txt";
        Console.WriteLine("Czy utworzyć plik w domyślnej lokalizacji (/home/marcin/Pulpit/LottoCS/PobraneDane.txt)? (y/n)");
        string answer = Console.ReadLine();
        string filePath;
        if (answer.Trim().ToLower().StartsWith("y"))
        {
            filePath = defaultPath;
        }
        else
        {
            Console.WriteLine("Podaj nową lokalizację (pełna ścieżka, w tym nazwa pliku):");
            filePath = Console.ReadLine().Trim();
        }
        
        // Sprawdzenie istnienia katalogu i pliku; jeśli nie istnieją, tworzymy je
        string directory = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Console.WriteLine($"Utworzono katalog: {directory}");
        }
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            Console.WriteLine($"Utworzono plik: {filePath}");
        }
        else
        {
            Console.WriteLine($"Plik już istnieje: {filePath}");
        }
        
        // Krok 2: Pobranie wyników ze wszystkich lat
        Console.WriteLine("Pobieranie linków do lat...");
        var yearLinks = await FetchYearLinks();
        Console.WriteLine($"Znaleziono {yearLinks.Count} lat.");

        int totalYears = yearLinks.Count;
        List<Task<List<LottoResult>>> tasks = new List<Task<List<LottoResult>>>();

        // Równoległe pobieranie wyników dla każdego roku z wyświetleniem progresu
        foreach (var yearLink in yearLinks)
        {
            tasks.Add(Task.Run(() =>
            {
                var results = FetchLottoResultsFromYear(yearLink);
                lock (progressLock)
                {
                    processedYears++;
                    Console.WriteLine($"Przetworzono {processedYears}/{totalYears} lat: {yearLink}");
                }
                return results;
            }));
        }
        var resultsByYear = await Task.WhenAll(tasks);
        var allResults = resultsByYear.SelectMany(x => x).ToList();

        // Sortowanie wyników od najstarszego (zakładamy format dd-MM-yyyy)
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

        // Krok 3: Budowanie kompletnej tabeli "pobrane" z wewnętrzną numeracją
        // Format: "Lp.  | Data         | Zwycięska kombinacja"
        List<string> completeTableLines = new List<string>();
        completeTableLines.Add("Lp.  | Data         | Zwycięska kombinacja");
        for (int i = 0; i < allResults.Count; i++)
        {
            var result = allResults[i];
            string combo = string.Join(" ", result.Numbers.Select(n => n.ToString().PadLeft(2)));
            string line = $"{(i + 1).ToString().PadRight(4)}| {result.Date.PadRight(12)}| {combo}";
            completeTableLines.Add(line);
        }

        // Krok 4: Sprawdzenie poprawności istniejącego pliku
        var fileLines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();

        bool fileIsUpToDate = fileLines.SequenceEqual(completeTableLines);
        if (!fileIsUpToDate)
        {
            Console.WriteLine("\nPlik zawiera niekompletne lub nieprawidłowe dane.");
            Console.WriteLine($"Obecna liczba wierszy: {fileLines.Count}, oczekiwana liczba wierszy: {completeTableLines.Count}");
            Console.WriteLine("Czy nadpisać plik nowymi danymi? (y/n)");
            string answerUpdate = Console.ReadLine();
            if (answerUpdate.Trim().ToLower().StartsWith("y"))
            {
                File.WriteAllLines(filePath, completeTableLines);
                Console.WriteLine("Plik został zaktualizowany.");
            }
            else
            {
                Console.WriteLine("Plik nie został zaktualizowany.");
            }
        }
        else
        {
            Console.WriteLine("\nPlik jest już aktualny.");
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

        // Wyniki znajdują się w <div class='lista_ostatnich_losowan'>, a każdy wynik w osobnym <ul>
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
                    // Numer z zewnętrznej strony nie jest używany – stosujemy wewnętrzną numerację
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
