using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using System.Threading;

class Program
{
    static async Task Main()
    {
        string baseUrl = "https://megalotto.pl/wyniki/lotto";
        string desktopPath = "/home/marcin/Pulpit/Zrzut danych Lotto";
        string filePath = Path.Combine(desktopPath, "PobraneLosowania.txt");

        // Tworzenie folderu na Pulpicie, jeśli nie istnieje
        if (!Directory.Exists(desktopPath))
        {
            Directory.CreateDirectory(desktopPath);
        }

        List<LottoResult> allResults = new List<LottoResult>();
        List<int> years = Enumerable.Range(1957, 2025 - 1957 + 1).ToList();

        Console.WriteLine("Rozpoczynam pobieranie danych wielowątkowo...");

        // Pobieranie danych równocześnie dla wielu lat
        using (SemaphoreSlim semaphore = new SemaphoreSlim(5)) // Ograniczenie do 5 równoczesnych żądań
        {
            await Parallel.ForEachAsync(years, async (year, _) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    string url = $"{baseUrl}/losowania-z-roku-{year}";
                    Console.WriteLine($"Pobieranie wyników dla roku: {year}...");
                    List<LottoResult> results = await GetResultsFromPage(url);
                    lock (allResults)
                    {
                        allResults.AddRange(results);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        // Sortowanie wyników chronologicznie
        allResults = allResults.OrderBy(r => r.Date).ToList();

        // Zapisywanie wyników do pliku
        SaveResultsToFile(allResults, filePath);
        Console.WriteLine($"Wyniki zapisane w: {filePath}");
    }

    static async Task<List<LottoResult>> GetResultsFromPage(string url)
    {
        List<LottoResult> results = new List<LottoResult>();

        using (HttpClient client = new HttpClient())
        {
            try
            {
                string html = await client.GetStringAsync(url);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var drawNodes = doc.DocumentNode.SelectNodes("//ul[@style='position: relative;']");
                if (drawNodes == null) return results;

                foreach (var drawNode in drawNodes)
                {
                    var dateNode = drawNode.SelectSingleNode(".//li[@class='date_in_list']");
                    var numberNodes = drawNode.SelectNodes(".//li[@class='numbers_in_list']");

                    if (dateNode != null && numberNodes != null)
                    {
                        string dateText = dateNode.InnerText.Trim();
                        List<string> numbers = numberNodes.Select(n => n.InnerText.Trim().PadLeft(2, '0')).ToList();
                        string numbersText = string.Join(" ", numbers);

                        if (DateTime.TryParse(dateText, out DateTime drawDate))
                        {
                            results.Add(new LottoResult(drawDate, numbersText));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas pobierania {url}: {ex.Message}");
            }
        }

        return results;
    }

    static void SaveResultsToFile(List<LottoResult> results, string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("Nr   | Data        | Numery");
            writer.WriteLine("------------------------------------------");

            int index = 1;
            foreach (var result in results)
            {
                writer.WriteLine($"{index,-4} | {result.Date:dd/MM/yyyy} | {FormatNumbers(result.Numbers)}");
                index++;
            }
        }
    }

    static string FormatNumbers(string numbers)
    {
        List<string> numList = numbers.Split(' ').Select(n => n.PadLeft(2, '0')).ToList();
        return string.Join(" ", numList);
    }
}

class LottoResult
{
    public DateTime Date { get; }
    public string Numbers { get; }

    public LottoResult(DateTime date, string numbers)
    {
        Date = date;
        Numbers = numbers;
    }
}
